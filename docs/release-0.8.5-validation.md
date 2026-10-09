# 0.8.5 validation — 2026-10-09

Source/tag: `fc6677027bed4e2c4d0f4053da34c115b0030f4d`, `v0.8.5`.

- Local Release solution build: no errors or warnings. Full WPF smoke suite, installer workflow mocks and autostart migration checks passed.
- Release workflow [37881920931](https://github.com/BU5INESSMAN/B5MShot/actions/runs/37881920931) passed, including signed build, real clean native installation, upgrade from running 0.8.4, startup takeover and editor protection on a disposable runner.
- Downloaded draft assets passed SHA-256, Authenticode signer `C5475B8C2639D3F402CDBCC2076105974C9CACA9`, file version and embedded MSIX verification. Embedded app matches the portable app exactly.
- Stable release published on GitHub. The anonymous `/releases/latest` endpoint advertises `v0.8.5`, both executables and `SHA256SUMS.txt`.
- Website rollout replaced downloads and landing HTML while retaining existing container identities/start times. Backup: `/opt/b5mshot/deploy/downloads/backups/before-0.8.5-20261009-040504`.
- Public landing displays 0.8.5 and health endpoint responds. Both publicly downloaded executables match the release SHA-256 manifest.
- At the user's request, no local installation or application restart was performed. Installed package and running process remain on 0.8.4 for the user's in-app update test.

## Release hashes

```text
ff66cb76e4e758aebcd092dc8d482fa315cd4af6ac4230ebd86bd01a0883b897  B5MShot.exe
62fa50eed137b11afecd7b090a4f78f3f30b9dbb8311f5b2d3dad0a30b256356  B5MShot-Setup.exe
```
