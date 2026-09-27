# B5MShot 0.7.0-preview.2

Implements the approved capture/editor HUD, not the as-yet unapproved settings,
tray menu or update-dialog redesign. Production server and stable installer are
unchanged. Portable test executable: `release/preview-0.7.0-2/B5MShot.exe`.

## Motion and selection update

- Shared, interruptible damped-spring motion: 460 ms entrance, 340 ms tool settling,
  110 ms press response and 190 ms dismissal. No perpetual decorative loops.
- The notch reveals through an animated rounded clip, starting after ContentRendered.
  Its content is not scaled/squashed, and capture/drawing never waits for animation.
- Selection-to-editor handoff retains the 350 × 60 hint surface and crossfades to tools.
- Palette remains 330 px wide; only its own clip expands. Toolbar height stays 60 px.
  Repeated toggles reverse from the current value without accumulating animation clocks.
- All editor icon buttons have hover lift, press compression and one activation recoil.
  Shared application buttons receive gentler feedback; settings/update text and editor
  hints appear smoothly. Annotation text and live numeric readouts are not animated.
- A dark-backed white outline with blue corner markers persists during editing and
  also outlines imported files. It is a sibling of the image, never part of export.
- Windows animation preferences are respected. The AppContext switch
  `B5MShot.DisableAnimations` additionally supports deterministic reduced-motion tests.

Motion direction follows UI/UX Pro Max guidance on short, interruptible feedback,
[Apple motion principles](https://developer.apple.com/design/human-interface-guidelines/motion)
and [WPF render-transform guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/transforms-overview).
Animations replace existing clocks and leave explicit final base values, per
[WPF animation guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/animation-tips-and-tricks).

## Behavior

- Region capture opens an editor over the frozen desktop, preserving selection position.
- Full-desktop capture uses the same overlay. Imported files use a resizable editor.
- Icon-only HUD: pen, line, arrow, rectangle, text, mosaic, color/size, undo,
  save, copy, publish and close. Tooltips and accessibility labels identify actions.
- Narrow HSV/HEX palette expands from the HUD. Its color dot represents both
  stroke color and thickness. Windows reduced-motion setting disables animations.
- File-editor background follows the Windows app theme; HUD remains black.
- Copy/save close only on success. Cancelling Save or encountering an error retains edits.
- Publishing copies the link, closes the editor and shows a short result HUD.
  Clipboard failure retains the editor and URL, preventing accidental duplicate uploads.
- Ctrl+C in text fields copies text normally. Escape closes the palette first.

## Performance

Removed the unconditional 120 ms delay and ApplicationIdle dispatch for captures.
Desktop capture and PNG encoding run off the UI thread. Capture now copies from
locked bitmap memory instead of allocating an additional HBITMAP. Selection mask
geometry is reused. Clipboard retries use asynchronous delays.

`%LOCALAPPDATA%/B5MShot/logs/capture-timing.log` records request-to-ContentRendered
timing and pixel dimensions only, capped at approximately 64 KiB. It is not a
measurement of hardware input latency. No screen pixels or window titles are logged.

## Validation

Run `dotnet run --project tests/B5MShot.Smoke -c Release` on Windows with .NET 8 SDK.
The smoke suite opens temporary test windows and checks crop boundaries, scaled
coordinates, original-resolution export, HUD exclusion, narrow palette geometry,
HEX color, size indicator, tool switching and collapsed-panel input isolation.
It also checks intermediate animation values, icon recoil/rest, fixed toolbar height,
rapid reversal, persistent outline geometry, animated dismissal and reduced motion.
The interactive `--selection` mode uses a synthetic desktop and exposes test-only
taskbar windows to allow desktop UI inspection; production overlays remain tray-only.
It benchmarks 12 background captures without saving screen contents.

Local preview.2 capture-only sample: median 70 ms, slowest of 12 samples 92 ms. These are
machine-specific and do not prove that all reported hotkey delays are eliminated.
The self-contained preview.2 file editor, palette and outline were inspected on Windows,
including visible intermediate frames of palette opening and closing. The UI tool
captures only a bounded portion of the spanning desktop window; full multi-monitor
handoff needs direct user acceptance in addition to the geometry/export tests.

Still requires user acceptance: mixed-DPI multi-monitor arrangements, gaming/fullscreen
applications, and end-to-end publication against the live server. No user screenshots
were uploaded during validation.

Close the older B5MShot instance through its tray menu before starting the preview
for hotkey tests; otherwise the existing single-instance guard will keep it active.
