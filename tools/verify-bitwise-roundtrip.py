"""Compare actual Unity/Unreal and managed roundtrip captures with an independent oracle."""
from pathlib import Path
import datetime
import hashlib
import json

root=Path(__file__).resolve().parent.parent
art=root/'artifacts'
run=Path(json.loads((art/'core-engine-launch-tests/paths.json').read_text())['run'])
output=art/'bitwise-statement-check/roundtrip-verification.json'

def require(good,message):
 if not good:raise RuntimeError(message)

def sha(path):
 with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest().upper()

def signed(value):return ((value+2147483648)%4294967296)-2147483648

try:
 paths=[art/'bitwise-statement-check/managed-rows.json',art/'runtime-ui-api-tests/bitwise-unity-execution.json',run/'bitwise-native-feature-verification.json']
 managed,unity,native=[json.loads(p.read_text(encoding='utf-8-sig')) for p in paths]
 require(unity['status']==native['status']=='PASS','Actual engine capture failed')
 expected=[]
 for value in [-2147483648,-2147483647,-17,-1,0,1,17,2147483647]:
  for count in [-2147483648,-65,-33,-32,-31,-1,0,1,2,30,31,32,33,63,64,2147483647]:
   n=count&31;compound=signed(signed((value|32)^17)<<n)>>n
   expected.append([value,count,signed(value&count),signed(value|count),signed(value^count),signed(~value),signed(value<<n),value>>n,compound,compound,compound])
 require(len(managed)==len(unity['runs'])==2,'Original/restored captures missing')
 captures=managed+unity['runs']+[native['checks']['bitwise']['rows']]
 for rows in captures:
  require(len(rows)==128 and all(len(row)==11 and all(type(v) is int for v in row) for row in rows),'Malformed boundary rows')
  require(rows==expected,'Boundary values differ from independent oracle')
 require(unity['loops']==[31,31] and native['checks']['bitwise']['loop']==31,'Signed shift loop differs')
 require(native['checks']['nativeReferenceComponents']=='PASS:28','Previous reference regression failed')
 headers=[]
 for p in (art/'bitwise-statement-check').glob('*.h'):
  actual=run/'Project/Plugins/EMActorPort/Source/EMActorPort/Public'/p.name
  require(sha(p)==sha(actual),'Compiled header differs from current generator: '+p.name)
  headers.append({'path':str(actual),'sha256':sha(actual)})
 require(len(headers)==3,'Generated actor classes missing')
 baseline=json.loads((art/'user-project-baseline/original-hashes.json').read_text(encoding='utf-8-sig'))
 require(all(Path(p).is_file() and sha(p)==h.upper() for p,h in baseline.items()),'Original project changed')
 response=run/'Project/Plugins/EMActorPort/Intermediate/Build/Win64/x64/UnrealEditor/Development/EMActorPort/EMActorPort.Shared.rsp'
 require('/Ox' in response.read_text(),'Expected actual optimized compiler configuration missing')
 result={'status':'PASS','boundaryRowsPerCapture':128,'resultsPerRow':9,'comparedCaptures':['ManagedOriginal','ManagedRestored','UnityOriginal','UnityRestored','UnrealNative'],'signedShiftLoop':31,'generatedHeaders':headers,'captureHashes':{str(p):sha(p) for p in paths},'compilerOptimization':'MSVC /Ox','compilerResponseSha256':sha(response),'originalFilesUnchanged':len(baseline),'actualUnityExecution':True,'actualUnrealExecution':True,'wholeProjectComplete':False,'verifiedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
except Exception as error:
 output.write_text(json.dumps({'status':'FAIL','error':str(error)},indent=2),encoding='utf-8')
 raise
output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('BITWISE_ACTUAL_ENGINE_ROUNDTRIP_PASS: 5 captures x 128 rows x 9 results')
