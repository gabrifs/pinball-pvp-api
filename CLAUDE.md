# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

PinballPVP.Api is an ASP.NET Core Web API (.NET 10) backing a Unity head-to-head pinball game. It exposes
REST endpoints for user accounts/auth, solo matches (vs CPU), versus matches (P2P player vs player), and
aggregated player records, persisting to PostgreSQL via EF Core.

**This project serves a dual purpose: it's both a portfolio piece and the backend for a commercial game.**
That raises the bar on code quality, security, and production-readiness beyond what a typical hobby/learning
project would need — treat shortcuts and "good enough for now" choices with extra scrutiny, since both a
prospective employer/client and real paying players may end up depending on this code.

See [TODO.md](TODO.md) for the production-readiness roadmap (security hardening, observability, testing, etc.).

## Directives to always follow

- Strive to follow S.O.L.I.D. principles.
- Keep code clean and comprehensible — favor the existing conventions in this codebase over introducing new patterns.
- Always use the modern Gold Standard on all features (current language/framework idioms and best practices — e.g. .NET 10 / C# latest, EF Core current APIs — rather than outdated or legacy approaches).
- Update this file (CLAUDE.md) whenever a change is made that's worth documenting here — new architecture, conventions, gotchas, etc.
- Always update README.md when a change might affect what it documents (features, setup steps, API surface, project structure).
- Always update TODO.md when working on items related to it — remove items once they're completed (rather
  than leaving them checked off; TODO.md is a roadmap of what's left, and git history already documents
  what was done and why), and add newly-discovered items.
- **Never write secrets or sensitive data into any tracked file** — connection strings, signing/API keys,
  passwords, tokens, certificates, etc. must only ever live in `dotnet user-secrets` (local dev) or
  environment variables/a secrets manager (other environments); see [.claude/Contexts/auth.md](.claude/Contexts/auth.md) and the
  `user-secrets` note under [EF Core migrations](.claude/Commands.md#ef-core-migrations). Once committed, a secret is in git
  history permanently (even if later removed from the working tree) — treat any value that reaches a commit
  as compromised and rotate it rather than relying on a follow-up commit to "remove" it.
- When a feature area grows enough conventions to need documenting, add a new file under `.claude/Contexts/`
  rather than growing this file inline — see [Feature-specific conventions](.claude/Architecture.md#feature-specific-conventions-claudecontexts)
  below for the existing examples and the rationale.
- Whenever you work on a feature, keep its file under `.claude/Contexts/` up to date — document what it does,
  key decisions, and gotchas as part of the same change, so a future session can refresh context on that area
  without re-deriving it from the diff.

## Commands

See [.claude/Commands.md](.claude/Commands.md) for build/run/test commands, CI/CD, Docker, and EF Core migrations.

## Architecture

See [.claude/Architecture.md](.claude/Architecture.md) for layering and the index of feature-specific
convention files under `.claude/Contexts/`.
