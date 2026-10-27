"""Produce deployment inputs only; no credentials or test output."""
from pathlib import Path
import io
import zipfile

root = Path(__file__).resolve().parents[1]
source = root / 'CloudSave'
function = io.BytesIO()
with zipfile.ZipFile(function, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in ['index.js', 'package.json']:
        archive.write(source / 'function' / name, name)
function_output = root.parent / 'liangxia-save-function.zip'
function_output.write_bytes(function.getvalue())
with zipfile.ZipFile(function_output) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == {'index.js', 'package.json'}
output = root.parent / '我就划两下_云存档部署包.zip'
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.writestr('liangxia-save-function.zip', function.getvalue())
    for name in ['setup.sql', 'config.example.json', '接入步骤.txt']:
        archive.write(source / name, name)
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
print(function_output)
print(output)
