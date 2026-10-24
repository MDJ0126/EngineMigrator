from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
"""Compare synchronous enable/disable ordering, delayed Start, update suppression and destruction."""
import json,sys,math
from pathlib import Path
REFS={'Counter','Recorder','Authored','AuthoredRecorder','Created'}
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
        drivers=[a for a in frame['actors'] if a['class'] in ('RuntimeEnableDriver','RuntimeMiddle','RuntimeFinal')]
        if not drivers:continue
        assert len(drivers)==1;driver=drivers[0];kind=driver['class'];state=driver['state']
        if not order or order[-1]!=kind:order.append(kind)
        n=int(state['Frame'])
        if kind!='RuntimeEnableDriver' or n not in range(1,19):continue
        actors={a['id']:a for a in frame['actors']};authored=actors[state['Authored']];authored_recorder=actors[state['AuthoredRecorder']]
        assert authored['state']['Recorder']==authored_recorder['id']
        counter=actors.get(state['Counter']);recorder=actors.get(state['Recorder'])
        if counter:assert counter['state']['Recorder']==recorder['id']
        counter_state=lambda a:None if a is None else {k:v for k,v in a['state'].items() if k!='Recorder'}
        if n==1:assert authored['state']['Awakes']==1 and authored['state']['Starts']==0 and authored['state']['Enables']==0
        if n in (2,8,9,12):assert state['AtReturnStart']==0,(n,'Start ran inside enable/add')
        expected_immediate={2:12,3:4,4:2,5:4,6:2,7:4,8:124,9:2,10:42,11:4,12:2,13:4,14:2,16:42}.get(n,0)
        assert state['Immediate']==expected_immediate,(n,'immediate event sequence',state['Immediate'])
        if n in (7,11):assert counter is None and recorder['state']['Code']==45,(n,'OnDisable/OnDestroy order')
        if n==8:assert counter['state']['Starts']==0 and recorder['state']['Code']==124
        if n==9:assert counter['state']['Starts']==1 and recorder['state']['Code']==23
        register_checkpoint(rows,int(n),None)
        rows[str(n)]={'immediate':state['Immediate'],'startAtReturn':state['AtReturnStart'],'readEnabled':state['ReadEnabled'],'counter':counter_state(counter),'recorder':None if recorder is None else recorder['state'],'authored':counter_state(authored),'authoredRecorder':authored_recorder['state']}
    assert len(rows)==18,len(rows);assert order==['RuntimeEnableDriver','RuntimeMiddle','RuntimeFinal'],order
    return rows
def compare(a,b,path=''):
    if isinstance(a,dict):
        assert set(a)==set(b),(path,'keys')
        for key in a:compare(a[key],b[key],path+'/'+key)
    elif isinstance(a,(int,float)) and not isinstance(a,bool):assert math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-4),(path,a,b)
    else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{'synchronousEnableDisableVerified': True, 'disabledAuthoredStateAndFirstStartVerified': True, 'immediateDisableDeferredDestroyVerified': True},'RUNTIME_ENABLE_EXECUTION_COMPARISON_PASS',comparison=compare)
