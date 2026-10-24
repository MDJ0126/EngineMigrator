from pathlib import Path
import json
root=Path('artifacts/core-engine-launch-tests');run=next(root.glob('ActorPort-*'))
script='''import unreal,json,math,struct,re,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parent
OUTPUT=ROOT/'native-feature-verification.json'
checks={}
def require(value,message):
 if not value:raise RuntimeError(message)
def call(actor,name,*args):
 py=re.sub(r'([A-Z]+)([A-Z][a-z])',r'\\1_\\2',name)
 py=re.sub(r'([a-z0-9])([A-Z])',r'\\1_\\2',py).lower()
 return getattr(actor,py)(*args)
def spawn(name):
 cls=unreal.load_class(None,'/Script/EMActorPort.Audit'+name)
 require(cls is not None,'Native class missing: '+name)
 actor=unreal.get_editor_subsystem(unreal.EditorActorSubsystem).spawn_actor_from_class(cls,unreal.Vector())
 require(actor is not None,'Native actor not spawned: '+name)
 return actor
def text(values):return '|'.join(format(v,'.10g') if isinstance(v,float) else str(v) for v in values)
try:
 unreal.EditorLoadingAndSavingUtils.new_blank_map(False)
 a=spawn('LoopActor');call(a,'Run')
 actual=text([a.get_editor_property('sum'),a.get_editor_property('index'),call(a,'Early'),call(a,'Overflow'),call(a,'Ordered'),call(a,'Inferred')])
 require(actual=='123|3|3|2|7|6','Loop state differs: '+actual);checks['loops']=actual
 a=spawn('SwitchActor');values=[call(a,'Pick',v) for v in [-2147483648,-1,2,3,9]];call(a,'Run');call(a,'Empty',9)
 values += [call(a,'Shared',v) for v in [0,1,2,3,9]]
 values += [a.get_editor_property('calls'),a.get_editor_property('score'),call(a,'NoDefault',9),call(a,'NoDefault',1),call(a,'DefaultOnly',4)]
 actual=text(values);require(actual=='1|2|12|13|29|9|18|11|12|0|17|68|0|7|5','Switch state differs: '+actual);checks['switch']=actual
 a=spawn('ConditionalActor');values=[call(a,'Choose',v) for v in [1,0,-1]]
 values += [a.get_editor_property('calls'),call(a,'Scalar',True),call(a,'Scalar',False),int(call(a,'BooleanChoice',True)),int(call(a,'BooleanChoice',False))]
 values += [call(a,'Nested',v) for v in [-1,0,1]];values += [call(a,'Wide',True),call(a,'Wide',False)]
 actual=text(values);require(actual=='10|-10|-10|6|1.25|2|1|0|-1|0|1|1|2.5','Scalar conditional state differs: '+actual);checks['scalarConditionals']=actual
 a=spawn('ConstantActor');values=[call(a,'Compute',9),call(a,'Compute',0),call(a,'Bounds'),call(a,'Ratio'),call(a,'Wide'),call(a,'Cross',0),call(a,'Cross',1),call(a,'Cross',9),call(a,'Guard',True),call(a,'Guard',False),a.get_editor_property('calls'),int(math.copysign(1,call(a,'Zero'))<0),struct.unpack('<I',struct.pack('<f',call(a,'Precision')))[0],call(a,'Negative')]
 actual=text(values);require(actual=='48|49|-1|2.5|3.25|7|8|0|10|-10|2|1|1066192077|-0.125','Constant state differs: '+actual);checks['constants']=actual
 def q(a,b):return (abs(a)//abs(b))*(-1 if (a<0)!=(b<0) else 1)
 def r(a,b):return a-q(a,b)*b
 a=spawn('DivisionActor');rows=[]
 for v in [-2147483648,-2147483647,-17,-1,0,1,17,2147483647]:
  values=[call(a,n,v) for n in ['Q2','QNeg2','Mod3','ModNeg3','QMax','QMin','ModMin','Compound']]
  expected=[q(v,2),q(v,-2),r(v,3),r(v,-3),q(v,2147483647),q(v,-2147483648),r(v,-2147483648),r(q(v,2),3)+r(q(v,-2),5)]
  require(values==expected,'Integer quotient/remainder differs: '+str(v));rows.append({'input':v,'values':values})
 require(call(a,'Loop')==7,'Division compound loop differs');checks['integerDivision']={'boundaryRows':rows,'loop':7}
 a=spawn('ReferenceConditional');subsystem=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
 left=subsystem.spawn_actor_from_class(unreal.Actor,unreal.Vector(100,0,0));right=subsystem.spawn_actor_from_class(unreal.Actor,unreal.Vector(200,0,0))
 a.set_editor_property('left',left);a.set_editor_property('right',right)
 def identity(value):return 'L' if value==left else 'R' if value==right else 'N' if value is None else '?'
 values=[identity(call(a,name,c)) for name in ['Pick','Optional','Reverse','Lazy','Local'] for c in [True,False]]
 values += [identity(call(a,'Nested',c,other)) for c in [True,False] for other in [True,False]]
 values += [str(a.get_editor_property('calls'))]
 require(call(a,'Empty',True) is None and call(a,'Empty',False) is None,'Target-typed null selection differs')
 actual='|'.join(values);require(actual=='L|R|L|N|N|R|L|R|L|R|L|N|R|R|2','Reference selection differs: '+actual);checks['gameObjectConditionals']=actual
 OUTPUT.write_text(json.dumps({'status':'PASS','checks':checks,'actualUnrealExecution':True,'runtimeReferenceKinds':['GameObject'],'transformConditionalRuntimeVerified':False,'uiConditionalRuntimeVerified':False,'fullProjectPortComplete':False},indent=2),encoding='utf-8')
 unreal.log('ACTUAL_GENERATED_SCRIPT_FEATURES_PASS')
except Exception:
 OUTPUT.write_text(json.dumps({'status':'FAIL','checksCompleted':checks,'error':traceback.format_exc()},indent=2),encoding='utf-8');raise
'''
(run/'probe.py').write_text(script,encoding='utf-8')
(root/'runner/Runner.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><AssemblyName>EngineMigrator.Checks</AssemblyName></PropertyGroup><ItemGroup><ProjectReference Include="../../../src/EngineMigrator.Core/EngineMigrator.Core.csproj" /></ItemGroup></Project>',encoding='utf-8')
(root/'paths.json').write_text(json.dumps({'run':str(run.resolve()),'runner':str((root/'runner/Runner.csproj').resolve())},indent=2),encoding='utf-8')
