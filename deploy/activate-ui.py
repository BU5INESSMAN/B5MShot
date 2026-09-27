"""Route only presentation traffic to the separately tested read-only UI container.

Run on the VPS after starting b5mshot-ui-v3. Existing services are not restarted.
The original Caddyfile and running-container start times are retained for rollback.
"""
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

def run(*args):
    return subprocess.check_output(args, text=True)

config = Path('/opt/media-stack/config/caddy/Caddyfile')
original = config.read_bytes()
old = b'''s.bu5inessman.ru {
    encode gzip zstd

    request_body {
        max_size 16MB
    }

    reverse_proxy b5mshot-server:8080
}'''
new = b'''s.bu5inessman.ru {
    encode gzip zstd

    request_body {
        max_size 16MB
    }

    @b5mshot_presentation path / /assets/* /i/* /download/B5MShot-preview.exe
    handle @b5mshot_presentation {
        reverse_proxy b5mshot-ui-v3:8080
    }
    handle {
        reverse_proxy b5mshot-server:8080
    }
}'''
if original.count(old) != 1:
    raise SystemExit('Expected site block differs; refusing to change Caddyfile')
updated = original.replace(old, new, 1)
stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
backup = config.with_name('Caddyfile.before-b5mshot-ui-' + stamp)
backup.write_bytes(original)
ids = run('docker', 'ps', '-q').split()
before = {c['Id']: (c['Name'], c['State']['StartedAt']) for c in json.loads(run('docker', 'inspect', *ids))}
backup.with_suffix('.containers.json').write_text(json.dumps(before, indent=2))
staged = config.with_name('Caddyfile.b5mshot-ui-staged')
staged.write_bytes(updated)
run('docker', 'cp', str(staged), 'caddy:/tmp/b5mshot-ui-Caddyfile')
run('docker', 'exec', 'caddy', 'caddy', 'validate', '--config', '/tmp/b5mshot-ui-Caddyfile', '--adapter', 'caddyfile')
if config.read_bytes() != original:
    raise SystemExit('Caddyfile changed concurrently; refusing to overwrite')
try:
    # In-place write preserves the bind-mounted inode; never replace or restart Caddy.
    config.write_bytes(updated)
    run('docker', 'exec', 'caddy', 'caddy', 'reload', '--config', '/etc/caddy/Caddyfile', '--adapter', 'caddyfile')
    home = run('curl', '--fail', '--silent', '--show-error', 'https://s.bu5inessman.ru/')
    if 'editor-demo' not in run('curl', '--fail', '--silent', '--show-error', 'https://s.bu5inessman.ru/assets/site.js?v=3') or 'preview.3' not in home:
        raise RuntimeError('Presentation smoke test failed')
    run('curl', '--fail', '--silent', '--show-error', 'https://s.bu5inessman.ru/health')
except Exception:
    config.write_bytes(original)
    run('docker', 'exec', 'caddy', 'caddy', 'reload', '--config', '/etc/caddy/Caddyfile', '--adapter', 'caddyfile')
    raise
after = {c['Id']: (c['Name'], c['State']['StartedAt']) for c in json.loads(run('docker', 'inspect', *ids))}
if before != after:
    raise RuntimeError('Container start times unexpectedly changed; inspect immediately')
print('Activated. Existing container IDs and start times unchanged. Backup:', backup)
