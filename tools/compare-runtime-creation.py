"""Compare actual creation, parenting, pose changes, clone names and deletion."""
import json,sys,math
from pathlib import Path

def normalize(path):
    raw=Path(path).read_bytes();data=json.loads(raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'));assert data['status']=='CAPTURED'
    result={};order=[]
    for frame in data['frames']:
        drivers=[a for a in frame['actors'] if a.get('class',a.get('className')) in ('RuntimeCreation','RuntimeMiddle','RuntimeFinal')]
        if not drivers:continue
        assert len(drivers)==1;driver=drivers[0];kind=driver.get('class',driver.get('className'))
        state=driver['state'];state={v['name']:v['value'] for v in state} if isinstance(state,list) else state
        if not order or order[-1]!=kind:order.append(kind)
        number=int(state['Frame'])
        if kind!='RuntimeCreation' or number not in (2,3,4,5,6,8,10,12,14,18,20,23):continue
        objects={o['id']:o for o in frame['objects']};rows={}
        for field in ('Created','Child','DefaultObject','Clone','LongNamed','UpperCase','LowerCase'):
            value=objects.get(str(state[field]))
            if value is None:rows[field]=None;continue
            parent=value['parent'];symbol=next((k for k in ('Created','Clone','Parent') if str(state[k])==parent),'') if parent else ''
            assert not parent or symbol,(number,field,'unmapped parent')
            rows[field]={'name':value['name'],'parent':symbol,'hidden':value['hidden'],**{k:value[k] for k in ('position','localPosition','scale','localScale','rotation','localRotation')}}
            if field=='Clone':rows[field]['children']=[{'name':v['name'],**{k:v[k] for k in ('position','localPosition','scale','localScale','rotation','localRotation')}} for v in objects.values() if v['parent']==value['id']]
        if number>=6:
            rows['readback']={k:json.loads(state[k]) if isinstance(state[k],str) else state[k] for k in ('ReadPosition','ReadLocal','ReadScale','ReadRotation')}
            assert str(state['ReadParent'])==str(state['Parent'])
        if number>=12:assert str(state['DuringDestroy']).lower()=='true'
        if number in (12,14):assert rows['Created'] is None and rows['Child'] is None,(number,'destruction failed')
        if number==14:assert rows['Clone'] is None and rows['DefaultObject'] is None
        result[str(number)]=rows
    assert len(result)==12,len(result)
    assert order==['RuntimeCreation','RuntimeMiddle','RuntimeFinal'],order
    assert result['2']['DefaultObject']['name']=='New Game Object'
    assert result['2']['LongNamed']['name']=='이름'*300
    assert result['2']['UpperCase']['name']=='Object' and result['2']['LowerCase']['name']=='object'
    assert result['8']['Clone']['name']=='변경된 객체(Clone)'
    assert result['18']['Created']['name']=='재생성'
    return result

def compare(a,b,path=''):
    if isinstance(a,dict):
        assert set(a)==set(b),(path,'keys')
        if set(a)==set('xyzw'):
            assert min(sum((a[k]-b[k])**2 for k in a),sum((a[k]+b[k])**2 for k in a))<1e-8,(path,a,b)
        else:
            for k in a:compare(a[k],b[k],path+'/'+k)
    elif isinstance(a,list):
        assert len(a)==len(b),(path,'length')
        for i,(x,y) in enumerate(zip(a,b)):compare(x,y,path+'/'+str(i))
    elif isinstance(a,(int,float)) and not isinstance(a,bool):assert math.isclose(a,b,rel_tol=1e-5,abs_tol=1e-4),(path,a,b)
    else:assert a==b,(path,a,b)
expected=normalize(sys.argv[1]);actual=normalize(sys.argv[2]);compare(expected,actual)
Path(sys.argv[3]).write_text(json.dumps({'status':'PASS','checkpoints':actual,'newObjectsAndUnicodeNamesVerified':True,'transformParentingAndReadbackVerified':True,'cloneAndDeferredHierarchyDestructionVerified':True,'projectPortComplete':False,'renderingEquivalent':False},ensure_ascii=False,indent=2),encoding='utf-8')
print('RUNTIME_CREATION_EXECUTION_COMPARISON_PASS')
