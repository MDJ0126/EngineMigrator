"""Prepare owned Unity/game-mode probes. Engine processes use only the common runner."""
from pathlib import Path
import json,re,shutil
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/dynamic-ui-check';home.mkdir(exist_ok=True)
unity=root/'artifacts/runtime-ui-api-tests/UnitySource'
core=Path(json.loads((root/'artifacts/core-engine-launch-tests/paths.json').read_text())['run'])
source=(root/'tests/EngineMigrator.Checks/Fixtures/dynamic-ui.cs.txt').read_text()
fields=[f for f in re.findall(r'public bool (\w+);',source) if f!='Existing']
(home/'fields.json').write_text(json.dumps(fields))
folder=unity/'Assets/EngineMigratorDynamicUIProbe';(folder/'Editor').mkdir(parents=True,exist_ok=True)
(folder/'AuditDynamicUI.cs').write_text(source)
if (home/'restored.cs').exists():(folder/'RestoredAuditDynamicUI.cs').write_text((home/'restored.cs').read_text().replace('AuditDynamicUI','RestoredAuditDynamicUI'))
observer='''using System;using System.IO;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public class EMDynamicUIObserver:MonoBehaviour{public Component target;private List<string> rows=new List<string>();private int prior;
 void LateUpdate(){try{var t=target.GetType();int f=(int)t.GetField("Frame").GetValue(target);if(f==prior)return;prior=f;var row=new List<int>{f};foreach(var n in FIELDS)row.Add((bool)t.GetField(n).GetValue(target)?1:0);rows.Add("["+string.Join(",",row)+"]");if(f>=10){File.WriteAllText(Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_OUTPUT"),"{\\"status\\":\\"CAPTURED\\",\\"rows\\":["+string.Join(",",rows)+"]}");EditorApplication.Exit(0);}}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
public static class EMDynamicUIProbe{public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var owner=new GameObject("DynamicUIOwner");var t=typeof(AuditDynamicUI).Assembly.GetType(Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_CLASS"));var target=owner.AddComponent(t);t.GetField("Owner").SetValue(target,owner);new GameObject("Observer").AddComponent<EMDynamicUIObserver>().target=target;EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;var deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup>deadline)EditorApplication.Exit(2);};EditorApplication.EnterPlaymode();}}
'''.replace('FIELDS','new[]{'+','.join(json.dumps(f) for f in fields)+'}')
(folder/'Editor/EMDynamicUIProbe.cs').write_text(observer)
text=(folder/'Editor/EMDynamicUIProbe.cs').read_text()
text=text.replace('var t=typeof(AuditDynamicUI)', 'bool existing=Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_EXISTING")=="1";if(existing){owner.AddComponent<Canvas>();owner.AddComponent<UnityEngine.UI.Button>();}var t=typeof(AuditDynamicUI)')
text=text.replace('t.GetField("Owner").SetValue(target,owner);','t.GetField("Owner").SetValue(target,owner);t.GetField("Existing").SetValue(target,existing);')
(folder/'Editor/EMDynamicUIProbe.cs').write_text(text)
native='''
namespace EMDynamicUIFixture{static FString Output;static TWeakObjectPtr<AActor> Target;static bool Started=false;static TArray<TSharedPtr<FJsonValue>> Rows;
static void Observe(UWorld* World,ELevelTick,float){if(Output.IsEmpty()){FParse::Value(FCommandLine::Get(),TEXT("EMDynamicUIProbe="),Output);if(Output.IsEmpty())return;}if(World->WorldType!=EWorldType::Game)return;
 if(!Started){Started=true;auto* Class=LoadObject<UClass>(nullptr,TEXT("/Script/EMActorPort.AuditDynamicUI"));check(Class);auto* Owner=World->SpawnActor<AStaticMeshActor>();auto* Actor=World->SpawnActorDeferred<AActor>(Class,FTransform::Identity);check(Owner&&Actor);FindFProperty<FObjectPropertyBase>(Class,TEXT("EM_Field_Owner"))->SetObjectPropertyValue_InContainer(Actor,Owner);Actor->Tags.Add(TEXT("EMComponentOrder:0"));Actor->AttachToActor(Owner,FAttachmentTransformRules::KeepWorldTransform);Actor->FinishSpawning(FTransform::Identity);Target=Actor;return;}
 auto* Actor=Target.Get();check(Actor);int32 Frame=FindFProperty<FIntProperty>(Actor->GetClass(),TEXT("Frame"))->GetPropertyValue_InContainer(Actor);if(Frame<1)return;TArray<TSharedPtr<FJsonValue>> Row;Row.Add(MakeShared<FJsonValueNumber>(Frame));for(const TCHAR* Name:{FIELDS})Row.Add(MakeShared<FJsonValueNumber>(FindFProperty<FBoolProperty>(Actor->GetClass(),Name)->GetPropertyValue_InContainer(Actor)?1:0));Rows.Add(MakeShared<FJsonValueArray>(Row));if(Frame>=10){auto Result=MakeShared<FJsonObject>();Result->SetStringField(TEXT("status"),TEXT("CAPTURED"));Result->SetArrayField(TEXT("rows"),Rows);FString Text;auto Writer=TJsonWriterFactory<>::Create(&Text);FJsonSerializer::Serialize(Result,Writer);FFileHelper::SaveStringToFile(Text,*Output);FPlatformMisc::RequestExit(false);}}
struct FInstall{FInstall(){FWorldDelegates::OnWorldTickEnd.AddStatic(&Observe);}};static FInstall Install;}
'''.replace('FIELDS',','.join('TEXT('+json.dumps(f)+')' for f in fields))
# Preserve the core module implementation without accumulating old observers.
(home/'native-observer.cpp.txt').write_text(native)
native=native.replace('auto* Actor=World->SpawnActorDeferred', 'int32 Existing=0;FParse::Value(FCommandLine::Get(),TEXT("EMDynamicUIExisting="),Existing);if(Existing)Owner->Tags={TEXT("EMUI:Canvas"),TEXT("EMUI:Button")};auto* Actor=World->SpawnActorDeferred')
native=native.replace('Actor->Tags.Add(TEXT("EMComponentOrder:0"));','FindFProperty<FBoolProperty>(Class,TEXT("Existing"))->SetPropertyValue_InContainer(Actor,Existing!=0);Actor->Tags.Add(TEXT("EMComponentOrder:0"));')
native=native.replace('if(Frame<1)return;', 'if(Frame<1)return;AActor* EM_Owner=Cast<AActor>(FindFProperty<FObjectPropertyBase>(Actor->GetClass(),TEXT("EM_Field_Owner"))->GetObjectPropertyValue_InContainer(Actor));AActor* EM_Button=Cast<AActor>(FindFProperty<FObjectPropertyBase>(Actor->GetClass(),TEXT("FirstButton"))->GetObjectPropertyValue_InContainer(Actor));FString EM_Tags; if(EM_Owner)for(const FName& Tag:EM_Owner->Tags)EM_Tags+=Tag.ToString()+TEXT(",");UE_LOG(LogTemp,Display,TEXT("DynamicUIDiagnostic Frame=%d Owner=%s Button=%s Tags=%s"),Frame,*GetNameSafe(EM_Owner),*GetNameSafe(EM_Button),*EM_Tags);')
(home/'native-observer.cpp.txt').write_text(native)
runner=home/'runner';runner.mkdir(exist_ok=True)
(runner/'Runner.csproj').write_bytes((root/'artifacts/ui-equality-check/destruction-runner/Runner.csproj').read_bytes())
code=(root/'artifacts/owner-field-check/destruction-runner/Program.cs').read_text()
code=code.replace('owner-field-check','dynamic-ui-check').replace('AuditOwnerCollision','AuditDynamicUI').replace('owner-field.cs.txt','dynamic-ui.cs.txt').replace('destruction-restored.cs','restored.cs').replace('EM_OWNER_FIELD','EM_DYNAMIC_UI').replace('EMOwnerField','EMDynamicUI')
code=code.replace('destruction-native','native').replace('destruction-original','original').replace('destruction-restored','restored').replace('"destruction-"+args[0]','args[0]')
code=code.replace('if(args[0]=="prepare")','var existing=args[0].EndsWith("-static",StringComparison.Ordinal);var suffix=existing?"-static":"";if(existing)args[0]=args[0][..^7];\nif(args[0]=="prepare")')
code=code.replace('"native.json"','"native"+suffix+".json"').replace('"native.log"','"native"+suffix+".log"').replace('args[0]+".json"','args[0]+suffix+".json"').replace('args[0]+".log"','args[0]+suffix+".log"')
code=code.replace('"-EMDynamicUIProbe="+output,','"-EMDynamicUIProbe="+output,"-EMDynamicUIExisting="+(existing?"1":"0"),')
code=code.replace('var oldClass=Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_CLASS");','var oldClass=Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_CLASS");var oldExisting=Environment.GetEnvironmentVariable("EM_DYNAMIC_UI_EXISTING");Environment.SetEnvironmentVariable("EM_DYNAMIC_UI_EXISTING",existing?"1":"0");')
code=code.replace('Environment.SetEnvironmentVariable("EM_DYNAMIC_UI_CLASS",oldClass);','Environment.SetEnvironmentVariable("EM_DYNAMIC_UI_CLASS",oldClass);Environment.SetEnvironmentVariable("EM_DYNAMIC_UI_EXISTING",oldExisting);')
# Core host source is only changed during explicit prepare, before native build.
needle='if(args[0]=="prepare"){'
code=code.replace(needle,needle+'File.WriteAllText(Path.Combine(core,"Project/Source/EMPortHost/EMPortHost.cpp"),File.ReadAllText("artifacts/ui-equality-check/core-host-before-ui-destruction.cpp.txt")+File.ReadAllText(Path.Combine(home,"native-observer.cpp.txt")));')
(runner/'Program.cs').write_text(code)
print('DYNAMIC_UI_PROBES_PREPARED_NO_ENGINE_STARTED')
