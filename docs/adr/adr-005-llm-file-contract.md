# ADR-005: LLM stages are file contracts

**Status**: accepted

**Context**: LLM steps must work with or without an API key, be auditable, and never inject unvalidated output into the pipeline.

**Decision**: every exchange is `prompts/out/<stage>-request.md` -> `prompts/in/<stage>-response.json`. Manual mode: exit code 3 with paste instructions. Automatic mode (`ANTHROPIC_API_KEY`, claude-haiku-4-5) writes the same files. Responses are JSON-Schema validated, max 3 attempts, violation reports written, exchanges hash-logged to `genesis.log.jsonl`.

**Consequences**: byte-identical artifact trail in both modes; canned response files double as test fixtures (replay mode).
