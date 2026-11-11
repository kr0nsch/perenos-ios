# Contributing

Thanks for helping improve Perenos (Перенос)!

## Getting started on Windows

1. Clone `https://github.com/kr0nsch/perenos-ios.git`.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Go 1.26.5.
3. Run `./build/Get-Ipatool.ps1` once to build the checksum-verified ipatool snapshot.
4. Run `dotnet run --project src/Perenos` or `dotnet run --project src/Perenos -- --demo`.
5. Run all suites: `dotnet test Perenos.sln`, including real Avalonia headless UI tests.

The app uses **Avalonia 12 and .NET 10**, not Windows App SDK. `Perenos.Core` contains
USB/App Store logic, `Perenos.Presentation` contains shared MVVM and localization,
and `src/Perenos` contains the cross-platform Avalonia UI.

## Getting started on macOS

Use macOS 15 or newer with Xcode Command Line Tools, .NET 10 SDK, Go 1.26.5 and PowerShell 7:

```bash
brew install dotnet go
brew install --cask powershell
bash build/Get-Ipatool.ps1 -RuntimeIdentifier osx-arm64
dotnet run --project src/Perenos -- --demo
```

On an Intel Mac use `osx-x64`. CGO is enabled on macOS because ipatool's Apple
integration requires it; it is not cross-compiled from Windows. Build a local DMG with
`bash build/build-macos.sh 1.7.2 osx-arm64`. The script makes an ad-hoc signed package
for development unless `MACOS_SIGN_IDENTITY` and `MACOS_NOTARY_PROFILE` are set.
A public release additionally requires a valid iPhone check on a physical Mac.

## Guidelines

- Keep the flow simple: a visible task, a clear current step and one primary action.
- Every user-visible string belongs in `src/Perenos.Presentation/Strings/Resources.resx`
  **and** `Resources.ru.resx`. Tests check resource parity and Avalonia template keys.
- Keep `README.md` (Russian), `README.en.md` (English) and the two website pages in sync.
  `README.ru.md` is a compatibility link for bookmarks.
- Never log or persist passwords, verification codes or passphrases.
- Device and App Store operations stay in `Perenos.Core` behind interfaces. UI-specific
  code belongs in `src/Perenos`, not in the shared view models.
- Do not change the old DPAPI entropy or legacy migration identifiers during cleanup.
- Run tests before opening a pull request. If you change USB code, report which physical
  iPhone, computer, OS and iOS versions you used.

## Help without writing code

- [Report a successful transfer](https://github.com/kr0nsch/perenos-ios/issues/new?template=compatibility_report.yml)
  with device models, OS versions and Perenos version. Never attach IPAs, account details
  or full device identifiers.
- [Report a bug](https://github.com/kr0nsch/perenos-ios/issues/new?template=bug_report.yml)
  with reproduction steps and a log you have checked for personal information.
- [Suggest an improvement](https://github.com/kr0nsch/perenos-ios/issues/new?template=feature_request.yml)
  by describing the task and the obstacle.
- Improve instructions, translations or accessibility. Include every resource key and
  preserve numeric placeholders such as `{0}`.

## Screenshots, demos and website

Use [docs/MEDIA.md](MEDIA.md) for media requirements. All screenshots must come from
the current Avalonia demo, without real account information or device identifiers.
Keep the same fictional data and both themes where possible.

The [project website](site/README.md) has Russian and English pages. It is built with
`python site/build.py`; the Pages workflow validates pull requests and deploys `main`.
Run `npm ci && npm run test:site` for browser, responsive-layout, keyboard, theme,
localization and download-link checks. Website-only changes do not need a new app release.

## Releasing

1. Update `CHANGELOG.md` and version references.
2. Ensure all three .NET suites, the Windows release build, macOS CI and browser checks pass.
3. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`.
4. The release workflow builds Windows and macOS packages and publishes them with checksums and
   Sigstore attestations. Without Apple signing credentials, macOS packages are explicitly named
   `-test.dmg` and described as unnotarized development builds. When the required secrets are configured
   and `vars.MACOS_SIGNING_ENABLED` is `true`, regular filenames are reserved for Developer ID-signed,
   notarized DMGs. A stable Mac release additionally requires a physical-device check.
