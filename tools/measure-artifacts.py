"""Measure every regular artifacts file; report read errors and links separately."""
import os,json,collections,time
from pathlib import Path
root=Path(__file__).resolve().parent.parent/'artifacts';native=chr(92)*2+'?'+chr(92)+str(root)
totals=collections.Counter();categories=collections.Counter();errors=[];links=[];count=0;stack=[native]
while stack:
 base=stack.pop();relative=os.path.relpath(base,native);parts=relative.lower().split(os.sep)
 try:
  with os.scandir(base) as entries:
   for entry in entries:
    try:
     stat=entry.stat(follow_symlinks=False)
     if stat.st_file_attributes&0x400:links.append(entry.path);continue
     if entry.is_dir(follow_symlinks=False):stack.append(entry.path);continue
     totals[relative.split(os.sep)[0]]+=stat.st_size;count+=1
     category=next((x for x in ('packagecache','saved','archive','offlinestage','binaries','library','intermediate','deriveddatacache') if x in parts),'other');categories[category]+=stat.st_size
    except OSError as ex:errors.append(str(ex))
 except OSError as ex:errors.append(str(ex))
report={'path':str(root),'bytes':sum(totals.values()),'GB':round(sum(totals.values())/1e9,6),'GiB':round(sum(totals.values())/2**30,6),'files':count,'top':[(k,round(v/2**30,4)) for k,v in totals.most_common(20)],'categories_GiB':{k:round(v/2**30,4) for k,v in categories.items()},'errors':errors,'links':links,'finished':time.strftime('%Y-%m-%dT%H:%M:%S%z')}
# Save outside artifacts so writing the measurement does not change the measured tree.
(root.parent/'docs'/'artifacts-size-latest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
raise SystemExit(1 if errors else 0)
