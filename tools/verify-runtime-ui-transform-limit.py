"""Record observed proxy differences as unsupported, never as gameplay parity."""
from pathlib import Path
import json,sys
def read(path):
 raw=Path(path).read_bytes();data=json.loads(raw.decode('utf-16' if raw.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'));assert data['status']=='CAPTURED';return data
def scale(data):
 frame=next(f for f in data['frames'] if f.get('ui'))
 owner=frame['ui'][0]['owner'];return next(o['scale'] for o in frame['objects'] if o['id']==owner)
unity=scale(read(sys.argv[1]));native=scale(read(sys.argv[2]));assert unity!=native
Path(sys.argv[3]).write_text(json.dumps({'status':'KNOWN_UNSUPPORTED','unityObservedRootScale':unity,'unrealObservedProxyScale':native,'uiTransformAPISupported':False,'sourceCapture':sys.argv[1],'nativeCapture':sys.argv[2],'projectPortComplete':False},ensure_ascii=False,indent=2),encoding='utf-8')
print('UI_TRANSFORM_DIFFERENCE_RECORDED_AS_UNSUPPORTED')
