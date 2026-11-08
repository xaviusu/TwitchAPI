# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core (.NET 10) REST API for a technical challenge ("Twitch Analytics"). The full spec is in `docs/enunciado.md` (Spanish) and is the source of truth. It defines two endpoints that proxy the public Twitch Helix API:

- `GET /analytics/user?id=<id>` returns one Twitch user. Errors: 400 (missing or invalid id), 401 (token invalid or expired), 404 (user not found), 500.
- `GET /analytics/streams` returns live streams as `[{ "title": ..., "user_name": ... }]`. Errors: 401, 500.

Requirements from the spec that are easy to break:
- **The spec wins.** Response bodies match it exactly, including field names (snake_case such as `display_name`, `broadcaster_type`, `user_name`) and format. Error bodies are `{ "error": "<exact message from spec>" }`. Never add status codes, response fields or error messages that are not in `docs/enunciado.md`.
- Client ID and secret come from .NET user secrets. Never write them to `appsettings*.json`.
- The README (Spanish) must keep documenting how to run the app, technical decisions and trade-offs, what was left out, and any assumptions. Update it when behavior changes.

## Commands

Run from the repo root. The SDK is pinned by `global.json` (10.0.403, latestFeature roll-forward, no prerelease).

```powershell
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~OpenApiTests"   # single class or test
dotnet format --verify-no-changes                        # style check (CI-style)
dotnet run --project .\src\TwitchAPI.Api --launch-profile https
```

In Development, OpenAPI is served at `/openapi/v1.json`.

## Architecture

The solution file is `TwitchAPI.slnx`. It follows Clean Architecture with this dependency direction:

- `Api` → `Application` → `Domain`
- `Infrastructure` → `Application`. Infrastructure implements the contracts (interfaces) that Application defines, such as the Twitch HTTP client and the token provider.
- `Api` → `Infrastructure` **only at the composition root** (`Program.cs`, for DI registration). Controllers must not use concrete Infrastructure types.

Endpoints use MVC **controllers** (in `src/TwitchAPI.Api/Controllers/`), not Minimal APIs. `Program.cs` ends with `public partial class Program { }` so that `WebApplicationFactory<Program>` works in integration tests.

The README rules out these on purpose. Don't add them without a requirement: EF Core or any persistence, CQRS, MediatR, AutoMapper, auth for the API itself, containers, and empty placeholder interfaces or services.

## Build rules

Analyzers and code style live in `Directory.Build.props` and `.editorconfig`; violations are build warnings. The Stop hook (`.claude/hooks/verify.ps1`) and CI build with `-warnaserror`, run `dotnet format --verify-no-changes` and `scripts/check-final-newline.ps1`.

## Tests

`tests/TwitchAPI.Tests` uses xUnit and `Microsoft.AspNetCore.Mvc.Testing`. Integration tests use `WebApplicationFactory<Program>` with base address `https://localhost`, because HTTPS redirection is enabled. Tests must not call the real Twitch API. Stub the Application-layer contracts or the `HttpMessageHandler` instead.

## Working rules

- If the spec is ambiguous, ask before implementing. Once decided, record the
  assumption in the README section "Dudas e hipótesis".
- For any non-trivial change, propose a plan first and wait for approval,
  unless the work comes from a `ready-for-agent` issue.
- Write or update tests before the implementation.
- Keep changes small and focused; one concern per commit.
  - Never commit, push or change git history unless asked. `/implement` counts as asking to commit, after a checkpoint: show the full review reports and wait for the user to choose fixes.
  - For .NET / ASP.NET Core APIs, analyzers or Twitch-unrelated framework behavior,
  check the `microsoft-learn` MCP before relying on memory.

Coding standards (enforced in review): see `CODING_STANDARDS.md`.

## Definition of done

A task is done only when all of these pass:
1. `dotnet build` with no new warnings.
2. `dotnet test` all green.
3. `dotnet format --verify-no-changes` clean.
4. README updated if behavior, setup or assumptions changed.

Before delivery, run the spec-checker subagent against the whole docs/enunciado.md;
/code-review only checks each ticket's diff.

## Agent skills

### Issue tracker

Issues live in GitHub Issues (xaviusu/TwitchAPI), managed with the `gh` CLI. Commands, the branch workflow and closing issues by hand: see `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root, created lazily. See `docs/agents/domain.md`.
