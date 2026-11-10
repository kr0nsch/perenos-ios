# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please report security problems privately through
[GitHub security advisories](https://github.com/kr0nsch/perenos-ios/security/advisories/new)
rather than in a public issue. Include the Perenos version, what you found, and how to
reproduce it. You should get a reply within a week.

## How Perenos handles secrets

- The Apple Account password is passed to the bundled ipatool for a single sign-in request
  and is never stored or logged by Perenos. While that request runs, it is visible on
  ipatool's command line to other programs running under the same Windows user.
- ipatool's session file is encrypted with a random passphrase. The passphrase is stored
  with Windows DPAPI (current-user scope) in `%LOCALAPPDATA%\Perenos\ipatool\keychain.key`
  (or the portable `Data` folder).
- Diagnostic logs never contain passwords, verification codes or passphrases, and device
  identifiers are shortened.
- The bundled ipatool binary is built from an upstream source snapshot pinned by full commit
  and SHA-256 checksum (`build/ipatool.json`) with a fixed Go toolchain during every build.
- Release files are built by GitHub Actions from the tagged commit and carry a Sigstore build
  provenance attestation: `gh attestation verify <file> -R kr0nsch/perenos-ios`.
- Authenticode signing of the application binaries and installer turns on automatically
  when the repository secrets `WINDOWS_SIGNING_CERT` (base64 .pfx) and
  `WINDOWS_SIGNING_CERT_PASSWORD` are set.

Perenos stores installed data in `%LOCALAPPDATA%\Perenos`, and its default saved-app
folder is `Downloads\Perenos`. Existing settings, sessions, pair records and saved apps
migrate automatically from earlier versions without overwriting newer files or moving
custom download folders. Portable copies continue to use their existing `Data` folder.
The DPAPI encryption contract remains unchanged so existing sessions stay readable.
