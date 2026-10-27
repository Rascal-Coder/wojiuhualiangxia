"""Package the current export without screenshots, test reports or debug symbols."""
from pathlib import Path
import json, zipfile

P = Path(__file__).resolve().parents[1]
M = P / 'Builds/WeChat/minigame'
config = json.loads((M / 'project.config.json').read_text(encoding='utf-8-sig'))
game = json.loads((M / 'game.json').read_text(encoding='utf-8-sig'))
assert config['appid'] == 'wx3a628e8b48216b63' and config['compileType'] == 'game'
assert game['openDataContext'] == 'open-data'
assert 'loadDataPackageFromSubpackage: true' in (M / 'game.js').read_text(encoding='utf-8-sig')
assert not (M / 'images/unity_logo.png').exists(), 'Hidden duplicate loading logo must not be packaged'

def ignored(file):
    rel = file.relative_to(M).as_posix()
    for rule in config.get('packOptions', {}).get('ignore', []):
        value = rule['value'].rstrip('/')
        if rule['type'] == 'suffix' and rel.endswith(value): return True
        if rule['type'] == 'folder' and (rel == value or rel.startswith(value + '/')): return True
    return False

files = sorted(f for f in M.rglob('*') if f.is_file() and not ignored(f))
roots = [s['root'].rstrip('/') + '/' for s in game.get('subpackages', [])]
for root in roots:
    entry = M / root / 'game.js'
    assert entry.is_file() and entry.read_text(encoding='utf-8-sig').strip(), f'Missing executable subpackage entry: {entry}'
total = sum(f.stat().st_size for f in files)
main = sum(f.stat().st_size for f in files if not any(f.relative_to(M).as_posix().startswith(r) for r in roots))
# DevTools compilation adds code bytes; do not ship a package near its 4MiB cap.
assert main <= 3774873, f'Main source {main} exceeds the 3.6MiB safety budget'
assert total < 30 * 1024**2
dest = P.parent / '我就划两下_微信小游戏_最新版.zip'
with zipfile.ZipFile(dest, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    for file in files: archive.write(file, 'minigame/' + file.relative_to(M).as_posix())
    archive.write(P/'微信小游戏导出说明.txt', '导入说明.txt')
with zipfile.ZipFile(dest) as archive: assert archive.testzip() is None
print(f'PASS: {len(files)} files; main {main} bytes; total {total} bytes; archive {dest.stat().st_size} bytes')
