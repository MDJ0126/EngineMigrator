"""Diagnose captured fixed-step spawning; reject unobserved/incorrect input timing."""
from pathlib import Path
import argparse,json,math
r=Path(__file__).resolve().parent.parent;h=r/'artifacts/fixed-spawn-check'
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def capture(name,columns):
 d=load(h/(name+'.json'));fps=int(name.split('-')[1]);rows=d['rows'];deltas=d.get('deltas')
 if d.get('status')!='CAPTURED'or len(rows)!=24:raise RuntimeError('Incomplete capture '+name)
 if not isinstance(deltas,list)or len(deltas)!=24 or any(not math.isfinite(v)or abs(v-1/fps)>1e-7 for v in deltas):raise RuntimeError('Observed timing differs '+name+': '+str(deltas))
 for i,row in enumerate(rows,1):
  if len(row)!=len(columns)or row[0]!=i or any(not isinstance(v,(int,float))or not math.isfinite(v)or v!=int(v)for v in row):raise RuntimeError('Invalid integer row '+name)
 return rows
p=argparse.ArgumentParser();p.add_argument('case');p.add_argument('--mode',default='native',choices=['native','restored']);args=p.parse_args();columns=load(h/'columns.json');left=capture('original-'+args.case,columns);right=capture(args.mode+'-'+args.case,columns);diff=[]
for i,(a,b)in enumerate(zip(left,right),1):
 for j,(v,w)in enumerate(zip(a,b)):
  if v!=w:diff.append({'frame':i,'column':columns[j],'original':v,'actual':w})
report={'status':'PASS'if not diff else'DIFFERENCES','case':args.case,'mode':args.mode,'frames':24,'values':len(columns),'observedTimingVerified':True,'differences':diff};(h/('comparison-'+args.mode+'-'+args.case+'.json')).write_text(json.dumps(report,indent=2));print(json.dumps({'status':report['status'],'differenceCount':len(diff),'first':diff[:12]},indent=2))
raise SystemExit(bool(diff))
