"""Compare observed inactive hierarchy lifecycle against the actual Unity baseline."""
from runtime_capture_validation import load_capture, register_checkpoint, run_comparison, single_runtime_driver
import json,sys
from pathlib import Path
REFS={'Parent','Child','Dormant','Created','Counter','Sleeping','Recorder','DormantRecorder','CreatedRecorder','Added'}
def read(path):
    data=load_capture(path,case_insensitive_state=False)
    for frame in data['frames']:
        for actor in frame['actors']:
            actor['class']=actor.get('class',actor.get('className'));values=actor['state'];values={v['name']:v['value'] for v in values} if isinstance(values,list) else values
            actor['state']={k:str(v) if k in REFS else (v.lower()=='true' if isinstance(v,str) and v.lower() in ('true','false') else int(v) if isinstance(v,str) and v.lstrip('-').isdigit() else v) for k,v in values.items()}
    return data
def normalize(path):
    rows={};order=[]
    for frame in read(path)['frames']:
        driver=single_runtime_driver((a for a in frame['actors'] if a['class'] in ('RuntimeActiveDriver', 'RuntimeMiddle', 'RuntimeFinal')))
        if not driver:continue
        kind=driver['class'];s=driver['state']
        if not order or order[-1]!=kind:order.append(kind)
        if kind!='RuntimeActiveDriver' or int(s['Frame']) not in range(1,19):continue
        n=int(s['Frame']);actors={a['id']:a for a in frame['actors']}
        recorders={key:actors[s[key]]['state'] for key in ('Recorder','DormantRecorder','CreatedRecorder')}
        expected={2:4,5:2,6:4,7:2,8:42,16:4}.get(n,0)
        assert s['Immediate']==expected,(n,'hierarchy immediate callbacks',s['Immediate'])
        assert s['DormantImmediate']=={9:12,10:4,12:2}.get(n,0),(n,'dormant immediate callbacks')
        assert s['CreatedImmediate']=={14:12,15:4}.get(n,0),(n,'created immediate callbacks')
        if n<9:assert recorders['DormantRecorder']['Awakes']==0,(n,'inactive authored Awake ran early')
        if n==13:assert recorders['CreatedRecorder']['Awakes']==0,'inactive AddComponent Awake ran early'
        if n==14:assert recorders['CreatedRecorder']['Code']==123 and recorders['CreatedRecorder']['Updates']==0
        if n==15:assert recorders['CreatedRecorder']['Code']==45
        if n==16:assert recorders['Recorder']['Code']==45
        register_checkpoint(rows,int(n),None)
        rows[str(n)]={'driver':{k:v for k,v in s.items() if k not in REFS},**recorders}
    assert len(rows)==18,len(rows);assert order==['RuntimeActiveDriver','RuntimeMiddle','RuntimeFinal'],order
    return rows
def compare(a,b,path=''):
    if isinstance(a,dict):
        assert set(a)==set(b),(path,'keys')
        for k in a:compare(a[k],b[k],path+'/'+k)
    else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{'inactiveAuthoredAndAddedAwakeDeferred': True, 'parentAndChildStateVerified': True},'RUNTIME_ACTIVE_EXECUTION_COMPARISON_PASS',comparison=compare)
