# Contributing

Thanks for helping improve Perenos (Перенос)!

## Getting started

1. On Windows, clone `https://github.com/kr0nsch/perenos-ios.git` and open the repository folder.
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
3. Run `./build/Get-Ipatool.ps1` once to fetch the pinned ipatool binary.
4. Build and run: `dotnet run --project src/AppBridge -p:Platform=x64`.
5. Run both suites: `dotnet test tests/AppBridge.Core.Tests` and
   `dotnet test tests/AppBridge.ViewModel.Tests`.

Use `--demo` to work on the UI without iPhones or an Apple Account:

```powershell
dotnet run --project src/AppBridge -p:Platform=x64 -- --demo
```

See [development priorities](docs/DEVELOPMENT.md) for completed reliability fixes and
the remaining product work. View-model tests compile the real view-model source with
headless UI adapters; the Windows app build checks the production WinUI integration.

The product was previously named AppBridge. Project folders, namespaces and test
project names retain `AppBridge`, so the build commands above are intentional.
Existing `%LOCALAPPDATA%\AppBridge` and `Downloads\AppBridge` folders also stay
unchanged to preserve settings, sessions and saved apps.

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

- [Report a successful transfer](https://github.com/kr0nsch/perenos-ios/issues/new?template=compatibility_report.yml)
  with your device models, iOS versions and Perenos version. Please never attach `.ipa`
  files, account details or full device identifiers.
- [Report a bug](https://github.com/kr0nsch/perenos-ios/issues/new?template=bug_report.yml)
  with steps to reproduce and a diagnostic log you have checked for personal information.
- [Suggest an improvement](https://github.com/kr0nsch/perenos-ios/issues/new?template=feature_request.yml)
  by describing the task you want to complete and what currently gets in the way.
- Improve instructions, translations or accessibility. For a new language, include every
  resource key and preserve format placeholders such as `{0}`.

## Screenshots, demos and website

Use the [media capture guide](docs/MEDIA.md) to refresh both languages and themes
from the real app in demo mode. Keep personal accounts and device identifiers out
of screenshots. The GIF generator records its source frames and timing, so the
published tours can be reproduced.

The [project website](site/README.md) is built from this repository with
`python site/build.py`. Its GitHub Pages workflow validates pull requests and
publishes changes merged into `main`. Website-only changes need the static build
and browser checks of both themes, mobile layout, keyboard navigation and download
links. They do not require a new application release.

## Releasing

1. Update `CHANGELOG.md`.
2. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`.
3. The release workflow builds the installer and portable ZIP, and publishes them with checksums.
