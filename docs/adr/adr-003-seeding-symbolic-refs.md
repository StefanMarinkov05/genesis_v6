# ADR-003: Seeding uses symbolic refs, DB-assigned int keys

**Status**: accepted

**Context**: SQL Server identity columns assign int PKs at insert; an LLM inventing numeric FK values is guaranteed drift.

**Decision**: seed rows carry `refKey` (parents) / `parentRef_{Target}` (children) strings. Emitted `SeedData.cs` inserts parents first, captures real IDs in `Dictionary<string,int>`, resolves children. Int PKs everywhere; ApplicationUser stays on the Identity default key.

**Consequences**: seed data is portable across databases and re-runs; the seed JSON schema makes refs mandatory so the LLM cannot omit them.
