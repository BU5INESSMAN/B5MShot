"""Replace only verified downloads and landing copy; never restart containers."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from urllib.request import urlopen

def run(*args):
    return subprocess.check_output(args, text=True)

if len(sys.argv) != 4:
    raise SystemExit('Usage: rollout-0.8.5.py APP_SHA256 SETUP_SHA256 SOURCE_COMMIT')
expected = dict(zip(('B5MShot.exe', 'B5MShot-Setup.exe'), sys.argv[1:3]))
commit = sys.argv[3]
if any(len(value) != 64 or any(c not in '0123456789abcdef' for c in value) for value in expected.values()):
    raise SystemExit('Invalid expected hashes')
if len(commit) != 40 or any(c not in '0123456789abcdef' for c in commit):
    raise SystemExit('Invalid source commit')
downloads = Path('/opt/b5mshot/deploy/downloads')
landing = Path('/opt/b5mshot/ui-0.8.0/wwwroot/landing.html')
if 'Скачать 0.8.4' not in landing.read_text():
    raise SystemExit('Unexpected live landing version; refusing overwrite')
stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
backup = downloads / 'backups' / ('before-0.8.5-' + stamp)
backup.mkdir(parents=True, exist_ok=False)
stage = Path('/opt/b5mshot') / ('release-0.8.5-' + stamp)
stage.mkdir(exist_ok=False)
ids = run('docker', 'ps', '-q').split()
before = {c['Id']: (c['Name'], c['State']['StartedAt']) for c in json.loads(run('docker', 'inspect', *ids))}
for name, digest in expected.items():
    with urlopen('https://github.com/BU5INESSMAN/B5MShot/releases/download/v0.8.5/' + name, timeout=120) as response, (stage/name).open('wb') as target:
        shutil.copyfileobj(response, target)
    with (stage/name).open('rb') as source:
        if hashlib.file_digest(source, 'sha256').hexdigest() != digest:
            raise SystemExit('Downloaded file checksum mismatch: ' + name)
with urlopen('https://raw.githubusercontent.com/BU5INESSMAN/B5MShot/' + commit + '/src/B5MShot.Server/wwwroot/landing.html', timeout=30) as response:
    new_landing = response.read()
if 'Скачать 0.8.5'.encode() not in new_landing:
    raise SystemExit('Unexpected new landing version')
shutil.copy2(landing, backup/'landing.html')
for name in expected:
    shutil.copy2(downloads/name, backup/name)
replaced = []
try:
    for name in expected:
        temporary = downloads/(name + '.0.8.5-new')
        shutil.copyfile(stage/name, temporary)
        temporary.chmod(0o644)
        os.replace(temporary, downloads/name)
        replaced.append(name)
    temporary = landing.with_name('landing.0.8.5-new.html')
    temporary.write_bytes(new_landing)
    temporary.chmod(0o644)
    os.replace(temporary, landing)
    if 'Скачать 0.8.5' not in run('curl', '--fail', '--silent', '--show-error', 'https://s.bu5inessman.ru/'):
        raise RuntimeError('Public landing verification failed')
    for name in expected:
        run('curl', '--fail', '--head', '--silent', '--show-error', 'https://s.bu5inessman.ru/download/' + name)
    run('curl', '--fail', '--silent', '--show-error', 'https://s.bu5inessman.ru/health')
except Exception:
    for name in replaced:
        temporary = downloads/(name + '.rollback-0.8.5')
        shutil.copy2(backup/name, temporary)
        os.replace(temporary, downloads/name)
    temporary = landing.with_name('landing.rollback-0.8.5.html')
    shutil.copy2(backup/'landing.html', temporary)
    os.replace(temporary, landing)
    raise
after = {c['Id']: (c['Name'], c['State']['StartedAt']) for c in json.loads(run('docker', 'inspect', *ids))}
if before != after:
    raise RuntimeError('An existing container changed start time')
print('PASS: 0.8.5 deployed; existing containers unchanged. Backup:', backup)
