import type { Strings } from './en.ts';
import { fromEn, signGlyph } from './encoders.ts';

// Sign language — per Stefan: "everything will be blank." Every leaf (including function
// outputs — the progress counter, error titles) becomes the same placeholder glyph (✋)
// instead of empty strings, so buttons/labels stay a sane clickable size rather than
// collapsing to zero width. There is no text, only the gesture.
export const sign: Strings = fromEn(signGlyph, /* wrapFn */ true);
