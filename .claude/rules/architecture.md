# Architecture rules (always-on)

## ADR home

All architecture decisions for this repo live in `docs/adr/`. Files are named
`NNNN-<slug>.md` (zero-padded four-digit sequence). Read the ADRs before proposing any
change that touches the areas they cover.

## When a new ADR is required

Write a new ADR — and stop for review before implementing — when a change involves any of:

- **Breaking an existing ADR's decision** (e.g. changing how `IFieldRetriever` backends are selected, altering the SQL transformation pipeline).
- **Adding or removing an infrastructure dependency** (new database, message broker, external API, etc.).
- **Introducing a new cross-cutting pattern** (new error-handling strategy, new async convention, new serialisation approach).
- **Any decision that is costly or slow to reverse** — if undoing it later would require coordinated changes across multiple projects or a data migration, write it down first.

A one-liner in a PR description is not a substitute. If it's worth doing, it's worth an ADR.
