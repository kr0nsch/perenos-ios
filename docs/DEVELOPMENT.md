# Development priorities

## Current architecture

Perenos uses .NET 10 and Avalonia 12 for one Windows and macOS application.
`Perenos.Core` owns USB, App Store and package operations. `Perenos.Presentation`
contains the shared MVVM models, demo backend, localization and platform-neutral
services. `src/Perenos` contains only the Avalonia shell, views and native UI adapters.

The Windows session protection contract remains unchanged: the old DPAPI entropy is
retained so the Perenos rename does not invalidate existing sessions. macOS uses the
login Keychain instead. A custom folder selected by the user is never moved during
migration. Do not replace `LegacyProductFolderName` or the DPAPI entropy as part of
cleanup.

## Verification requirements

Run Core, ViewModel/Platform and Avalonia headless UI suites on Windows and macOS.
CI also builds Windows x64/ARM64 and packages both macOS RIDs with an ad-hoc development signature. Release Mac packages
must be Developer ID-signed and notarized. The Mac release job is skipped until its
credentials are configured; it never silently turns an ad-hoc build into a stable release.

Demo checks must cover transfer, purchase restoration, saving to the library,
installing from the library, theme changes and live language changes. Screenshots
used by the website must come from the current Avalonia demo, not the old WinUI UI.

A successful CI package does not prove USB or Apple Account compatibility. Before a
public macOS release, verify on a physical Mac and iPhone: discovery, trust, app list,
purchase history, downloading, installation, cancellation, Keychain session restore,
and first launch on a clean Mac user account. Record the macOS, iPhone and iOS versions.
Do not attach purchased IPAs, credentials, full UDIDs or private keys.

## Next reliability work

- Expand real-device compatibility reports, including failures and account-region limits.
- Validate the Windows ARM64 package on physical hardware before publishing it.
- Keep the Windows installer migration isolated in tests; do not run migration checks
  against a user's real installation or data.
- Sign Windows binaries when an Authenticode certificate is available. Sigstore
  attestation proves build provenance, not the publisher's Windows identity.
- Keep website screenshots, two languages and platform requirements synchronized
  with the current app. Mac downloads appear only when matching DMGs exist in the
  latest release.

See [CONTRIBUTING.md](../CONTRIBUTING.md) for local builds and
[docs/MEDIA.md](MEDIA.md) for screenshot requirements.
