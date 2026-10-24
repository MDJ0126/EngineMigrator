"""Archive and verify the published app, engine variants and Unicode extraction path."""
from pathlib import Path
import hashlib,json,zipfile,uuid
root=Path(__file__).resolve().parent.parent;art=root/'artifacts';release=art/'EngineMigrator-runtime-prefab-preview-20261023';archive=release.with_suffix('.zip');extracted=art/'배포 검증'/('runtime-prefab-preview-20261023-'+uuid.uuid4().hex[:8])
def sha(path):return hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
assert (release/'EngineMigrator.App.exe').is_file()
modules={}
for build in ('55116800','37670630'):
 folder=release/'adapters/unreal/native'/build/'EMBlueprintBridge';reference=root/'adapters/unreal/native'/build/'EMBlueprintBridge'
 assert json.loads((folder/'Binaries/Win64/UnrealEditor.modules').read_text(encoding='utf-8-sig'))['BuildId']==build
 binary='Binaries/Win64/UnrealEditor-EMBlueprintBridge.dll';assert sha(folder/binary)==sha(reference/binary)
 assert (folder/'Source/EMBlueprintBridge/Private/EMMesh.cpp').is_file()
 modules[build]=sha(folder/binary)
assert sha(release/'adapters/unity/ProjectBridge.cs')==sha(root/'adapters/unity/ProjectBridge.cs')
assert sha(release/'adapters/unreal/runtime_templates.py')==sha(root/'adapters/unreal/runtime_templates.py')
assert sha(release/'guide.html')==sha(root/'docs/guide.html')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for path in release.rglob('*'):
  assert not path.is_symlink()
  if path.is_file():z.write(path,path.relative_to(release))
assert not extracted.exists(),'Use a fresh private extraction directory'
extracted.mkdir(parents=True)
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 for entry in z.infolist():assert (extracted/entry.filename).resolve().is_relative_to(extracted.resolve())
 z.extractall(extracted)
files=list(p for p in release.rglob('*') if p.is_file())
for path in files:assert sha(path)==sha(extracted/path.relative_to(release))
result={'status':'PASS','filesCompared':len(files),'zipBytes':archive.stat().st_size,'zipCrcVerified':True,'extractedFileHashesVerified':True,'unicodeExtraction':str(extracted),'engineBuildIds':modules}
(art/'runtime-prefab-archive-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('RUNTIME_RELEASE_ARCHIVE_PASS')



