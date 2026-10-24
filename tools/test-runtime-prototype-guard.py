"""Exercise edit detection without requiring the editor; native negative test is separate."""
import importlib.util,json,sys,types
from pathlib import Path

class Value:
    def __init__(self, **values): self.values=values
    def get_editor_property(self,key): return self.values[key]
    def quaternion(self): return self
class Actor:
    tags=["EMRuntimeTemplate"]
    def __init__(self): self.x=0;self.count=1;self.parent=None
    def get_path_name(self): return "Map.Actor"
    def get_class(self): return self
    def get_attach_parent_actor(self): return self.parent
    def get_actor_location(self): return Value(x=self.x,y=0,z=0)
    def get_actor_rotation(self): return Value(x=0,y=0,z=0,w=1)
    def get_actor_scale3d(self): return Value(x=1,y=1,z=1)
    def get_components_by_class(self,cls): return []
    def get_component_by_class(self,cls): return None
    def get_editor_property(self,name): return self.count
actor=Actor();world=object();metadata={}
class Assets:
    @staticmethod
    def get_metadata_tag(obj,key):
        if obj is actor and key=="EngineMigratorBehaviour":return json.dumps({"source":{"fields":[{"name":"Count","kind":"int"}]}})
        return metadata.get((obj,key),"")
    @staticmethod
    def set_metadata_tag(obj,key,value):metadata[obj,key]=value
subsystem=types.SimpleNamespace(get_all_level_actors=lambda:[actor])
sys.modules['unreal']=types.SimpleNamespace(EditorAssetLibrary=Assets,ActorComponent=object,StaticMeshComponent=object,EditorLoadingAndSavingUtils=types.SimpleNamespace(save_map=lambda *_:True))
spec=importlib.util.spec_from_file_location('guard',Path(__file__).resolve().parents[1]/'adapters/unreal/runtime_prototype_guard.py');guard=importlib.util.module_from_spec(spec);spec.loader.exec_module(guard)
guard.persist(world,subsystem,"Map");guard.verify(world,subsystem)
for field,value in (("x",3),("count",2),("parent",actor)):
    original=getattr(actor,field);setattr(actor,field,value)
    try:guard.verify(world,subsystem)
    except RuntimeError as error:assert "changed independently" in str(error)
    else:raise AssertionError("Missed edit: "+field)
    setattr(actor,field,original)
guard.verify(world,subsystem)
metadata.clear()
try:guard.verify(world,subsystem)
except RuntimeError as error:assert "baseline missing" in str(error)
else:raise AssertionError("Missing prototype baseline accepted")
print("PASS unchanged prototype; transform, field and parent edits rejected; missing baseline rejected")
