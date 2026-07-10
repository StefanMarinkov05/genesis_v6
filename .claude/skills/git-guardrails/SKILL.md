---
name: git-guardrails
description: Mechanical enforcement of git safety in Claude Code - hooks that block dangerous git commands (push to main, reset --hard, clean -f, branch -D, force push) before they execute; use when setting up a new repo or agent pipeline, when the user wants to prevent destructive git operations by agents, or after any incident where an agent ran a destructive git command. Complements git-workflow, which states the rules - this one makes breaking them impossible.
---

# Git Guardrails (Enforcement Layer)

Instructions decay under context pressure; hooks don't. For agent pipelines, every git-workflow rule that *can* be mechanically enforced *should* be - an agent that cannot run `reset --hard` never needs to be trusted about it. Same philosophy as verification-gates: replace trust with checks.

## Claude Code PreToolUse hook

Add to the project's `.claude/settings.json` (project-level, so it travels with the repo and binds every agent session):

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          { "type": "command", "command": ".claude/hooks/git-guard.sh" }
        ]
      }
    ]
  }
}
```

`.claude/hooks/git-guard.sh` (reads the tool input JSON on stdin, exit 2 blocks the call):

```bash
#!/usr/bin/env bash
# Blocks destructive git commands before execution. Exit 2 = block + message to agent.
cmd=$(jq -r '.tool_input.command // empty')

deny() { echo "BLOCKED by git-guard: $1. See git-workflow skill; ask the human if genuinely needed." >&2; exit 2; }

case "$cmd" in
  *"git push"*"--force"*|*"git push"*"-f "*) deny "force push" ;;
  *"git push"*) [[ "$cmd" == *"origin main"* || "$cmd" == *"origin master"* ]] && deny "direct push to main - use a PR" ;;
  *"git reset --hard"*)      deny "reset --hard (use git stash or ask)" ;;
  *"git clean"*"-f"*)        deny "clean -f (deletes untracked files)" ;;
  *"git branch"*" -D"*)      deny "force-delete branch" ;;
  *"git checkout ."*|*"git restore ."*) deny "bulk discard of working changes" ;;
  *"git rebase"*) [[ "$cmd" == *"main"* && "$cmd" == *"-i"* ]] && deny "interactive rebase touching main" ;;
  *"git commit"*"--no-verify"*|*"git push"*"--no-verify"*) deny "hook bypass (--no-verify)" ;;
  *"rm -rf .git"*)           deny "deleting the repository" ;;
esac
exit 0
```

`chmod +x .claude/hooks/git-guard.sh`, commit both files. Test the guard itself: ask the agent to run `git reset --hard` and confirm the block fires - an untested guardrail is a hope (same rule as untested backups).

## Companion: pre-commit hooks (server-side of the same idea)

Local commit-time gates via the stack's tool (husky+lint-staged / pre-commit for Python): format, typecheck, affected tests, secret scan. These run for humans and agents alike and mirror CI's G1 so failures surface seconds after the mistake, not minutes later in CI. Keep them fast (<10s) or they'll get bypassed - and note the guard above blocks `--no-verify`, closing that loophole for agents.

## Design rules for guardrails

- **Block, don't warn** - warnings train dismissal (verification-gates principle).
- **Every block message names the safe alternative** ("use a PR", "use git stash") so the agent can proceed correctly instead of retrying variants.
- **Escape hatch is the human**: genuinely needed destructive operations are run by you in a terminal, not unlocked for agents. If you're unlocking one weekly, the workflow is wrong - fix the workflow, not the guard.
- Keep the deny-list short and high-consequence. A guard that blocks routine work gets deleted in frustration, which is worse than a narrow guard that survives.
- Extend the same pattern to other irreversible tools as your pipeline grows: deploy commands, database drops, `rm -rf` outside the workspace.
