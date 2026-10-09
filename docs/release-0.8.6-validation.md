# 0.8.6 validation — 2026-10-09

Source/tag: `3f51056eaa3eab3226d61adc9318720b22605b8d`, `v0.8.6`.

- Local Release solution build completed without errors or warnings. The full WPF smoke suite passed.
- Update download regression checks passed: shared download, independent dialog cancellation, reuse, invalidation, deleted files, changed asset URLs, shutdown cancellation and retry after failure. SHA-256 cache checks accept matching bytes and reject modified bytes.
- Release workflow [37883183287](https://github.com/BU5INESSMAN/B5MShot/actions/runs/37883183287) passed, including the new regression checks, signed build, native clean installation, upgrade from running 0.8.5, startup takeover and editor protection on a disposable runner.
- Draft assets downloaded successfully after one interrupted transfer was retried. SHA-256, Authenticode signer `C5475B8C2639D3F402CDBCC2076105974C9CACA9`, file versions and embedded signed MSIX were verified. Embedded app matches the portable app exactly.
- Stable release published. The anonymous latest-release endpoint advertises `v0.8.6` with the installer, portable app and checksum manifest.
- Website rollout retained existing container identities/start times. Backup: `/opt/b5mshot/deploy/downloads/backups/before-0.8.6-20261009-042749`.
- Public landing displays 0.8.6 and health endpoint responds. Both executables fetched through public website URLs from the VPS match the release SHA-256 manifest.
- No local app installation or restart was performed. Installed package and process 1116 remain on 0.8.4 for the user's update test.

Notification display and click behavior have not been manually exercised in the installed desktop app. The new hourly notification/background-download behavior takes effect after the user installs 0.8.6; old versions keep their existing update mechanism until then.

## Release hashes

```text
20e0c31a19556ebfe5245074c792d07fc3bd943165faec87fa25f1c9e80de8bb  B5MShot.exe
9efe1232f8f5ecd47d8aceb109a51de52d5bfeda806914d2b833f88dc9b7986a  B5MShot-Setup.exe
```
