## Conventions

- No code comments; names should explain intent.
- Classes are `sealed` unless designed for inheritance. Prefer primary constructors.
- API contracts are `record`s with `[JsonPropertyName]` in snake_case.
- Logging uses `[LoggerMessage]` source-generated methods.
- Tests: `MethodWhenConditionReturnsResult` naming, Arrange / Act / Assert.
- One behavior per test: one scenario and the assertions describing its
  observable result (status, body and calls to the fake Twitch count as one
  behavior).
- Every status code in the spec has at least one test. Behavior shared by both
  endpoints (token lifecycle, 401, 500) is tested on both, with a Theory over
  the endpoints.
- Fake Twitch payloads match the case they model (a status in the body equals
  the response status), or are status-neutral.
- Integration tests must not depend on user secrets: provide fake Twitch
  credentials in the test `WebApplicationFactory`.
- Mappings between layers (Helix → Domain → Contract) use named arguments.
