## Conventions

- No code comments; names should explain intent.
- Classes are `sealed` unless designed for inheritance. Prefer primary constructors.
- API contracts are `record`s with `[JsonPropertyName]` in snake_case.
- Logging uses `[LoggerMessage]` source-generated methods.
- Tests: `MethodWhenConditionReturnsResult` naming, Arrange / Act / Assert,
  one behavior per test. Every status code in the spec has at least one test.
- Integration tests must not depend on user secrets: provide fake Twitch
  credentials in the test `WebApplicationFactory`.
- Mappings between layers (Helix → Domain → Contract) use named arguments.
