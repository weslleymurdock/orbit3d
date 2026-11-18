# Orbit3D Agent Instructions

## Project goal
Orbit3D is a fork of Orbit Engine focused on runtime 3D game support while preserving the existing .NET MAUI 2D engine. The first 3D release targets **0.1-rc1** and does not include an editor.

The primary 3D asset importer is **Assimp.MAUI 6.0.5-rc5**:
https://github.com/weslleymurdock/Assimp.MAUI

Assimp is an asset importer, not the Orbit renderer.

## Runtime baseline
- **.NET SDK `11.0.100`** (pinned in `global.json`, `rollForward: disable`) and **.NET MAUI** (`Directory.Build.props`).
- Existing 2D rendering uses `GraphicsView`, `IDrawable` and `ICanvas`.
- Existing 2D behavior must remain functional.
- Do not silently remove an existing MAUI target to make 3D work.
- Do not arbitrarily modify `global.json`, `Directory.Build.props`, target frameworks, or package/workload pins unless a concrete incompatibility is discovered and justified.

## Authoritative development and validation topology
This is the only supported development and validation topology:

| Platform | Workspace & Implementation | Build & App Execution | Appium UI Tests |
|---|---|---|---|
| **Windows** | Windows local VS Code | Windows local | Windows local (`windows`) |
| **Android** | Windows local VS Code | Windows local (device/emulator) | Windows local (`uiautomator2`) |
| **iOS** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`xcuitest`) |
| **Mac Catalyst** | Windows VS Code via Remote - SSH | Remote macOS `27.2` workspace | Remote macOS `27.2` workspace (`mac2`) |

### Decision process for tasks
- **Android / Windows tasks**: Work in the Windows-local VS Code workspace. Implement, build, execute on real Windows/Android targets, run Appium UI tests, and inspect screenshots/logs/diagnostics locally.
- **iOS / Mac Catalyst tasks**: Connect from Windows VS Code to the provisioned **macOS `27.2`** host via **VS Code Remote - SSH**, open the repository on the remote macOS filesystem, and run the agent inside that remote workspace. Implement, build with `.NET SDK 11.0.100` and the installed Apple toolchain (confirm exact Xcode/workload versions on the remote host as needed), execute on iOS Simulator / Mac Catalyst, run Appium UI tests, and inspect screenshots/logs/diagnostics directly on macOS.
- **Cross-platform changes**:
  1. Implement and validate Android and Windows in the Windows-local workspace.
  2. Connect to the remote macOS workspace via VS Code Remote - SSH and implement and validate iOS and Mac Catalyst there.
  3. Ensure both workspaces contain the same intended source changes and are synchronized through the normal Git workflow.
  4. Report actual platform execution evidence separately for each platform.

### Prohibited orchestration workarounds
Do **not** implement or use any Windows-to-macOS build orchestration layer. Agents must never:
- Copy the working tree to a temporary macOS staging directory.
- Package the repository into TAR archives or use SCP to transfer source archives.
- Execute remote Apple build/validation scripts through standalone SSH commands.
- Download Apple or CI build artifacts to validate them on Windows.
- Use password-based or key-based SSH automation scripts as a replacement for VS Code Remote - SSH.

## CI policy (helper only, never validation authority)
GitHub Actions CI is optional supporting infrastructure for builds, diagnostics, packaging, and supplementary checks:
- CI is **never** the authoritative validation mechanism for an implementation stage.
- A successful CI workflow does not prove that a platform implementation works; a failed CI workflow does not automatically invalidate a locally/remotely validated implementation.
- Agents must not use CI results as a substitute for executing applications on their actual target platforms.
- Agents must not wait for CI to validate implementation changes.
- CI status must not be used as a Stage Definition of Done requirement.
- When a target cannot be executed due to a missing device, unavailable simulator, or environment limitation, report it explicitly as **blocked** or **not executed** — never substitute a CI result.

## Architecture rules
1. Keep Orbit-facing 3D types independent from Assimp types.
2. Use `System.Numerics` for math where practical.
3. Separate game/runtime model, asset import, graphics abstraction and platform backend.
4. Game code must not directly depend on OpenGL, Metal, Direct3D or Vulkan.
5. Platform graphics APIs belong behind renderer/device abstractions.
6. Do not use `ICanvas` as the primary 3D rasterization pipeline.
7. Do not copy vertices, indices or native asset data every frame.
8. GPU resources require explicit ownership and disposal.
9. Public Orbit APIs must not expose SWIG/native Assimp objects.
10. Preserve the existing 2D API unless a breaking change is explicitly required.

## 0.1-rc1 scope
Required: transforms, scene hierarchy, cameras, meshes, materials, textures, basic lights, Assimp importing, GPU renderer abstraction, accelerated renderer path, MAUI 3D surface, depth/culling/indexed drawing/textures/basic lighting, caching, metrics, tests and a runnable sample.

Out of scope: editor, advanced PBR authoring, advanced shadows/post-processing, 3D physics, ECS rewrite and a full skeletal animation system unless required by a concrete milestone (Stage 11 introduces runtime skeletal/skinning animation).

## Animation implementation tasks (Stage 11+)
- Use `assets/Idle.fbx` (the existing Mixamo animated/skinned FBX model) as the mandatory animation asset. Do not replace it with another model or synthetic skeleton/animation.
- Keep `games/3D/AnimationSample/` (`com.remotecodehub.animationsample`) independent from `games/3D/SilkTriangleSample/`.
- Keep `games/3D/AnimationSample.UITests/` independent from `games/3D/SilkTriangleSample.UITests/`.
- Validate actual animation behavior on all four platforms while preserving the existing static rendering baseline.

## Backend rule
Compare OpenGL/OpenGL ES, Metal, Direct3D, Vulkan and established cross-platform .NET graphics abstractions before selecting the implementation. Evaluate platform coverage, MAUI surface integration, shader model, maintenance and resource lifetime.

The renderer API must allow platform-specific implementations without leaking backend types into game code.

## Assimp rule
Read the actual Assimp.MAUI API before integration. Do not infer wrapper names from desktop Assimp documentation. Convert Assimp Scene/Node/Mesh/Material/Texture data into Orbit-owned data and release native resources at the correct boundary.

## Performance
- No per-frame Assimp traversal.
- No per-frame vertex marshaling.
- No GPU-to-CPU readback in normal rendering.
- Avoid allocations in Update/Render.
- Upload immutable mesh data once.
- Cache imported assets and GPU resources.
- Measure before adding batching.
- Track frame time/FPS and draw-call/triangle counts when instrumentation exists.

## Lifecycle
Respect MAUI UI-thread requirements and graphics-context requirements. Handle view creation, resize, suspension, recreation and disposal without leaking native or GPU resources.

## Code style
Follow existing repository style. Public APIs require XML documentation. Prefer focused types and explicit ownership. Avoid speculative abstractions and unrelated refactors. Do not add editor dependencies during runtime milestones.

## Validation
For every stage:
1. Build affected projects in the appropriate workspace (Windows local for Windows/Android; VS Code Remote - SSH on macOS `27.2` for iOS/Mac Catalyst).
2. Run relevant unit and UI automation tests against real application builds.
3. Validate 2D when renderer/runtime changes.
4. Validate real MAUI targets when platform code changes and report execution evidence per platform.
5. Report blocked or unexecuted platforms explicitly without substituting CI results.

## Git operations and remote changes
- Do not create commits or push changes by default. Keep implementation changes in the working tree unless the task explicitly authorizes a commit/push or the requested deliverable cannot be completed without publishing the changes.
- A task explicitly requiring a GitHub Actions CI build, workflow dispatch, or CI-produced artifacts authorizes the minimum necessary commit and push of the relevant, validated changes to the intended branch when those changes must be present on GitHub for the workflow to run. This authorization is limited to that task and its required CI inputs; it is not blanket permission to publish unrelated or unfinished work.
- When the user explicitly requests a commit, push, release, or other remote write, perform only the requested remote operation and its necessary prerequisites.
- Before committing, inspect the current branch, working-tree status, staged diff, and recent history. Stage only files relevant to the task and use a descriptive commit message.
- Before pushing, verify the destination remote and branch. Never force-push, rewrite shared history, or push to a different branch unless the user explicitly requests it.
- If a required remote operation fails because of authentication, branch protection, permissions, conflicts, or network errors, preserve local changes and report the exact blocker and recovery action. Do not claim the remote operation succeeded.
