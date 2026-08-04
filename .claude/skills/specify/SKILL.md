---
name: specify
description: >
  Draft a feature spec for this repo. Triggers on: "write a spec", "spec this feature",
  "specify ...", "draft a spec for ...", "I want to spec ...", "before we build, let's spec".
  Produces specs/<kebab-feature>.md from an interview, stops before writing any code.
argument-hint: <feature>
model: opus
effort: high
---

# /specify — Draft a feature spec

## What this skill does

Interviews you to fill every section of `specs/_template.md`, writes the result to
`specs/<kebab-feature>.md`, then **stops**. No implementation until you approve the spec.

## Steps

1. **Understand the feature area.**
   - Read the source files the feature will touch (use Glob/Grep to locate them).
   - Check `docs/adr/` if it exists for any decisions that constrain the design.
   - Read `specs/_template.md`. If it doesn't exist, create a lean one first (see below).

2. **Interview the user — one question at a time.**
   - Ask one focused question, wait for the answer, then ask the next.
   - Do not fill in gaps yourself or assume. Ask instead.
   - Cover all template sections: context, behaviour/requirements, edge cases,
     out-of-scope, and acceptance criteria concrete enough to test.
   - For this repo, make sure to clarify:
     - Which MCP tool(s) or service layer is affected.
     - Whether SQL changes are involved (Druid constraints apply).
     - Which universe(s) are in scope.
     - Whether field retrieval mode (Taxonomy vs Vector) matters.

3. **Draft the spec.**
   - Write to `specs/<kebab-feature>.md` using the template.
   - Use column IDs (e.g. `customerOrder_orderStatus`), not display names, when
     referencing fields.
   - Acceptance criteria must be concrete and independently testable.

4. **Stop.**
   - Show the spec path and a one-line summary of what was captured.
   - Do NOT write any implementation, scaffolding, or test files until the user
     explicitly approves the spec and asks you to proceed.

---

## If `specs/_template.md` is missing

Create it first using the lean structure below, show it to the user, and stop for
confirmation before continuing the interview.

```markdown
# Spec: <feature name>

## Context
_What is this change and why does it matter? Which part of the system does it touch?_

## Behaviour
_What should the system do after this change? Be specific about inputs, outputs, and
observable behaviour — from the perspective of the LLM agent, MCP client, or end user._

## Edge cases
_What tricky or boundary scenarios must be handled explicitly?_

## Out of scope
_What is explicitly NOT being changed or addressed here?_

## Acceptance criteria
- [ ] _Concrete, independently verifiable condition._
- [ ] _Another condition._
```
