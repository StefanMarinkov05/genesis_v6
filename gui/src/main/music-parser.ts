// Pure logic for picking a random track from a list of filenames — no filesystem/Electron
// import, so it's directly unit-testable under plain node --test (same split as
// registry-parser.ts / recent-projects.ts).

/** Filters to .mp3 files only (case-insensitive extension), sorted for a stable base order. */
export function filterMp3Files(entries: readonly string[]): string[] {
  return entries.filter((f) => f.toLowerCase().endsWith('.mp3')).sort();
}

/** Picks one filename at random from a non-empty list. Caller guards the empty case. */
export function pickRandom(files: readonly string[]): string {
  if (files.length === 0) throw new Error('pickRandom called with an empty list');
  return files[Math.floor(Math.random() * files.length)]!;
}
