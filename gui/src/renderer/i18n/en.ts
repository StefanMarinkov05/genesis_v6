// EN string table — the base/reference language and the source of the `Strings` shape.
// The other five languages (BG/DE/RU/Romani/SGA) were filled in phase 3b; each is typed
// `Strings` so it must match this shape exactly (see the Widen note at the bottom). Keys
// mirror the component vocabulary in design-specs.md §5.
//
// SCOPE BOUNDARY (do not conflate): this table is the GUI's OWN chrome language only. It is
// renderer-local and has ZERO effect on the language of a GENERATED app — those stay
// Bulgarian-only (CLAUDE.md hard requirement) unless the *spec's* meta.language says "en"
// (a completely separate axis, changelog 054). The main process is language-agnostic.
export const en = {
  title: 'DPS-NN — Genesis',
  win: { minimize: 'Minimize', maximize: 'Maximize', close: 'Close' },
  toolbar: { debug: 'Nerd mode', settings: 'Settings', reopen: 'Last result' },
  spec: { heading: 'Specification', upload: 'Upload spec…', pick: 'Select a spec…', none: 'No spec selected' },
  seed: {
    heading: 'Seed',
    upload: 'Upload seed…',
    pick: 'Select a seed…',
    none: 'No seed selected (optional)',
    claude: '✦ Claude',
    claudeHint: 'Claude seed generation — coming in phase 2',
  },
  run: {
    caption: 'Start / Pause / Resume / Stop — R2: locked across all themes',
    start: '▶ Start',
    pause: '❚❚ Pause',
    resume: '▶▶ Resume',
    stop: '■ Stop',
    phase2: 'Pause/Resume — coming in phase 2',
  },
  progress: { idle: 'idle', of: (i: number, t: number) => `${i} / ${t}` },
  dup: {
    title: 'Duplicate build',
    lead: 'This specification + seed combination has already been generated.',
    project: 'Project',
    lastBuild: 'Last build',
    output: 'Output',
    open: 'Open existing',
    regen: 'Generate anyway',
    cancel: 'Cancel',
  },
  report: {
    title: 'Done — the application was generated',
    url: 'URL',
    credentials: 'Credentials',
    email: 'Email',
    password: 'Password',
    db: 'Database',
    start: 'Start',
    runApp: 'Run app',
    killApp: 'Kill app',
    openBrowser: 'Open in browser',
    openFolder: 'Open folder',
    close: 'Close',
    copy: 'copy',
    copied: 'copied',
  },
  error: {
    title: (stage: string) => `Build failed at stage "${stage}"`,
    stage: 'Stage',
    cause: 'Cause',
    retry: 'Retry from this stage',
    fullLog: 'Full log',
    stop: 'Stop',
  },
  settings: {
    title: 'Settings',
    apiKey: 'Anthropic API key',
    apiKeySet: '✓ Key is set (write-only)',
    apiKeyUnset: 'No key set — LLM stages will wait for manual input',
    apiKeyPlaceholder: 'sk-ant-…',
    genesisCmd: 'Genesis command',
    genesisCmdHint: 'For advanced/batch builds and debugging. Defaults to the bundled command.',
    outDir: 'Output folder',
    save: 'Save',
    cancel: 'Cancel',
  },
  toast: {
    started: 'Build started',
    stageDone: (i: number, t: number) => `Stage ${i}/${t} complete`,
    done: 'Done',
    duplicate: 'Duplicate build found',
    stopped: 'Build stopped',
    failed: 'Build failed',
    awaitingLlm: 'Waiting for an LLM response',
    noKey: 'No API key set',
  },
} as const;

// The EN table is authored `as const`, so `typeof en` has narrow *literal* string types
// ('Minimize', not string). A translation table must satisfy the SAME SHAPE but obviously
// with different string values — so we widen the literals to `string` (preserving nested
// objects and function signatures like `progress.of`). Every other language table is typed
// `Strings`, which makes a missing/renamed/mistyped key a COMPILE error (tsc), not a runtime
// surprise — the "later i18n pass is a table fill, not a re-wire" guarantee, enforced.
type Widen<T> = T extends (...args: infer A) => infer R
  ? (...args: A) => R
  : T extends string
    ? string
    : { [K in keyof T]: Widen<T[K]> };

export type Strings = Widen<typeof en>;
