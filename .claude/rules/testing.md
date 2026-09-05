# Building from a spec — RED-GREEN (YOU MUST)

When implementing a feature that has a spec in `specs/`, work test-first against its
acceptance criteria:

1. Take the next acceptance criterion and write a test that captures it. No implementation yet.
2. Run the suite and **SHOW** the failure — the test must fail because the behaviour is missing,
   not because of a compile error or typo. That is RED.
   ```
   dotnet test tests/reporting.mcp.server.tests/reporting.mcp.server.tests.csproj
   ```
3. Write the minimum code to make that test pass, then run the full suite. Every previously
   passing test must still pass. That is GREEN.
4. Refactor if useful, keeping green, then move to the next criterion.

**NEVER modify a test to make it pass.** If a criterion is ambiguous or a test looks wrong,
STOP and ask before changing anything.
