import '@xterm/xterm/css/xterm.css';
import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import type { BuildReport, DuplicateMatch, FileHandle } from '@shared/ipc-types';
import { en } from './i18n/en';
import { activeButton, enablement, type RunState } from './state';
import './styles.css';

// Renderer bootstrap for the MVP (NOVA + EN). It owns screen state and talks to the
// privileged main process only through window.gui (the typed preload surface). No fs, no
// child_process, no key here — the renderer is fully sandboxed (security model).

const gui = window.gui;
const t = en;

function $<T extends HTMLElement = HTMLElement>(id: string): T {
  const el = document.getElementById(id);
  if (!el) throw new Error(`missing #${id}`);
  return el as T;
}

// ---- run/selection state ----------------------------------------------------
let specs: FileHandle[] = [];
let seeds: FileHandle[] = [];
let selectedSpec: FileHandle | null = null;
let selectedSeed: FileHandle | null = null;
let runId: string | null = null;
let runState: RunState = 'idle';
let pendingDup: DuplicateMatch | null = null;

const TOTAL_STAGES = 7; // the pipeline stages that emit progress (genesis-runner PIPELINE_STAGES)

// ---- debug terminal ---------------------------------------------------------
const term = new Terminal({
  convertEol: true,
  fontFamily: '"Cascadia Code", "Consolas", ui-monospace, monospace',
  fontSize: 12,
  theme: { background: '#03070e', foreground: '#a9c3e6', cursor: '#3fd0ff' },
  scrollback: 5000,
  disableStdin: true, // display-only: we pipe the CLI's output, we never send input to it
});
const fit = new FitAddon();
term.loadAddon(fit);

function ensureTerminalMounted(): void {
  if (term.element) return;
  term.open($('terminal'));
}
function refitTerminal(): void {
  if (!term.element || $('terminal-wrap').hasAttribute('hidden')) return;
  try {
    fit.fit();
  } catch {
    /* terminal not laid out yet */
  }
}

// ---- toasts -----------------------------------------------------------------
type ToastKind = 'info' | 'success' | 'warning' | 'danger';
function toast(kind: ToastKind, msg: string, sub = ''): void {
  const el = document.createElement('div');
  el.className = `toast ${kind}`;
  el.setAttribute('role', 'status');
  el.innerHTML =
    `<span class="tdot"></span><div><div class="tmsg"></div>` +
    (sub ? `<div class="tsub"></div>` : '') +
    `</div><button type="button" class="tclose" aria-label="close">✕</button><span class="trail"></span>`;
  (el.querySelector('.tmsg') as HTMLElement).textContent = msg;
  if (sub) (el.querySelector('.tsub') as HTMLElement).textContent = sub;
  el.querySelector('.tclose')!.addEventListener('click', () => el.remove());
  $('toast-stack').appendChild(el);
  let timer = window.setTimeout(() => el.remove(), 5000);
  el.addEventListener('mouseenter', () => clearTimeout(timer));
  el.addEventListener('mouseleave', () => (timer = window.setTimeout(() => el.remove(), 2500)));
}

// ---- run-control state ------------------------------------------------------
function setRunState(s: RunState): void {
  runState = s;
  const en2 = enablement(s);
  ($('run-start') as HTMLButtonElement).disabled = !en2.start;
  ($('run-pause') as HTMLButtonElement).disabled = !en2.pause;
  ($('run-resume') as HTMLButtonElement).disabled = !en2.resume;
  ($('run-stop') as HTMLButtonElement).disabled = !en2.stop;
  const active = activeButton(s);
  for (const key of ['start', 'pause', 'resume', 'stop'] as const) {
    $(`run-${key}`).classList.toggle('active', active === key);
  }
  // A run can't start without a spec.
  if ((s === 'idle' || s === 'done' || s === 'error' || s === 'stopped') && !selectedSpec) {
    ($('run-start') as HTMLButtonElement).disabled = true;
  }
}

function setProgress(index: number, total: number, stage: string, cls: '' | 'running' | 'success' | 'error'): void {
  $('progress-stage').textContent = stage;
  $('progress-count').textContent = t.progress.of(index, total);
  const fillEl = $('progress-fill');
  fillEl.style.width = `${Math.round((index / total) * 100)}%`;
  fillEl.classList.remove('running', 'success', 'error');
  if (cls) fillEl.classList.add(cls);
}

// ---- selection UI -----------------------------------------------------------
function optionLabel(f: FileHandle): string {
  return f.source === 'uploaded' ? `${f.name} (uploaded)` : `${f.name} · ${f.source}`;
}

function populateSelect(select: HTMLSelectElement, list: FileHandle[], placeholder: string): void {
  select.innerHTML = '';
  const ph = document.createElement('option');
  ph.value = '';
  ph.textContent = placeholder;
  select.appendChild(ph);
  list.forEach((f, i) => {
    const opt = document.createElement('option');
    opt.value = String(i);
    opt.textContent = optionLabel(f);
    select.appendChild(opt);
  });
}

async function reloadLists(): Promise<void> {
  [specs, seeds] = await Promise.all([gui.specList(), gui.seedList()]);
  populateSelect($('spec-select') as HTMLSelectElement, specs, t.spec.pick);
  populateSelect($('seed-select') as HTMLSelectElement, seeds, t.seed.pick);
}

function onSpecChosen(f: FileHandle | null): void {
  selectedSpec = f;
  $('spec-well').textContent = f ? f.name : t.spec.none;
  setRunState(runState); // re-evaluate Start enablement
}
function onSeedChosen(f: FileHandle | null): void {
  selectedSeed = f;
  $('seed-well').textContent = f ? f.name : t.seed.none;
}

// ---- build lifecycle --------------------------------------------------------
async function attemptStart(): Promise<void> {
  if (!selectedSpec) {
    toast('warning', 'Select a spec first');
    return;
  }
  const dup = await gui.checkDuplicate(selectedSpec, selectedSeed);
  if (dup) {
    pendingDup = dup;
    openDuplicateDialog(dup);
    return;
  }
  await doStart();
}

async function doStart(): Promise<void> {
  if (!selectedSpec) return;
  term.clear();
  setProgress(0, TOTAL_STAGES, '…', 'running');
  runId = await gui.buildStart(selectedSpec, selectedSeed);
  setRunState('running');
  toast('info', t.toast.started, selectedSpec.name + (selectedSeed ? ` · ${selectedSeed.name}` : ''));
}

async function doStop(): Promise<void> {
  if (!runId) return;
  await gui.buildStop(runId);
  setRunState('stopped');
  setProgress(Number($('progress-count').textContent!.split('/')[0]) || 0, TOTAL_STAGES, 'stopped', '');
  toast('warning', t.toast.stopped);
}

// ---- duplicate dialog -------------------------------------------------------
function openDuplicateDialog(dup: DuplicateMatch): void {
  $('dup-project').textContent = dup.project;
  $('dup-last').textContent = new Date(dup.ts).toLocaleString();
  $('dup-out').textContent = dup.outDir;
  $('dup-dialog').setAttribute('data-open', 'true');
  ($('dup-open') as HTMLButtonElement).focus();
}
function closeDuplicateDialog(): void {
  $('dup-dialog').setAttribute('data-open', 'false');
}

// ---- report / error screens -------------------------------------------------
let lastReport: BuildReport | null = null;

function copyField(value: string): string {
  const safe = value.replace(/"/g, '&quot;');
  return (
    `<div class="copyfield"><code>${escapeHtml(value)}</code>` +
    `<button type="button" data-copy="${safe}">${t.report.copy}</button></div>`
  );
}
function escapeHtml(s: string): string {
  return s.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]!);
}

function showReport(report: BuildReport): void {
  lastReport = report;
  const dl = $('report-dl');
  const rows: string[] = [];
  if (report.url) rows.push(`<dt>${t.report.url}</dt><dd>${copyField(report.url)}</dd>`);
  for (const c of report.credentials) {
    rows.push(`<dt>${escapeHtml(c.role)}</dt><dd>${copyField(`${c.email} / ${c.password}`)}</dd>`);
  }
  if (report.dbName) rows.push(`<dt>${t.report.db}</dt><dd>${copyField(report.dbName)}</dd>`);
  if (report.startCommand) rows.push(`<dt>${t.report.start}</dt><dd>${copyField(report.startCommand)}</dd>`);
  dl.innerHTML = rows.join('');
  dl.querySelectorAll<HTMLButtonElement>('[data-copy]').forEach((b) =>
    b.addEventListener('click', () => {
      void navigator.clipboard.writeText(b.getAttribute('data-copy')!);
      b.textContent = t.report.copied;
      setTimeout(() => (b.textContent = t.report.copy), 1200);
    }),
  );
  ($('report-open-browser') as HTMLButtonElement).disabled = !report.url;
  $('report-title-text').textContent = t.report.title;
  $('report-screen').setAttribute('data-open', 'true');
}

function showError(stage: string | null, message: string): void {
  $('error-title-text').textContent = t.error.title(stage ?? '—');
  $('error-stage').textContent = stage ?? '—';
  $('error-cause').textContent = message;
  $('error-screen').setAttribute('data-open', 'true');
  // Surface the terminal so the failing output is visible.
  if ($('terminal-wrap').hasAttribute('hidden')) {
    ($('debug-toggle') as HTMLInputElement).checked = true;
    toggleDebug(true);
  }
}

function closeResultScreens(): void {
  $('report-screen').setAttribute('data-open', 'false');
  $('error-screen').setAttribute('data-open', 'false');
}

// ---- settings ---------------------------------------------------------------
async function openSettings(): Promise<void> {
  const s = await gui.settingsGet();
  ($('s-key') as HTMLInputElement).value = '';
  ($('s-cmd') as HTMLInputElement).value = s.genesisCommand;
  ($('s-out') as HTMLInputElement).value = s.outDir;
  const state = $('s-key-state');
  state.textContent = s.apiKeySet ? t.settings.apiKeySet : t.settings.apiKeyUnset;
  state.classList.toggle('unset', !s.apiKeySet);
  $('settings-sheet').setAttribute('data-open', 'true');
}
async function saveSettings(): Promise<void> {
  const key = ($('s-key') as HTMLInputElement).value;
  await gui.settingsSet({
    genesisCommand: ($('s-cmd') as HTMLInputElement).value,
    outDir: ($('s-out') as HTMLInputElement).value,
    ...(key ? { apiKey: key } : {}),
  });
  $('settings-sheet').setAttribute('data-open', 'false');
  toast('success', 'Settings saved');
}

// ---- debug toggle -----------------------------------------------------------
function toggleDebug(on: boolean): void {
  const wrap = $('terminal-wrap');
  if (on) {
    wrap.removeAttribute('hidden');
    ensureTerminalMounted();
    requestAnimationFrame(refitTerminal);
  } else {
    wrap.setAttribute('hidden', '');
  }
}

// ---- static labels ----------------------------------------------------------
function applyStrings(): void {
  $('title-text').textContent = t.title;
  $('debug-label').textContent = t.toolbar.debug;
  $('settings-label').textContent = t.toolbar.settings;
  $('spec-heading').textContent = t.spec.heading;
  $('seed-heading').textContent = t.seed.heading;
  $('spec-well').textContent = t.spec.none;
  $('seed-well').textContent = t.seed.none;
  $('spec-upload').textContent = t.spec.upload;
  $('seed-upload').textContent = t.seed.upload;
  ($('seed-claude') as HTMLButtonElement).title = t.seed.claudeHint;
  $('run-caption-text').textContent = t.run.caption;
  $('run-start').textContent = t.run.start;
  $('run-pause').textContent = t.run.pause;
  $('run-resume').textContent = t.run.resume;
  $('run-stop').textContent = t.run.stop;
  ($('run-pause') as HTMLButtonElement).title = t.run.phase2;
  ($('run-resume') as HTMLButtonElement).title = t.run.phase2;
  $('progress-stage').textContent = t.progress.idle;
  $('progress-count').textContent = t.progress.of(0, TOTAL_STAGES);
  // dialog labels
  $('dup-title').textContent = t.dup.title;
  $('dup-lead').textContent = t.dup.lead;
  $('dup-l-project').textContent = t.dup.project;
  $('dup-l-last').textContent = t.dup.lastBuild;
  $('dup-l-out').textContent = t.dup.output;
  $('dup-open').textContent = t.dup.open;
  $('dup-regen').textContent = t.dup.regen;
  $('dup-cancel').textContent = t.dup.cancel;
  $('report-open-browser').textContent = t.report.openBrowser;
  $('report-open-folder').textContent = t.report.openFolder;
  $('report-new').textContent = t.report.newBuild;
  $('error-l-stage').textContent = t.error.stage;
  $('error-retry').textContent = t.error.retry;
  $('error-new').textContent = t.report.newBuild;
  $('settings-title').textContent = t.settings.title;
  $('s-key-label').textContent = t.settings.apiKey;
  ($('s-key') as HTMLInputElement).placeholder = t.settings.apiKeyPlaceholder;
  $('s-cmd-label').textContent = t.settings.genesisCmd;
  $('s-cmd-hint').textContent = t.settings.genesisCmdHint;
  $('s-out-label').textContent = t.settings.outDir;
  $('settings-save').textContent = t.settings.save;
  $('settings-cancel').textContent = t.settings.cancel;
}

// ---- file upload (renderer reads bytes, main validates + stages) ------------
async function uploadFile(kind: 'spec' | 'seed'): Promise<void> {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = 'application/json,.json';
  input.addEventListener('change', async () => {
    const file = input.files?.[0];
    if (!file) return;
    const bytes = new Uint8Array(await file.arrayBuffer());
    try {
      const handle =
        kind === 'spec' ? await gui.specUpload(bytes, file.name) : await gui.seedUpload(bytes, file.name);
      await reloadLists();
      if (kind === 'spec') {
        specs.push(handle);
        onSpecChosen(handle);
      } else {
        seeds.push(handle);
        onSeedChosen(handle);
      }
      toast('success', `${kind === 'spec' ? 'Spec' : 'Seed'} uploaded`, handle.name);
    } catch (e) {
      toast('danger', 'Upload failed', (e as Error).message);
    }
  });
  input.click();
}

// ---- wiring -----------------------------------------------------------------
function wire(): void {
  // window chrome
  $('win-min').addEventListener('click', () => gui.windowMinimize());
  $('win-max').addEventListener('click', () => gui.windowMaximize());
  $('win-close').addEventListener('click', () => gui.windowClose());

  // selection
  ($('spec-select') as HTMLSelectElement).addEventListener('change', (e) => {
    const v = (e.target as HTMLSelectElement).value;
    onSpecChosen(v === '' ? null : specs[Number(v)]);
  });
  ($('seed-select') as HTMLSelectElement).addEventListener('change', (e) => {
    const v = (e.target as HTMLSelectElement).value;
    onSeedChosen(v === '' ? null : seeds[Number(v)]);
  });
  $('spec-upload').addEventListener('click', () => void uploadFile('spec'));
  $('seed-upload').addEventListener('click', () => void uploadFile('seed'));

  // run controls
  $('run-start').addEventListener('click', () => void attemptStart());
  $('run-stop').addEventListener('click', () => void doStop());

  // debug
  ($('debug-toggle') as HTMLInputElement).addEventListener('change', (e) =>
    toggleDebug((e.target as HTMLInputElement).checked),
  );

  // duplicate dialog
  $('dup-open').addEventListener('click', () => {
    closeDuplicateDialog();
    if (pendingDup) void gui.openPath(pendingDup.outDir);
  });
  $('dup-regen').addEventListener('click', () => {
    closeDuplicateDialog();
    void doStart();
  });
  $('dup-cancel').addEventListener('click', closeDuplicateDialog);

  // report/error actions
  $('report-open-browser').addEventListener('click', () => {
    if (lastReport?.url) void gui.openExternal(lastReport.url);
  });
  $('report-open-folder').addEventListener('click', () => {
    if (lastReport?.outDir) void gui.openPath(lastReport.outDir);
  });
  $('report-new').addEventListener('click', () => {
    closeResultScreens();
    setRunState('idle');
  });
  $('error-new').addEventListener('click', () => {
    closeResultScreens();
    setRunState('idle');
  });

  // settings
  $('settings-open').addEventListener('click', () => void openSettings());
  $('settings-close').addEventListener('click', () => $('settings-sheet').setAttribute('data-open', 'false'));
  $('settings-cancel').addEventListener('click', () => $('settings-sheet').setAttribute('data-open', 'false'));
  $('settings-save').addEventListener('click', () => void saveSettings());

  // keyboard: Esc closes overlays; Enter in dup dialog = quiet default (open existing)
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      closeDuplicateDialog();
      closeResultScreens();
      $('settings-sheet').setAttribute('data-open', 'false');
    }
    if (e.key === 'Enter' && $('dup-dialog').getAttribute('data-open') === 'true') {
      $('dup-open').click();
    }
  });

  window.addEventListener('resize', refitTerminal);
}

// ---- IPC event subscriptions ------------------------------------------------
function subscribe(): void {
  gui.onLog((line) => {
    ensureTerminalMounted();
    term.writeln(line);
  });
  gui.onProgress((e) => {
    setProgress(e.index, e.total, String(e.stage), e.phase === 'error' ? 'error' : 'running');
    if (e.phase === 'end') toast('success', t.toast.stageDone(e.index, e.total), String(e.stage));
  });
  gui.onAwaitingLlm((e) => {
    setRunState('awaiting-llm');
    toast('warning', t.toast.awaitingLlm, e.stage);
  });
  gui.onDone((report) => {
    setProgress(TOTAL_STAGES, TOTAL_STAGES, 'report', 'success');
    setRunState('done');
    toast('success', t.toast.done);
    showReport(report);
  });
  gui.onError((e) => {
    setProgress(Number($('progress-count').textContent!.split('/')[0]) || 0, TOTAL_STAGES, String(e.stage ?? 'error'), 'error');
    setRunState('error');
    toast('danger', t.toast.failed, e.stage ?? undefined);
    showError(e.stage ? String(e.stage) : null, e.message);
  });
}

// ---- ambient starfield (z.canvas motion; respects reduced-motion) -----------
function starfield(): void {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const win = $('window');
  const canvas = $('stars') as HTMLCanvasElement;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  let particles: { x: number; y: number; r: number; s: number }[] = [];
  function resize(): void {
    const r = win.getBoundingClientRect();
    canvas.width = r.width * dpr;
    canvas.height = r.height * dpr;
    canvas.style.width = `${r.width}px`;
    canvas.style.height = `${r.height}px`;
    if (!particles.length) {
      for (let i = 0; i < 80; i++) {
        particles.push({
          x: Math.random() * canvas.width,
          y: Math.random() * canvas.height,
          r: Math.random() * 1.4 * dpr + 0.3,
          s: (Math.random() * 0.05 + 0.01) * dpr,
        });
      }
    }
  }
  window.addEventListener('resize', resize);
  resize();
  function frame(): void {
    const accent = getComputedStyle(win).getPropertyValue('--accent').trim() || '#3fd0ff';
    ctx!.clearRect(0, 0, canvas.width, canvas.height);
    ctx!.fillStyle = accent;
    for (const p of particles) {
      ctx!.globalAlpha = 0.5;
      ctx!.beginPath();
      ctx!.arc(p.x, p.y, p.r, 0, Math.PI * 2);
      ctx!.fill();
      if (!reduce) {
        p.y += p.s;
        if (p.y > canvas.height) {
          p.y = 0;
          p.x = Math.random() * canvas.width;
        }
      }
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}

// ---- boot -------------------------------------------------------------------
async function boot(): Promise<void> {
  applyStrings();
  wire();
  subscribe();
  starfield();
  setRunState('idle');
  await reloadLists();
  const footer = await gui.footerGet();
  $('footer').textContent = footer;
}

void boot();
