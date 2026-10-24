"""Use the actual UI lifetime/clone scenario with a source field named Owner."""
from pathlib import Path
import shutil
import json
root=Path(__file__).resolve().parent.parent
home=root/'artifacts/owner-field-check'
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
assert core.resolve().is_relative_to((root/'artifacts').resolve())
manifest=core/'Project/EngineMigratorRun.json'
if not manifest.exists():manifest.write_text(json.dumps({'runtimeScenes':{}}))
(home/'runner').mkdir(parents=True,exist_ok=True)
shutil.copyfile(root/'artifacts/ui-equality-check/runner/Runner.csproj',home/'runner/Runner.csproj')
shutil.copyfile(root/'artifacts/ui-equality-check/core-host-before-ui-destruction.cpp.txt',home/'core-host-before-ui-destruction.cpp.txt')
fixture=root/'tests/EngineMigrator.Checks/Fixtures/owner-field.cs.txt'
fixture.write_text((root/'tests/EngineMigrator.Checks/Fixtures/ui-destruction.cs.txt').read_text().replace('AuditUIDestruction','AuditOwnerCollision').replace('UIOwner','Owner'))
script=(root/'tools/prepare-ui-destruction-probe.py').read_text()
script=script.replace('ui-equality-check','owner-field-check').replace('AuditUIDestruction','AuditOwnerCollision').replace('ui-destruction.cs.txt','owner-field.cs.txt').replace('UIOwner','Owner')
script=script.replace('TEXT("Owner")','TEXT("EM_Field_Owner")')
# Keep separate Unity files so the existing validated fixture remains intact.
script=script.replace('Assets/EngineMigratorUIEqualityProbe','Assets/EngineMigratorOwnerFieldProbe').replace('EMUIDestruction','EMOwnerField').replace('EM_UI_DESTRUCTION','EM_OWNER_FIELD')
(root/'artifacts/runtime-ui-api-tests/UnitySource/Assets/EngineMigratorOwnerFieldProbe/Editor').mkdir(parents=True,exist_ok=True)
exec(compile(script,str(root/'tools/prepare-ui-destruction-probe.py'),'exec'))
verifier=(root/'tools/verify-ui-destruction.py').read_text().replace('ui-equality-check','owner-field-check').replace('EMUIDestructionProbe.Run','EMOwnerFieldProbe.Run')
(root/'tools/verify-owner-field-probe.py').write_text(verifier)
