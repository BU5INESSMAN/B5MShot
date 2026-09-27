"""Publish verified 0.8.0 downloads and switch UI without restarting existing apps."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone

def run(*args):
    return subprocess.check_output(args,text=True)

source=Path('/opt/b5mshot/release-0.8.0')
downloads=Path('/opt/b5mshot/deploy/downloads')
expected={
 'B5MShot.exe':'647cf764ebdf7180e90edb877ef9028099301d06d77281365b4b6e5cf3950c78',
 'B5MShot-Setup.exe':'ceb4030b27f940ab73c5cbb3c4bd9c4fd9a5092f95bb51bb0ea9c7dc352ce86f'
}
for name,digest in expected.items():
    with (source/name).open('rb') as stream:
        if hashlib.file_digest(stream,'sha256').hexdigest()!=digest:
            raise SystemExit('Release checksum mismatch: '+name)
config=Path('/opt/media-stack/config/caddy/Caddyfile')
original=config.read_bytes()
if original.count(b'reverse_proxy b5mshot-ui-v3:8080')!=1:
    raise SystemExit('Unexpected Caddy configuration; refusing overwrite')
updated=original.replace(b'reverse_proxy b5mshot-ui-v3:8080',b'reverse_proxy b5mshot-ui-v8:8080',1)
stamp=datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S')
backup=downloads/'backups'/('before-0.8.0-'+stamp)
backup.mkdir(parents=True,exist_ok=False)
config_backup=config.with_name('Caddyfile.before-0.8.0-'+stamp)
config_backup.write_bytes(original)
ids=run('docker','ps','-q').split()
before={c['Id']:(c['Name'],c['State']['StartedAt']) for c in json.loads(run('docker','inspect',*ids))}
staged=config.with_name('Caddyfile.staged-0.8.0')
staged.write_bytes(updated)
run('docker','cp',str(staged),'caddy:/tmp/Caddyfile-0.8.0')
run('docker','exec','caddy','caddy','validate','--config','/tmp/Caddyfile-0.8.0','--adapter','caddyfile')
if config.read_bytes()!=original:
    raise SystemExit('Caddyfile changed concurrently')
replaced=[]
try:
    for name in expected:
        shutil.copy2(downloads/name,backup/name)
        temporary=downloads/(name+'.0.8.0-new')
        shutil.copyfile(source/name,temporary)
        temporary.chmod(0o644)
        os.replace(temporary,downloads/name)
        replaced.append(name)
    config.write_bytes(updated)
    run('docker','exec','caddy','caddy','reload','--config','/etc/caddy/Caddyfile','--adapter','caddyfile')
    home=run('curl','--fail','--silent','--show-error','https://s.bu5inessman.ru/')
    if 'Скачать 0.8.0' not in home or 'Скачать preview.3' in home:
        raise RuntimeError('Live landing version check failed')
    for name in expected:
        run('curl','--fail','--head','--silent','--show-error','https://s.bu5inessman.ru/download/'+name)
    run('curl','--fail','--silent','--show-error','https://s.bu5inessman.ru/health')
except Exception:
    for name in replaced:
        temporary=downloads/(name+'.rollback')
        shutil.copy2(backup/name,temporary)
        os.replace(temporary,downloads/name)
    config.write_bytes(original)
    run('docker','exec','caddy','caddy','reload','--config','/etc/caddy/Caddyfile','--adapter','caddyfile')
    raise
after={c['Id']:(c['Name'],c['State']['StartedAt']) for c in json.loads(run('docker','inspect',*ids))}
if before!=after:
    raise RuntimeError('An existing container start time changed')
print('0.8.0 activated. All existing containers unchanged. Backups:',backup,config_backup)
