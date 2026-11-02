# Development priorities

## Reliability fixes in 1.1.1

The three issues from the source review are fixed and covered by regression tests:

- **Stop during preparation.** Cancellation applies while icons load and while apps
  transfer. Unfinished apps remain available for retry with a fresh cancellation token.
  Source: [InstallViewModel](../src/AppBridge/ViewModels/InstallViewModel.cs).
- **Unsuccessful sign-out.** A failed revoke retains the signed-in account and displays
  a localized error with a retry action. Successful sign-out clears account details
  and form secrets; account operations cannot overlap.
  Source: [AccountViewModel](../src/AppBridge/ViewModels/AccountViewModel.cs).
- **Activation forwarding.** A second launch waits for a bounded period, observes
  forwarding failures and releases the event handle even if the target stalls.
  Source: [ActivationForwarder](../src/AppBridge/Services/ActivationForwarder.cs).

[View-model tests](../tests/AppBridge.ViewModel.Tests) compile the production view
models with deterministic headless UI adapters. [Core tests](../tests/AppBridge.Core.Tests)
cover activation forwarding, saved-app storage and the transfer pipeline. Both suites
run in CI and before release packaging. These tests do not replace real-device USB
checks or visual checks of WinUI screens.

## Product evidence and distribution

### Expand real-device evidence

Publish a short recording of a real transfer, labeled separately from the
existing simulated demos. For each confirmed report, record source and target
models, both iOS versions, Perenos version, app versions, chosen flow, and
outcome. Include failures and known limits so the compatibility matrix helps
users decide whether to try their own devices. Share no account credentials,
verification codes, full device identifiers, or purchased IPA files.

### Validate and publish native ARM64 packages

[CI](../.github/workflows/ci.yml) builds the ARM64 app, and the
[build script](../build/build.ps1) accepts `win-arm64`; the current
[release workflow](../.github/workflows/release.yml) uses the default x64-only
packaging. Before adding ARM64 release artifacts, validate the bundled ipatool,
USB communication, installation, portable storage, and installer on a Windows
ARM64 PC. Then publish architecture-specific downloads, checksums, and
provenance attestations, and update the download instructions.
