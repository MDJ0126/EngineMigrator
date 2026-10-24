from runtime_capture_validation import load_capture, register_checkpoint, run_comparison
"""Compare component identity, inheritance, state, lifecycle and hierarchy at engine checkpoints."""
import json,sys,math
from pathlib import Path
def load(path):
    data=load_capture(path,case_insensitive_state=False)
    for frame in data['frames']:
        for actor in frame['actors']:
            actor['class']=actor.get('class',actor.get('className'))
            if isinstance(actor['state'],list):actor['state']={v['name']:v['value'] for v in actor['state']}
    return data
def state(actor):
    values={}
    for name,value in actor['state'].items():
        if name in ('Driver','Authored','Added','Second','Found','AfterRemoval','Created','Clone','CallOwner'):values[name]=str(value)
        elif isinstance(value,str) and value.startswith('{'):values[name]=json.loads(value)
        elif isinstance(value,str) and value.lower() in ('true','false'):values[name]=value.lower()=='true'
        elif isinstance(value,str) and value.lstrip('-').isdigit():values[name]=int(value)
        elif isinstance(value,str):
            try:values[name]=float(value)
            except ValueError:values[name]=value
        else:values[name]=value
    return values
def normalize(path):
    result={};order=[]
    for frame in load(path)['frames']:
        drivers=[a for a in frame['actors'] if a['class'] in ('RuntimeComponents','RuntimeMiddle','RuntimeFinal')]
        if not drivers:continue
        assert len(drivers)==1;driver=drivers[0];s=state(driver);kind=driver['class']
        if not order or order[-1]!=kind:order.append(kind)
        if kind!='RuntimeComponents' or int(s['Frame']) not in (2,3,4,5,6,7,8,9,10,12,13,16):continue
        number=int(s['Frame']);actors={a['id']:a for a in frame['actors']};objects={o['id']:o for o in frame['objects']}
        authored=actors[s['Authored']];assert authored['class']=='RuntimeAddedBase' and state(authored)['Driver']==driver['id'],(number,'authored component cycle')
        assert (s['AtAddAwakes'],s['AtAddStarts'],s['AtAddValue'],s['AfterSetValue'])==(1,0,4,7),(number,'AddComponent immediate lifecycle',s)
        assert s['Same'] is True
        assert (s['AfterCall'],s['ReadCall'],s['CallBool'],s['CallNumber'],s['CallFloat'])==(11,11,True,3.75,3.0),(number,'component calls',s)
        if number<12:assert s['CallOwner']==s['Created']
        if number>=6:assert s['DuringDestroy'] is True
        if number>=7:assert s['MissingMatches'] is True,(number,'destroyed component compared to null variable')
        rows=[]
        for actor in frame['actors']:
            if actor['class']!='RuntimeAddedCounter':continue
            values=state(actor);owner='created' if actor['owner']==s['Created'] else 'clone' if actor['owner']==s['Clone'] else 'unexpected';assert owner!='unexpected'
            identity='first' if actor['id']==s['Added'] else 'second' if actor['id']==s['Second'] else 'cloned' if owner=='clone' else 'unexpected';assert identity!='unexpected'
            if identity=='first':assert values['Driver']==driver['id'],(number,'assigned component reference')
            values['Driver']='driver' if values['Driver']==driver['id'] else '' if not values['Driver'] else 'unexpected';assert values['Driver']!='unexpected'
            assert actor['owner'] in objects
            rows.append({'identity':identity,'owner':owner,'state':values})
        expected=1 if number in (2,3,6,7) else 2 if number in (4,5,8,9,10) else 0
        assert len(rows)==expected,(number,len(rows),expected)
        if number in (2,3,4,5):assert s['Found']==s['Added'] and s['Found'] in actors
        if number in (8,9,10):assert s['AfterRemoval']==s['Second'] and s['AfterRemoval'] in actors
        if number>=12:assert not s['Added'] and not s['Second'] and not s['Created'] and not s['Clone']
        register_checkpoint(result,int(number),(2,3,4,5,6,7,8,9,10,12,13,16))
        result[str(number)]={'counters':sorted(rows,key=lambda row:row['identity']),'authoredCycleVerified':True,'immediateAwake':s['AtAddAwakes'],'immediateStart':s['AtAddStarts'],'firstMatchVerified':s['Same']}
    assert len(result)==12,len(result);assert order==['RuntimeComponents','RuntimeMiddle','RuntimeFinal'],order
    return result
def compare(a,b,path=''):
    if isinstance(a,dict):
        assert set(a)==set(b),(path,'keys',set(a),set(b))
        for key in a:compare(a[key],b[key],path+'/'+key)
    elif isinstance(a,list):
        assert len(a)==len(b),(path,'length')
        for i,(x,y) in enumerate(zip(a,b)):compare(x,y,path+'/'+str(i))
    elif isinstance(a,(int,float)) and not isinstance(a,bool):assert math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-4),(path,a,b)
    else:assert a==b,(path,a,b)
if __name__=='__main__':
 run_comparison(sys.argv[1:],normalize,lambda rows:None,lambda expected,actual,path:{'typedComponentReferencesVerified': True, 'inheritanceAndFirstMatchVerified': True, 'immediateAwakeDeferredStartAndUpdateVerified': True},'RUNTIME_COMPONENT_EXECUTION_COMPARISON_PASS',comparison=compare)
