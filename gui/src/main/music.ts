import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { musicDir } from './paths';
import { filterMp3Files, pickRandom } from './music-parser';

// "Play Veselin Marinov" (IMPERATOR theme only) — Stefan drops his own MP3s into design/music/;
// this never ships any audio itself. Read-only, best-effort: an empty/missing folder means "no
// track available" rather than a thrown error, matching the same stance logs/registry.json's
// reader takes for a missing file.

/** Every .mp3 filename found in design/music/, or [] if the folder doesn't exist / is empty. */
export function listMusicFiles(): string[] {
  const dir = musicDir();
  if (!existsSync(dir)) return [];
  return filterMp3Files(readdirSync(dir));
}

/** Absolute path to a random track in design/music/, or null if none exist. */
export function pickRandomTrack(): string | null {
  const files = listMusicFiles();
  if (files.length === 0) return null;
  return join(musicDir(), pickRandom(files));
}
