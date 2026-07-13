import type { Strings } from './en.ts';
import { fromEn, morseHalf } from './encoders.ts';

// Morse (lite): buttons/labels/short phrases become real dot-dash Morse (space-separated
// letters, " / " between words); LONG strings (toast prose, error causes, hints) stay English
// — encoding a full sentence in Morse is unreadable noise, not a joke anymore. The companion
// `morse-full.ts` is the fully-committed version with no length exception. Function-valued
// entries (which are always sentence-shaped) stay English here, matching the "keep long
// strings readable" rule. Generated from EN via a real encoder (encoders.ts) — see there for
// the character table and the icon-passthrough behavior.
export const morseLite: Strings = fromEn(morseHalf, /* wrapFn */ false);
