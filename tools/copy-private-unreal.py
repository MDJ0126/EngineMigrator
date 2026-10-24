"""Independent Unreal packaging copy; retain binaries and skip rebuildable caches."""
import os,shutil,sys
from pathlib import Path
allowed=(Path(__file__).resolve().parent.parent/'artifacts').resolve()
source,target=(Path(x).resolve() for x in sys.argv[1:3])
assert source.is_relative_to(allowed) and target.is_relative_to(allowed) and source!=target and not target.is_relative_to(source)
assert not target.exists(),'Use a fresh private packaging directory'
def native(path):return chr(92)*2+'?'+chr(92)+str(path) if os.name=='nt' else str(path)
shutil.copytree(native(source),native(target),ignore=shutil.ignore_patterns('Intermediate','Saved','DerivedDataCache','.vs','.git','__pycache__'))
print('PRIVATE_UNREAL_PACKAGING_COPY_READY')
