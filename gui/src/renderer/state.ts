// Renderer mirror of the run-control state machine in docs/explanation/gui-architecture.md.
// It decides only which run-control buttons are legal in each state (a button is enabled
// only where its transition is valid, per design-specs.md §5). Pause/Resume are phase-2
// features and stay disabled in the MVP regardless of state.

export type RunState = 'idle' | 'running' | 'awaiting-llm' | 'done' | 'error' | 'stopped';

export interface ButtonEnablement {
  start: boolean;
  pause: boolean;
  resume: boolean;
  stop: boolean;
}

const MVP_PAUSE_RESUME_ENABLED = false; // phase 2

export function enablement(state: RunState): ButtonEnablement {
  const canStart = state === 'idle' || state === 'done' || state === 'error' || state === 'stopped';
  const canStop = state === 'running' || state === 'awaiting-llm';
  return {
    start: canStart,
    stop: canStop,
    pause: MVP_PAUSE_RESUME_ENABLED && state === 'running',
    resume: MVP_PAUSE_RESUME_ENABLED && state === 'stopped',
  };
}

/** The button whose glow "pulse" should animate (the live action), or null. */
export function activeButton(state: RunState): keyof ButtonEnablement | null {
  return state === 'running' || state === 'awaiting-llm' ? 'start' : null;
}
