# Development Guide

## Structure
- `engine/` — reusable engine projects.
- `games/` — sample/game applications.
- `assets/` — repository assets.
- `scripts/` — development scripts.
- `.github/skills/` — specialized agent guidance.
- `docs/` — architecture and development rules.

## Milestones
Implement 3D in independent stages. Do not implement future-stage functionality early merely because an abstraction is obvious.

For each stage: inspect current code, read relevant guidance, make the smallest coherent change, build, test and document limitations.

## Stage 05 status
The Silk backend contains real OpenGL resource and indexed-draw commands but still requires a platform-owned current context. Native MAUI surface creation/presentation and a runnable diagnostic scene are pending. Do not report Stage 05 complete or a platform supported until a triangle is visibly rendered on that platform and its lifecycle is validated.

## Public API
Public engine APIs require XML documentation. Prefer stable Orbit concepts over third-party implementation details.

## Dependencies
Before adding a graphics dependency evaluate target frameworks, native dependencies, license, platform coverage, surface integration, shader support and maintenance.

## Commits
Use focused commit messages. Do not mix unrelated formatting or refactoring into milestone changes.

## Editor
The editor is explicitly deferred.
