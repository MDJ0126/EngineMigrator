"""Copy an existing test project including its independent cache; supports Windows long paths."""
import os,shutil,sys
from pathlib import Path
workspace=Path(__file__).resolve().parent.parent;allowed=(workspace/'artifacts').resolve()
source=Path(sys.argv[1]).resolve();target=Path(sys.argv[2]).resolve()
assert source.is_relative_to(allowed) and target.is_relative_to(allowed) and source!=target and not target.is_relative_to(source)
def native(path):return '\\\\?\\'+str(path) if os.name=='nt' else str(path)
shutil.copytree(native(source),native(target),dirs_exist_ok=True,ignore=shutil.ignore_patterns('Temp','Logs','.vs','.git'))
print('PRIVATE_PROJECT_COPY_READY')
