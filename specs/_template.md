# Spec: <feature name>

## Context
_What is this change and why does it matter? Which part of the system does it touch?
(e.g. MCP tool, SQL pipeline, field retrieval, seeder, chat agent)_

## Behaviour
_What should the system do after this change? Describe inputs, outputs, and observable
behaviour from the perspective of the LLM agent, MCP client, or end user. Reference
column IDs (e.g. `customerOrder_orderStatus`), not display names, when naming fields._

## Edge cases
_Tricky or boundary scenarios that must be handled explicitly. For SQL changes, note
Druid constraints (no CTEs, no UNION, no `SELECT *`, 500-row cap, `TIME_PARSE()` for
dates). For field retrieval changes, note whether Taxonomy and Vector modes both apply._

## Out of scope
_What is explicitly NOT being changed or addressed. Be direct — this prevents scope creep._

## Acceptance criteria
- [ ] _Concrete, independently verifiable condition._
- [ ] _Another condition._
