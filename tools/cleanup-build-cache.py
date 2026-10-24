"""Delete regenerable Unreal Intermediate caches; retain sources, binaries and evidence."""
import argparse, ctypes, json, os, shutil, subprocess, sys, time
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def linked(p):
    return p.is_symlink() or p.is_junction()

def candidates(root):
    root=Path(root).absolute()
    for base,ds,fs in os.walk(root,followlinks=False):
        p=Path(base)
        ds[:]=[d for d in ds if not linked(p/d) and d.lower() not in ('library','temp','saved','deriveddatacache','.git','node_modules')]
        if 'offline-runtime-runner' not in [x.lower() for x in p.parts] and any(x.lower() in ('binaries','bin') for x in p.parts):
            for filename in fs:
                if filename.lower().endswith('.pdb') and not linked(p/filename):
                    yield p/filename
        if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir():
            library=p/'Library'
            if library.is_dir() and not linked(library):
                for cache in library.iterdir():
                    if cache.is_dir() and cache.name.lower() not in ('packagecache','packagemanager') and not linked(cache):
                        yield cache
        if any(n.lower().endswith(('.uproject','.uplugin')) for n in fs):
            for d in list(ds):
                if d.lower()=='intermediate':
                    yield p/d
                    ds.remove(d)

def inspect(p):
    if p.is_file():return os.stat(chr(92)*2+'?'+chr(92)+str(p)).st_size,1
    size=0;count=0
    for base,ds,fs in os.walk(p,followlinks=False):
        for n in ds+fs:
            if linked(Path(base)/n):
                raise ValueError('Linked entry: '+str(Path(base)/n))
        for n in fs:
            size+=os.stat(chr(92)*2+'?'+chr(92)+str(Path(base)/n)).st_size;count+=1
    return size,count

def lock(root):
    k=ctypes.WinDLL('kernel32',use_last_error=True)
    k.CreateFileW.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
    k.CreateFileW.restype=ctypes.c_void_p
    k.CloseHandle.argtypes=[ctypes.c_void_p]
    h=k.CreateFileW(str(root/'offline-editor-runtime.lock'),0xC0000000,0,None,4,0x80,None)
    if h==ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(),'Engine lease unavailable; cleanup refused')
    return k,h

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply',action='store_true')
    parser.add_argument('--run',nargs=argparse.REMAINDER,help='Run a test command, then clean its caches even on failure')
    args=parser.parse_args()
    artifacts=ROOT/'artifacts'
    if not artifacts.is_dir() or linked(artifacts):raise ValueError('Regular artifacts directory required')
    rc=0
    if args.run:
        rc=subprocess.run(args.run,cwd=ROOT).returncode
        args.apply=True
    kernel,handle=lock(artifacts)
    report={'apply':args.apply,'items':[],'removed_bytes':0,'errors':[],'command_exit':rc}
    try:
        before=shutil.disk_usage(artifacts).free
        for p in candidates(artifacts):
            try:
                resolved=p.resolve(strict=True)
                if not resolved.is_relative_to(artifacts.resolve()) or linked(p):raise ValueError('Outside artifacts')
                size,count=inspect(p)
                item={'path':str(p.relative_to(ROOT)),'bytes':size,'files':count,'removed':False}
                if args.apply:
                    if p.is_file():p.unlink()
                    else:shutil.rmtree(chr(92)*2+'?'+chr(92)+str(p))
                    item['removed']=True;report['removed_bytes']+=size
                report['items'].append(item)
            except (OSError,ValueError) as e:
                report['errors'].append({'path':str(p),'error':str(e)})
        report['free_bytes_delta']=shutil.disk_usage(artifacts).free-before
        report['finished']=time.strftime('%Y-%m-%dT%H:%M:%S%z')
        if args.apply:
            receipt=artifacts/('cache-cleanup-'+time.strftime('%Y%m%d-%H%M%S')+'.json')
            receipt.write_text(json.dumps(report,indent=2),encoding='utf-8')
            report['receipt']=str(receipt)
        print(json.dumps({'apply':args.apply,'candidates':len(report['items']),'candidate_GiB':round(sum(x['bytes'] for x in report['items'])/2**30,2),'removed_GiB':round(report['removed_bytes']/2**30,2),'free_GiB_delta':round(report['free_bytes_delta']/2**30,2),'errors':report['errors'],'receipt':report.get('receipt')},indent=2))
    finally:kernel.CloseHandle(handle)
    return rc or (1 if report['errors'] else 0)
if __name__=='__main__':sys.exit(main())
