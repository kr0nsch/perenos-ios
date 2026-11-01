# Contributing

Thanks for helping improve AppBridge!

## Getting started

1. On Windows, clone `https://github.com/kr0nsch/AppBridge.git` and open the repository folder.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
3. Run `./build/Get-Ipatool.ps1` once to fetch the pinned ipatool binary.
4. Build and run: `dotnet run --project src/AppBridge -p:Platform=x64`.
5. Run the tests: `dotnet test tests/AppBridge.Core.Tests`.

Use `--demo` to work on the UI without iPhones or an Apple Account:

```powershell
dotnet run --project src/AppBridge -p:Platform=x64 -- --demo
```

See [development priorities](docs/DEVELOPMENT.md) for concrete reliability work and
the regression scenarios each change should cover.

## Guidelines

- Keep the flow simple: four steps, one primary button per screen.
- Every user-visible string goes into `src/AppBridge/Strings/en-US/Resources.resw` **and**
  `ru-RU/Resources.resw`. A unit test checks that both files have the same keys and placeholders.
- Keep `README.md` (Russian, the main page) and `README.en.md` (English) in sync.
  `README.ru.md` is a compatibility link for existing bookmarks.
- Never log or persist passwords, verification codes or passphrases.
- Device and App Store code lives in `AppBridge.Core` behind `IDeviceService`,
  `IDeviceWatcher` and `IAppStoreClient`, so it can be tested with fakes.
- Run the tests before opening a pull request. If you change USB code, please say which
  iPhone models and iOS versions you tried.

## Help without writing code

- [Report a successful transfer](https://github.com/kr0nsch/AppBridge/issues/new?template=compatibility_report.yml)
  with your device models, iOS versions and AppBridge version. Please never attach `.ipa`
  files, account details or full device identifiers.
- [Report a bug](https://github.com/kr0nsch/AppBridge/issues/new?template=bug_report.yml)
  with steps to reproduce and a diagnostic log you have checked for personal information.
- [Suggest an improvement](https://github.com/kr0nsch/AppBridge/issues/new?template=feature_request.yml)
  by describing the task you want to complete and what currently gets in the way.
- Improve instructions, translations or accessibility. For a new language, include every
  resource key and preserve format placeholders such as `{0}`.

## Releasing

1. Update `CHANGELOG.md`.
2. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`.
3. The release workflow builds the installer and portable ZIP, and publishes them with checksums.
