# Security policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please report security problems privately through
[GitHub security advisories](https://github.com/kr0nsch/perenos-ios/security/advisories/new)
rather than in a public issue. Include the Perenos version, what you found and how to
reproduce it. You should get a reply within a week.

## How Perenos handles secrets

- The Apple Account password is passed to the bundled ipatool for a single sign-in request
  and is never stored or logged by Perenos. While that request runs, its command-line
  arguments can be visible to other processes under the same OS user.
- ipatool's session file is encrypted with a random passphrase. On Windows it is protected
  with current-user DPAPI in `%LOCALAPPDATA%\Perenos\ipatool\keychain.key` (or portable
  `Data`). On macOS the passphrase is stored in the current user's login Keychain as
  `Perenos.ipatool.session`, not as a plaintext file.
- Diagnostic logs never intentionally contain passwords, verification codes or passphrases;
  device identifiers are shortened. Review any log before sharing it.
- The bundled ipatool binary is built from an upstream snapshot pinned by full commit and
  SHA-256 (`build/ipatool.json`) with a fixed Go toolchain. macOS requires native CGO.
- Release files are built by GitHub Actions from the tagged commit and carry Sigstore build
  provenance: `gh attestation verify <file> -R kr0nsch/perenos-ios`.
- Windows Authenticode signing turns on when `WINDOWS_SIGNING_CERT` (base64 .pfx) and
  `WINDOWS_SIGNING_CERT_PASSWORD` are configured. Provenance is not an Authenticode
  publisher signature.
- Public macOS packages require a Developer ID Application certificate and Apple
  notarization. The temporary CI keychain is not uploaded as an artifact. Ad-hoc packages
  from CI are for development checks only, not a substitute for notarized releases.

Windows stores application data in `%LOCALAPPDATA%\Perenos`; macOS uses
`~/Library/Application Support/Perenos`. The default saved-app folder is
`Downloads/Perenos`. Existing Windows settings, sessions, pair records and saved apps
migrate automatically from earlier versions without overwriting newer files or moving
custom download folders. The old DPAPI encryption contract is preserved.
