# MAUI Graphics Surface Skill

## Use when
Integrating a native accelerated graphics surface with .NET MAUI on Android, iOS, MacCatalyst or Windows.

## Required reading
- `AGENTS.md`
- `docs/architecture.md`
- `docs/rendering.md`

## Rules
- Keep platform-specific code behind the renderer/surface abstraction.
- Respect MAUI lifecycle and UI-thread rules.
- Handle surface creation, resize, suspension, recreation and disposal.
- Do not assume a mobile graphics context survives suspension.
- Avoid blocking the UI thread with asset import or shader compilation.
- Preserve the existing `GameSceneView` 2D path.
- Validate each platform separately rather than assuming equivalent native behavior.
