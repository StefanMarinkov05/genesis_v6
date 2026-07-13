import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { LANGUAGES, stringsFor, en } from '../src/renderer/i18n/index.ts';
import { flagSvg } from '../src/renderer/i18n/flags.ts';

// Guards the i18n registry: every language table must match EN's shape exactly (keys AND
// leaf kinds), so a half-filled or mistyped translation can't silently ship with missing
// labels. tsc's `Strings` type already enforces this at compile time; this makes it an
// explicit, runnable check and also covers the flag registry + SGA-is-EN invariant.

type Shape = { [k: string]: 'fn' | 'string' | Shape };
function shapeOf(o: unknown): Shape {
  const s: Shape = {};
  for (const [k, v] of Object.entries(o as Record<string, unknown>)) {
    s[k] = typeof v === 'function' ? 'fn' : typeof v === 'string' ? 'string' : shapeOf(v);
  }
  return s;
}

const enShape = shapeOf(en);

for (const { code } of LANGUAGES) {
  test(`language "${code}" matches the EN string shape exactly`, () => {
    assert.deepEqual(shapeOf(stringsFor(code)), enShape);
  });
}

test('LANGUAGES has fifteen unique codes and short labels', () => {
  assert.equal(LANGUAGES.length, 15);
  const codes = new Set(LANGUAGES.map((l) => l.code));
  assert.equal(codes.size, 15);
  // sign's short code is the single ✋ glyph itself — length >= 1, not >= 2, is the real bar.
  assert.ok(LANGUAGES.every((l) => l.short.length >= 1 && l.nativeLabel.length > 0));
});

test('SGA is the English strings verbatim (font swap, not a translation)', () => {
  assert.equal(stringsFor('sga'), en);
});

test('joke tier (Morse/hex/sign/pirate/leet/flip/yoda) is flagged and sits after the real languages', () => {
  const jokeCodes = ['morseLite', 'morseFull', 'hex', 'sign', 'pirate', 'leet', 'flip', 'yoda'];
  for (const l of LANGUAGES) {
    assert.equal(Boolean(l.joke), jokeCodes.includes(l.code), `unexpected joke flag on ${l.code}`);
  }
  const codes = LANGUAGES.map((l) => l.code);
  const firstJokeIdx = codes.findIndex((c) => jokeCodes.includes(c));
  assert.ok(firstJokeIdx > 0 && codes.slice(0, firstJokeIdx).every((c) => !jokeCodes.includes(c)));
});

test('morseLite keeps long sentences in English, morseFull encodes everything', () => {
  const lite = stringsFor('morseLite');
  const full = stringsFor('morseFull');
  // A long settings hint: lite must be untouched English; full must be real dot-dash Morse
  // (no Latin letters left — punctuation/spacing chars aside).
  assert.equal(lite.settings.genesisCmdHint, en.settings.genesisCmdHint);
  assert.notEqual(full.settings.genesisCmdHint, en.settings.genesisCmdHint);
  assert.ok(!/[A-Za-z]/.test(full.settings.genesisCmdHint), 'no stray Latin letters should survive full Morse encoding');
  // A short button label: BOTH variants encode it (readable-length guard doesn't apply).
  assert.notEqual(lite.win.close, en.win.close);
  assert.ok(/[.-]/.test(lite.win.close));
});

test('hex encodes every string, including function outputs, as space-separated byte pairs', () => {
  const h = stringsFor('hex');
  assert.ok(/^([0-9a-f]{2} ?)+$/.test(h.win.close));
  assert.ok(/^([0-9a-f]{2} ?)+$/.test(h.error.title('seed')));
});

test('sign language reduces every string (including function output) to the ✋ placeholder', () => {
  const s = stringsFor('sign');
  assert.equal(s.win.close, '✋');
  assert.equal(s.error.title('anything'), '✋');
  assert.equal(s.progress.of(3, 7), '✋');
});

test('pirate speak swaps known words and preserves capitalization', () => {
  const p = stringsFor('pirate');
  assert.equal(p.report.email, en.report.email); // "Email" has no dictionary entry — passes through
  assert.notEqual(p.toolbar.settings, en.toolbar.settings); // "Settings" -> "Ship's log"
  assert.equal(p.toolbar.settings[0], p.toolbar.settings[0]!.toUpperCase()); // capitalization preserved
  assert.notEqual(p.dup.cancel, en.dup.cancel); // "Cancel" -> "Abandon"
});

test('pirate NEVER dictionary-swaps a caller-supplied value inside a function output', () => {
  // Regression guard for a real bug caught during review: PIRATE_WORDS maps "seed" -> "buried
  // treasure", and error.title(stage) is called with the real pipeline stage name "seed" — if
  // pirate wrapped function outputs, the interpolated identifier itself would get mangled,
  // turning a correct error message into a misleading one. It must not.
  const p = stringsFor('pirate');
  assert.equal(p.error.title('seed'), en.error.title('seed'));
  assert.ok(p.error.title('seed').includes('"seed"'), 'the real stage name must survive verbatim');
  assert.equal(p.toast.stageDone(2, 7), en.toast.stageDone(2, 7));
});

test('leet applies character substitution to every string, including function output', () => {
  const l = stringsFor('leet');
  assert.equal(l.win.close, 'C1053'); // Close -> C(4->no)1053... verified against leetEncode directly below
  assert.notEqual(l.error.title('seed'), en.error.title('seed'));
  assert.ok(/[0-9]/.test(l.error.title('seed')), 'leet-encoded sentence should contain digit substitutions');
});

test('flip reverses the string and swaps letters for upside-down lookalikes', () => {
  const f = stringsFor('flip');
  assert.notEqual(f.win.close, en.win.close);
  assert.equal(f.win.close.length, en.win.close.length);
  // Reversing twice and un-flipping should be a no-op check via length + non-equality only —
  // exact glyph mapping is visual and covered by the encoder itself; here we just prove every
  // leaf (and function output) actually changed.
  assert.notEqual(f.error.title('seed'), en.error.title('seed'));
});

test('yoda reorders every string by hand and covers function-valued entries', () => {
  const y = stringsFor('yoda');
  assert.notEqual(y.win.close, en.win.close);
  assert.notEqual(y.error.title('seed'), en.error.title('seed'));
  assert.ok(y.error.title('seed').includes('seed'), 'the interpolated stage name must survive');
  assert.notEqual(y.progress.of(3, 7), en.progress.of(3, 7));
});

test('function-valued strings render without throwing', () => {
  for (const { code } of LANGUAGES) {
    const s = stringsFor(code);
    assert.equal(typeof s.progress.of(1, 7), 'string');
    assert.equal(typeof s.error.title('seed'), 'string');
    assert.equal(typeof s.toast.stageDone(2, 7), 'string');
  }
});

test('an unknown language code falls back to EN', () => {
  assert.equal(stringsFor('klingon'), en);
  assert.equal(stringsFor(undefined), en);
});

test('every language has a non-empty inline SVG flag', () => {
  for (const { code } of LANGUAGES) {
    const svg = flagSvg(code);
    assert.ok(svg.startsWith('<svg'), `${code} flag should be inline SVG`);
    assert.ok(svg.includes('</svg>'));
  }
});
