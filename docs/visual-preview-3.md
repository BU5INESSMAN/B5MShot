# B5MShot 0.7.0-preview.3 — unified visual system

Graphite glass application surfaces, white text, blue action emphasis, consistent
rounded corners and interruptible spring feedback. Light frosted web surfaces use
the same logo and blue accent. UI/UX Pro Max guided motion/interaction choices;
Frontend Design guided website hierarchy, typography and real-product presentation.

## Windows app

- WPF tray menu replaces the native context menu; supports arrows, Escape and
  keyboard focus. Actions close it before capture. It never appears on the taskbar.
- Compact settings window opens near the cursor's monitor taskbar/work area.
  Custom hotkeys, file import, startup switch, updates and support remain available.
- Shared Glass.xaml resources style the settings, tray and update dialog.
- Reduced-motion reopening restores opacity explicitly. Capture also dismisses
  any open tray menu before flushing composition and copying pixels.
- Master logo: `src/B5MShot.App/Assets/logo.svg`. `tools/build-brand.cjs` generates
  app ICO/PNG, package tiles and website/social assets using Sharp.
- Portable build: `release/preview-0.7.0-3/B5MShot.exe`. Self-signed test certificate;
  this is not a CA-trusted production signature. Stable installer stays at 0.6.0.

## Website demo

`tools/B5MShot.Demo` renders the real WPF editor, settings and tray on synthetic
data. Scripted frames demonstrate arrow annotation and the actual palette opening,
stroke-size adjustment and closing. No private desktop, clipboard or upload is used.
`tools/encode-demo.py` creates a roughly 200 KB animated WebP and static poster.
Playback requires an explicit click, can be paused, and stops on tab hiding.
Scroll/hover transitions respect reduced-motion preferences.

Manrope is self-hosted under its included OFL license. Screenshot pages retain
their no-scroll layout, PNG download and format chooser in the new web theme.

## Verification

- `dotnet run --project tests/B5MShot.Smoke -c Release`: editor export isolation,
  palette animation/reversal, icon feedback, reduced-motion settings reopening,
  prerelease version label and compact tray geometry.
- `node tests/site-smoke.cjs` (Playwright + Edge): responsive landing/viewer,
  loaded assets, playback controls, format popover/Escape, no overflow, browser
  errors, reduced motion and mutation rejection by the read-only UI service.
- `node tests/site-live.cjs`: read-only checks on the live domain, including
  asset/font loading, demo controls and both download links. Passed after deployment.
- Viewer tested at 1440, 768, 390 and 375 px plus 844 × 390 landscape. The image
  itself is checked against its stage bounds, not only document scroll dimensions.
- Native main/tray screenshots are generated directly from WPF. Mixed-DPI
  placement and full-screen game interactions still need user acceptance.

## Safe deployment topology

The existing `b5mshot-server` remains running and handles uploads, raw images,
stable downloads and storage cleanup. New `b5mshot-ui-v3` handles only `/`,
`/assets/*`, `/i/*` and `/download/B5MShot-preview.exe`.

The new container runs as UID 10001, mounts screenshots/downloads read-only,
has no cleanup worker, rejects mutations, and has no public port. Memory is bounded.
`deploy/activate-ui.py` validates a staged Caddyfile, retains a dated backup and
gracefully reloads Caddy. It checks all existing container IDs/start times remain
unchanged and restores the original config if the public smoke test fails.

Do not run a broad compose recreation. Future UI deployments must preserve this
routing split or explicitly plan a coordinated backend update. To roll back the
UI routes, restore the saved Caddyfile **in place**, validate, then reload Caddy;
do not stop any unrelated services.

Activated 2026-09-27. Caddy rollback copy:
`/opt/media-stack/config/caddy/Caddyfile.before-b5mshot-ui-20260927-154729`.
Public preview SHA-256:
`b14135738b688a027006bb83a05fb1acb63b3cee2ab52feba7400744331e1383`.
The seven pre-existing service container start times were unchanged after rollout.
