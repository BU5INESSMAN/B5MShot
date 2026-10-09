# 0.8.7 validation — 2026-10-09

Source/tag: `ea2eaab648153dbf95a5e6af885bdfd774f3e5c8`, `v0.8.7`.

- Local Release solution build and WPF smoke suite passed; installer workflow mocks and exact shell-host identity guards passed.
- Release workflow [37890282224](https://github.com/BU5INESSMAN/B5MShot/actions/runs/37890282224) passed, including signed build and native clean installation.
- Real 0.8.6 -> 0.8.7 upgrade passed while holding the packaged Explorer menu COM object in an actual surrogate. The old tray process exited safely, deployment completed, and activation returned a verified new-version process.
- A controlled unrelated process named dllhost remained alive. The test uses a controlled lifetime because idle system COM hosts can exit naturally during deployment.
- Legacy startup migration, takeover from an old portable instance, exact installed-version sign-in activation and real unsaved-editor shutdown protection passed.
- Initial regression fixture failures were corrected before publication: COM activation must explicitly request the local server; unrelated host lifetime must be controlled. No failed build was published.
- Draft assets verified: SHA-256, Authenticode signer `C5475B8C2639D3F402CDBCC2076105974C9CACA9`, file versions, embedded signed MSIX, extension and exact embedded app bytes.
- Stable release published; anonymous latest endpoint advertises v0.8.7. Public landing displays 0.8.7 and health endpoint responds.
- Website deployment preserved existing containers and start times. Backup: `/opt/b5mshot/deploy/downloads/backups/before-0.8.7-20261009-055128`.
- Both public website executable downloads match the release hashes below, checked through the public HTTPS URLs from the VPS.
- No local app installation, update, restart or process shutdown was performed. Read-only inspection still shows installed package 0.8.4.0; the user will exercise the updater themselves. Windows UAC interaction was not manually tested on their desktop.

```text
9ed37987353515879906d9301948ab9f4ef03eb2985a5213172442654ae79af9  B5MShot.exe
26337469ec85cc81ed86c20894140b173bd997fc129c36705bb0c4c070900b4d  B5MShot-Setup.exe
```
