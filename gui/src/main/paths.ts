import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { app } from 'electron';

// The CLI contract (gui-architecture.md) is absolute on one point: the child process
// cwd MUST be the Genesis root — the CLI resolves templates/, spec.schema.json,
// feature-templates/ and writes logs/, prompts/, the registry all relative to cwd.
// The gui/ app is a sibling of those, so the root is found by walking up from the app
// directory to the first ancestor that actually holds templates/ + spec.schema.json.
// Never guess a path; verify the marker files exist (same discipline as the CLI's own
// GenesisRoot()).

let cached: string | null = null;

function looksLikeGenesisRoot(dir: string): boolean {
  return existsSync(join(dir, 'templates')) && existsSync(join(dir, 'spec.schema.json'));
}

/** Resolve the Genesis repo root, or throw with a clear message (fail loud, not blind). */
export function genesisRoot(): string {
  if (cached) return cached;

  // In dev, app.getAppPath() → gui/ ; packaged, it's inside resources. Start from both
  // the app path and the executable dir, walk up to the marker.
  const starts = [app.getAppPath(), dirname(app.getPath('exe')), process.cwd()];
  for (const start of starts) {
    let dir = resolve(start);
    // Walk up to the filesystem root.
    for (;;) {
      if (looksLikeGenesisRoot(dir)) {
        cached = dir;
        return dir;
      }
      const parent = dirname(dir);
      if (parent === dir) break;
      dir = parent;
    }
  }
  throw new Error(
    'Could not locate the Genesis root (a directory containing templates/ and spec.schema.json) ' +
      'above the GUI app. The GUI must live as gui/ inside the Genesis repo.',
  );
}

/** Directories the spec list-select scans, in display order (matches the CLI pickers). */
export function specSearchDirs(): { dir: string; source: 'cwd' | 'fixtures' | 'archive' }[] {
  const root = genesisRoot();
  return [
    { dir: root, source: 'cwd' },
    { dir: join(root, 'fixtures'), source: 'fixtures' },
    { dir: join(root, 'prompts', 'out', 'schemas'), source: 'archive' },
  ];
}

/** Directories the seed list-select scans (matches ResolveSeedInteractively). */
export function seedSearchDirs(): { dir: string; source: 'seeds' | 'archive' }[] {
  const root = genesisRoot();
  return [
    { dir: join(root, 'seeds'), source: 'seeds' },
    { dir: join(root, 'prompts', 'out', 'seeds'), source: 'archive' },
  ];
}

/** Where uploaded files are staged before being handed to the CLI. */
export function uploadsDir(): string {
  return join(genesisRoot(), 'gui', '.uploads');
}

/** Where the IMPERATOR theme's "Play Veselin Marinov" media lives (Stefan drops files in here),
 *  split into 3 category subfolders instead of one flat directory. */
export function musicDir(): string {
  return join(genesisRoot(), 'design', 'music');
}
export type MusicCategory = 'songsAudio' | 'concertsAudio' | 'concertsVideo';
const MUSIC_SUBDIRS: Record<MusicCategory, string> = {
  songsAudio: 'songs-audio',
  concertsAudio: 'concerts-audio',
  concertsVideo: 'concerts-video',
};
export function musicCategoryDir(category: MusicCategory): string {
  return join(musicDir(), MUSIC_SUBDIRS[category]);
}
