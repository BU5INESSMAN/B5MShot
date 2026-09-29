# 0.8.4 validation — 2026-09-29

Source/tag: `5995fc4366123d8dc114923ead8bf48449dc3ce2`, `v0.8.4`.

## Verified

- Release workflow https://github.com/BU5INESSMAN/B5MShot/actions/runs/36530493504 succeeded.
- Real native installer (only modal confirmation suppressed in a separate, unpublished CI binary): clean install, certificate import, PowerShell 5.1 execution and retained trust.
- Actual installed 0.8.3 running in the background upgraded to 0.8.4; old process exited without TerminateProcess or Stop-Process.
- Simulated sign-in ordering: published portable 0.8.3 acquired the single-instance mutex first; installed 0.8.4 replaced it and remained running.
- Stable AppsFolder startup command activated exactly one installed 0.8.4 process.
- Real new application's editor refused update shutdown and remained running. Separate isolated WPF fixtures cover hidden editors, legacy modal update notifications, idle legacy shutdown, protocol shutdown, busy responses, same-version and downgrade protection.
- Installer workflow mocks cover clean/update/idle, busy editor, unresponsive process, another session, Appx failure, deferred deployment, migration/launch warnings and installed-version verification failure.
- Autostart migration preserves disabled/empty state and unrelated Run values; command and registry-policy tests passed.
- Local full WPF smoke suite passed: editor/export, crop move/resize, annotation coordinates, shortcuts, settings, reduced motion and all four HUD edges. One preceding attempt hit the existing timing-sensitive animation-frame assertion; a subsequent complete run passed. This does not measure reboot timing.
- Both production EXEs: expected 0.8.4 versions, signer C5475B8C2639D3F402CDBCC2076105974C9CACA9, matching SHA-256 manifest. Embedded MSIX identity, signature, extension and exact app hash verified.
- Live GitHub updater found 0.8.4 from a simulated 0.8.3 client; downloaded and verified installer, without executing it locally.
- Site smoke passed: 0.8.4 download buttons, assets, demo play/stop, no browser errors.
- Deployment replaced only download files and landing HTML. Existing container identities/start times unchanged. Backup: `/opt/b5mshot/deploy/downloads/backups/before-0.8.4-20260929-062307`.

## Release hashes

```
b2c9744e98f68685f51a2428012bc13db0d7d840950ddfecade21d7e30d94271  B5MShot.exe
e9d84e797b44aab72b6649783b64962619aad7d9b4b4e55e057bfe2a94b7223c  B5MShot-Setup.exe
```

## Scope and limits

The reported screenshot was an intentional blanket background-process blocker, not a Windows package error. On initial inspection a legacy 0.6.0 portable process existed. On reinspection the user's process and installed package had already become 0.8.3. The actual Run entry pointed to the stable package identity; no additional B5MShot startup commands/tasks/Startup-folder files were found. No unverified claim is made about the exact earlier reboot ordering.

The user's PC was not rebooted and its installed app was not silently replaced during QA. Sign-in activation and old-first process ordering were tested on disposable Windows runners. UAC and the setup confirmation still require user interaction; the test certificate is not a publicly trusted publisher certificate. Existing editors and unknown/unresponsive processes are not forcibly terminated. Legacy compatibility is limited to recognized 0.6.0–0.8.3 WPF builds, same user and session; newer builds use an explicit UI-thread update handoff. Other server applications were not stopped or restarted.
