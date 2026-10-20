# Apple Simulator UI Testing Guideline

## Decision

Orbit3D must validate Apple-platform implementation changes on a simulator hosted by a GitHub Actions macOS runner before requiring validation on physical devices or paid device-cloud services.

For iOS, the default pre-device validation path is:

1. Use a GitHub-hosted macOS runner with a compatible Xcode installation.
2. Select the .NET SDK and workload configuration pinned by the repository.
3. Build the real MAUI application for an iOS Simulator target.
4. Boot an available iOS Simulator on that runner.
5. Install and launch the built application in the simulator.
6. Run automated UI tests against the running application.
7. Publish test results and useful diagnostics as GitHub Actions artifacts and/or job summaries.

The simulator tests are the first automated end-to-end validation gate for Apple implementation work. Physical-device validation remains a later gate for hardware-specific behavior, real-device performance, and final compatibility checks. BrowserStack, Firebase Test Lab, or other device-cloud services are not prerequisites for the initial implementation-validation loop.

## Scope and intent

The simulator must run the actual application build and actual platform rendering implementation. Tests must not substitute a mock renderer, bypass native surface initialization, or report success using test-only values that do not reflect runtime state.

For Orbit3D graphics changes, tests should progressively verify:

- The application installs and launches.
- The sample UI is displayed and remains responsive.
- The native graphics surface is created.
- The configured graphics backend initializes successfully.
- The sample scene and its real assets load.
- Rendering makes observable progress (for example, frame metrics advance) where the application exposes a reliable diagnostic contract.
- Lifecycle transitions such as background/foreground and surface recreation do not leave the renderer unusable.
- Screenshots or other visual evidence are captured for relevant rendering scenarios when feasible.

A passing UI test is evidence only for the assertions it actually makes. App launch alone does not prove that the graphics backend initialized or that a correct frame was rendered.

## Toolchain and version policy

- The repository root `global.json` and `Directory.Build.props` are authoritative for the SDK, workloads, and pinned Apple package versions.
- Do not change repository pins, silently install a different Apple workload release, or upgrade package versions merely to make a hosted runner pass.
- The workflow must print diagnostic version information for .NET, workloads, Xcode, and available simulator runtimes.
- The workflow must fail with an actionable compatibility message when the hosted runner cannot satisfy the pinned toolchain. Treat this as an environment/toolchain blocker, not as permission to rewrite version pins.
- Select a simulator runtime and device type explicitly from the runtimes available on the runner; do not assume a particular simulator is installed without checking.
- Keep the app's iOS deployment target and simulator target compatible with the existing project configuration.

The repository currently pins .NET SDK `10.0.100` in `global.json` and Apple SDK package release `26.5.10318` in `Directory.Build.props`. These values must be re-read from the repository when implementing or updating the workflow rather than copied blindly into new configuration. Xcode compatibility must be checked against the pinned toolchain and the actual hosted runner image.

## UI automation requirements

- UI automation must exercise the application installed in the simulator, not only unit-test engine classes.
- Keep backend-independent unit and integration tests in their existing test projects; simulator UI tests cover the app/platform integration boundary.
- Use stable, intentional UI automation identifiers or another documented accessibility contract rather than brittle positional selectors or localized display strings where possible.
- Wait for observable application readiness conditions; avoid fixed sleeps as the primary synchronization mechanism.
- Preserve logs and failure diagnostics even when a test fails.
- Make the simulator UI test suite deterministic and suitable for repeated CI runs.
- Do not treat simulator results as proof of physical-device GPU performance or complete hardware compatibility.

The concrete test framework and automation driver must be chosen to fit the MAUI application and the CI execution model. If using Appium, the workflow must explicitly install/configure a compatible Appium server and XCUITest driver, start the simulator, build/install the simulator-targeted app, and ensure the test runner connects to that local Appium server. Do not assume an iOS device-targeted `.ipa` is interchangeable with a simulator build.

## CI behavior

The Apple simulator validation should be a distinct, understandable CI job/workflow so that:

- It can be run for pull requests and manually.
- Its outcome is visible independently from Android and Windows validation.
- Failures retain test results, simulator/application logs, and screenshots where available.
- Simulator/device/runtime selection and toolchain versions are visible in the job log.
- The suite can later be extended without requiring a physical device or third-party device-cloud account.

The job should build the relevant solution/project first, then execute the UI test project using a documented command. The repository's solution and project structure should remain the source of truth; avoid duplicating build logic or maintaining a second, divergent app configuration solely for UI tests.

## Validation stages

1. **Hosted simulator gate:** compile the actual iOS Simulator target and run app-level UI smoke tests on GitHub Actions macOS.
2. **Expanded simulator gate:** validate renderer startup, real sample assets, observable frame progress, and lifecycle behavior; add visual comparisons where stable.
3. **Physical-device/cloud gate:** validate device-specific behavior, real GPU performance, and compatibility on selected physical iOS devices.

A failure in stage 1 or 2 should be diagnosed and corrected before spending time or quota on stage 3, except when the failure itself requires physical hardware to investigate.

## References

- [GitHub Actions: Building and testing Xcode projects](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-xcode)
- [Apple: Testing in Xcode](https://developer.apple.com/documentation/xcode/testing)
- [Appium XCUITest Driver](https://appium.github.io/appium-xcuitest-driver/)
