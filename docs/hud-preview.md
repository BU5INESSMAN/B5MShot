# B5MShot 0.7.0-preview.1

Implements the approved capture/editor HUD, not the as-yet unapproved settings,
tray menu or update-dialog redesign. Production server and stable installer are
unchanged. Portable test executable: `release/preview-0.7.0/B5MShot.exe`.

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
It benchmarks 12 background captures without saving screen contents.

Local capture-only sample: median 74 ms, slowest of 12 samples 118 ms. These are
machine-specific and do not prove that all reported hotkey delays are eliminated.
The real file editor and its palette were also inspected on Windows.

Still requires user acceptance: mixed-DPI multi-monitor arrangements, gaming/fullscreen
applications, and end-to-end publication against the live server. No user screenshots
were uploaded during validation.

Close the older B5MShot instance through its tray menu before starting the preview
for hotkey tests; otherwise the existing single-instance guard will keep it active.
