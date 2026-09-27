# 0.8.0 release verification

Published tag: `v0.8.0`, application source commit `e025e74b75bd0d443f488d601d301303dbf1eb4a`.
Signed installer workflow run: https://github.com/BU5INESSMAN/B5MShot/actions/runs/36333030737

## Verified

- WPF smoke suite: all four edges, pointer hit testing inside extended palettes,
  small-window bounds, explicit monitor selection, settings serialization/fallback,
  unchanged screenshot exports, animation/reversal, reduced-motion reopening.
- Update parsing prefers the installer, ignores prereleases/drafts, normalizes
  version comparisons, rejects unrelated/insecure URLs and malformed checksums.
- Live GitHub update check detects 0.8.0 from 0.6.0 and does not offer an update
  to 0.8.0. The real download/verifier downloaded the published installer and
  verified its SHA-256 without launching installation.
- `tools/verify-release.ps1` checked both EXE hashes, Authenticode signatures,
  version resources, embedded MSIX identity, presence of signed package/Explorer
  extension and byte-identical embedded vs portable app.
- Website smoke suite passed at 1440, 768, 390, 375 and landscape 844 × 390.

Still requires user acceptance: actual installation/UAC and Explorer registration
on a clean Windows account; diverse mixed-DPI monitor arrangements and fullscreen games.
The active user installation was not closed or replaced during tests.

## Release identity

Portable SHA-256: `647cf764ebdf7180e90edb877ef9028099301d06d77281365b4b6e5cf3950c78`

Installer SHA-256: `ceb4030b27f940ab73c5cbb3c4bd9c4fd9a5092f95bb51bb0ea9c7dc352ce86f`

The existing test-signing identity remains in use. CI stores its password-protected
PFX only in encrypted repository secrets; no private-key file was committed.
Future builds must use a new release tag. Never overwrite a published verified asset.

## VPS rollout

New read-only service `b5mshot-ui-v8`; old UI kept running as rollback standby.
Uploads/storage stay on the unchanged `b5mshot-server`. All pre-existing container
IDs/start times remained unchanged. Only Caddy's configuration was gracefully reloaded.

Old downloads: `/opt/b5mshot/deploy/downloads/backups/before-0.8.0-20260927-162941`.
Caddy backup: `/opt/media-stack/config/caddy/Caddyfile.before-0.8.0-20260927-162941`.
`deploy/rollout-0.8.py` validates exact release checksums and preserves rollback copies.
