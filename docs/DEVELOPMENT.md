# Development priorities

These are bounded follow-ups from a source review, not release commitments.
The UI issues below are supported by control flow in the code; they have not
been reproduced in a running app or on an iPhone.

## Reliability

### 1. Honor Stop during preparation

Source: [InstallViewModel](../src/AppBridge/ViewModels/InstallViewModel.cs).

- Condition to reproduce: start a transfer with uncached icons and press Stop
  while the icons are still loading.
- Static evidence: `BeginAsync` sets `Running` before awaiting icon reads;
  `Cancel` acts on `_run`, which `RunAsync` creates afterward. The eventual
  transfer therefore gets a fresh token despite the earlier Stop request.
- Expected behavior: create a run token before the first asynchronous step,
  preserve it through preparation and transfer, and finish as canceled.
- Regression test: delay icon loading, request cancellation, then release the
  icon read; assert that no download or installation starts and retry remains
  possible. Also test cancellation after a previous completed run.

### 2. Report unsuccessful sign-out

Source: [AccountViewModel](../src/AppBridge/ViewModels/AccountViewModel.cs).

- Condition to reproduce: make `IAppStoreClient.SignOutAsync` fail or time out
  while an account is signed in, then restart the app.
- Static evidence: `SignOutAsync` catches the exception but still clears the
  account fields and sets `SignedOut`, without confirming session removal.
- Expected behavior: show a useful error and retain the account state until
  sign-out succeeds; handle local session deletion explicitly if introduced.
- Regression tests: a failed revoke must not report successful sign-out;
  a successful revoke must clear the account and remain signed out on refresh.

### 3. Bound activation forwarding

Source: [Program](../src/AppBridge/Program.cs).

- Condition to reproduce: the existing app exits or activation forwarding fails
  between finding the running instance and redirecting to it.
- Static evidence: an exception from `RedirectActivationToAsync` skips
  `SetEvent`, while `CoWaitForMultipleObjects` waits without a timeout.
- Expected behavior: signal completion in `finally`, observe forwarding errors,
  bound the wait, and close the native event handle on every exit path.
- Regression tests: simulate successful forwarding, an exception, and a stalled
  target; each path must finish within a bound and release the handle.

## Product evidence and distribution

### Expand real-device evidence

Publish a short recording of a real transfer, labeled separately from the
existing simulated demos. For each confirmed report, record source and target
models, both iOS versions, AppBridge version, app versions, chosen flow, and
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
