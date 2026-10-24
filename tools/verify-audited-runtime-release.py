"""Archive and verify the published app, engine variants and Unicode extraction path."""
from pathlib import Path
import hashlib,json,zipfile,uuid
if __debug__ is False:raise RuntimeError("Assertions must be enabled")
root=Path(__file__).resolve().parent.parent;art=root/'artifacts';release=art/'EngineMigrator-audited-runtime-preview-20261023';archive=release.with_suffix('.zip');extracted=art/'배포 검증'/('audited-runtime-preview-20261023-'+uuid.uuid4().hex[:8])
def sha(path):return hashlib.file_digest(path.open('rb'),'sha256').hexdigest()
assert (release/'EngineMigrator.App.exe').is_file()
assert sha(release/'EngineMigrator.Core.dll')==sha(root/'tests/EngineMigrator.Checks/bin/Release/net9.0/EngineMigrator.Core.dll'),'Published core differs from the engine-tested CLI'
modules={}
for build in ('55116800','37670630'):
 folder=release/'adapters/unreal/native'/build/'EMBlueprintBridge';reference=root/'adapters/unreal/native'/build/'EMBlueprintBridge'
 assert json.loads((folder/'Binaries/Win64/UnrealEditor.modules').read_text(encoding='utf-8-sig'))['BuildId']==build
 binary='Binaries/Win64/UnrealEditor-EMBlueprintBridge.dll';assert sha(folder/binary)==sha(reference/binary)
 assert (folder/'Source/EMBlueprintBridge/Private/EMMesh.cpp').is_file()
 modules[build]=sha(folder/binary)
assert sha(release/'adapters/unity/ProjectBridge.cs')==sha(root/'adapters/unity/ProjectBridge.cs')
assert sha(release/'adapters/unreal/runtime_templates.py')==sha(root/'adapters/unreal/runtime_templates.py')
assert sha(release/'adapters/unreal/runtime_prototype_guard.py')==sha(root/'adapters/unreal/runtime_prototype_guard.py')
assert sha(release/'adapters/unity/ScriptComponentsBridge.cs')==sha(root/'adapters/unity/ScriptComponentsBridge.cs')
assert sha(release/'adapters/unity/ScriptDependenciesBridge.cs')==sha(root/'adapters/unity/ScriptDependenciesBridge.cs')
assert sha(release/'adapters/unity/PhysicsBridge.cs')==sha(root/'adapters/unity/PhysicsBridge.cs')
assert sha(release/'adapters/unity/AnimationBridge.cs')==sha(root/'adapters/unity/AnimationBridge.cs')
assert sha(release/'adapters/unreal/animation_components.py')==sha(root/'adapters/unreal/animation_components.py')
assert sha(release/'adapters/unreal/resource_bridge.py')==sha(root/'adapters/unreal/resource_bridge.py')
assert sha(release/'adapters/unreal/project_bridge.py')==sha(root/'adapters/unreal/project_bridge.py')
assert sha(release/'adapters/unity/UIBridge.cs')==sha(root/'adapters/unity/UIBridge.cs')
assert sha(release/'adapters/unreal/ui_components.py')==sha(root/'adapters/unreal/ui_components.py')
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
(art/'audited-runtime-archive-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print('RUNTIME_RELEASE_ARCHIVE_PASS')













