using EngineMigrator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class ActorChecks
{
    internal static void Run(Action<bool,string> check)
    {
        const string source="""
using UnityEngine;
public class PortBase : MonoBehaviour
{
 public double Health = 100.0;
 public double Position = 0.0;
 public double AwakeCount = 0.0;
 public double StartCount = 0.0;
 public double TickCount = 0.0;
 public double DestroyCount = 0.0;
 protected virtual void Awake() { AwakeCount += 1.0; }
 protected virtual void Start() { StartCount += 1.0; Health -= 5.0; }
 protected virtual void Update() { TickCount += 1.0; Position += Time.deltaTime * 2.0; }
 protected virtual void OnDestroy() { DestroyCount += 1.0; }
 public virtual double ApplyDamage(double amount) { Health -= amount; return Health; }
}
public class PortDerived : PortBase
{
 public double Bonus = 3.0;
 protected override void Awake() { base.Awake(); Bonus += 1.0; }
 protected override void Start() { base.Start(); Health += Bonus; }
 protected override void Update() { base.Update(); Position += Time.deltaTime; }
 protected override void OnDestroy() { base.OnDestroy(); DestroyCount += 2.0; }
 public override double ApplyDamage(double amount) { return base.ApplyDamage(amount * 0.5); }
}
""";
        var cpp=ActorScripts.FromUnity(source);check(cpp.Count==2&&cpp.Single(f=>f.Name=="PortDerived.h").Code.Contains("Super::EM_Update(DeltaSeconds)"),"MonoBehaviour inheritance/state/events emit native Actor classes");
        check(cpp.Single(f=>f.Name=="PortBase.h").Code.Contains("CreateDefaultSubobject<USceneComponent>")&&!cpp.Single(f=>f.Name=="PortDerived.h").Code.Contains("CreateDefaultSubobject"),"Only the inheritance root owns the scene attachment component");
        var restored=ActorScripts.ToUnity(cpp);check(restored.Count==2&&restored.Any(f=>f.Code.Contains("base.Update()")),"Actual generated Actor bodies restore MonoBehaviour overrides/parent calls");
        var legacy=cpp.Select(f=>f with{Code=f.Code.Replace("#include \"Components/SceneComponent.h\"\n","").Replace(" RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT(\"EM_Root\"));","").Replace("UPROPERTY(EditAnywhere, Category=\"EngineMigrator\")","UPROPERTY()")}).ToArray();
        check(ActorScripts.ToUnity(legacy).Count==2,"Earlier generated Actor headers remain reversible after scene attachment support");
        var declarations="namespace UnityEngine { public class MonoBehaviour {} public static class Time { public static float deltaTime; } }\n"+string.Join("\n",restored.Select(f=>f.Code.Replace("using UnityEngine;","")));
        var compilation=CSharpCompilation.Create("ActorRoundtrip",[CSharpSyntaxTree.ParseText("using UnityEngine;\n"+declarations)],[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        check(!compilation.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error),"Restored lifecycle class hierarchy compiles");
        var extra=cpp.Select(f=>f.Name=="PortBase.h"?f with { Code=f.Code.Replace("};","double Unmapped = 3.0;\n};") }:f).ToArray();
        var rejected=false;try{ActorScripts.ToUnity(extra);}catch(NotSupportedException){rejected=true;}check(rejected,"Reverse Actor conversion rejects unmapped extra state");
        var renamedField=cpp.Select(f=>f with{Code=f.Code.Replace("double Health =","double EM_arg_Health =")}).ToArray();rejected=false;try{ActorScripts.ToUnity(renamedField);}catch(NotSupportedException){rejected=true;}check(rejected,"Native field declarations must match metadata despite internal parameter-name normalization");
        var renamedMethod=cpp.Select(f=>f with{Code=f.Code.Replace("double ApplyDamage(","double EM_arg_ApplyDamage(")}).ToArray();rejected=false;try{ActorScripts.ToUnity(renamedMethod);}catch(NotSupportedException){rejected=true;}check(rejected,"Native method declarations must match metadata despite internal variable-name normalization");
        rejected=false;try{ActorScripts.FromUnity(source.Replace("OnDestroy","OnGUI"));}catch(NotSupportedException){rejected=true;}check(rejected,"Unsupported lifecycle message fails without fake conversion");
        rejected=false;try{ActorScripts.FromUnity(source.Replace("Health += Bonus;","Health = ApplyDamage(1.0) + Health;"));}catch(NotSupportedException){rejected=true;}check(rejected,"Stateful expression evaluation order differences are blocked");
        rejected=false;try{ActorScripts.FromUnity(source.Replace("Health += Bonus;","Health += ApplyDamage(1.0);"));}catch(NotSupportedException){rejected=true;}check(rejected,"Stateful compound assignment ordering differences are blocked");
        const string intSource="""
using UnityEngine;
public class IntActor : MonoBehaviour {
 public int Count = 2147483646;
 public int Product = 123456789;
 public bool Flag = true;
 protected virtual void Awake() { Count++; }
 protected virtual void Start() { Count += 3; Product *= 27; }
 protected virtual void Update() { Count++; if (Count < 0) Product -= 3; }
 protected virtual void OnDestroy() { Count--; Flag = false; }
 public int Mix(int amount) { int value = amount * 7; return Count + value; }
}
""";
        var intCpp=ActorScripts.FromUnity(intSource);var intBack=ActorScripts.ToUnity(intCpp).Single().Code;
        check(intCpp.Single().Code.Contains("uint32 EM_Bits=")&&intCpp.Single().Code.Contains("int32 Mix(int32 EM_arg_amount)"),"Integer Actor state uses defined unsigned wrapping and reflected int32 types");
        static System.Reflection.Assembly CompileActor(string code,string? runtimeStub=null)
        {
            var input="using UnityEngine;\n"+(code.Contains("using UnityEngine.UI;")?"using UnityEngine.UI;\n":"")+(code.Contains("using UnityEngine.SceneManagement;")?"using UnityEngine.SceneManagement;\n":"")+(runtimeStub??"namespace UnityEngine { public class SerializeField:System.Attribute {} public class MonoBehaviour {} public class GameObject {} public class Transform {} }")+"\n"+code.Replace("using UnityEngine;","").Replace("using UnityEngine.UI;","").Replace("using UnityEngine.SceneManagement;","");
            var assembly=CSharpCompilation.Create("IntegerActor_"+Guid.NewGuid().ToString("N"),[CSharpSyntaxTree.ParseText(input)],[MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));using var bytes=new MemoryStream();var result=assembly.Emit(bytes);if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics));return System.Reflection.Assembly.Load(bytes.ToArray());
        }
        const string keywordSource="using UnityEngine;public class KeywordLocals:MonoBehaviour{public int Evaluate(int template){var friend=template+2;return friend;}public bool Same(GameObject template,GameObject other){return template==other;}}";
        var keywordFiles=ActorScripts.FromUnity(keywordSource);var keywordBack=ActorScripts.ToUnity(keywordFiles).Single().Code;var keywordResults=new List<object?>();foreach(var code in new[]{keywordSource,keywordBack}){var asm=CompileActor(code,ActorScripts.ValidationStub);var t=asm.GetType("KeywordLocals")!;keywordResults.Add(t.GetMethod("Evaluate")!.Invoke(Activator.CreateInstance(t),[7]));}check(keywordResults.All(v=>Equals(v,9))&&keywordBack.Contains("int template")&&keywordFiles.Single().Code.Contains("EM_local_friend"),"C++ keyword local/argument names preserve source identifiers and actual managed results through safe prefixes");
        const string ownerSource="using UnityEngine;public class OwnerBase:MonoBehaviour{public int Owner=3;public int Read(int Owner){return Owner+this.Owner;}public void Copy(OwnerBase other){other.Owner=this.Owner;}}public class OwnerChild:OwnerBase{public int Inherited(){return Owner;}}";
        var ownerCpp=ActorScripts.FromUnity(ownerSource);var ownerBack=string.Join("\n",ActorScripts.ToUnity(ownerCpp).Select(f=>f.Code.Replace("using UnityEngine;","")));
        check(ownerCpp[0].Code.Contains("int32 EM_Field_Owner = 3;")&&ownerCpp[0].Code.Contains("this->EM_Field_Owner")&&ownerCpp[0].Code.Contains("EM_arg_Owner")&&ownerCpp[0].Code.Contains("TEXT(\"EM_Field_Owner\")"),"Owner field collision maps declarations and reflected access without renaming shadowed parameters");
        var ownerStates=new List<string>();foreach(var code in new[]{ownerSource,"using UnityEngine;"+ownerBack}){var asm=CompileActor(code);var t=asm.GetType("OwnerChild")!;var a=Activator.CreateInstance(t)!;var b=Activator.CreateInstance(t)!;t.GetField("Owner")!.SetValue(a,17);t.GetMethod("Copy")!.Invoke(a,[b]);ownerStates.Add(t.GetMethod("Read")!.Invoke(a,[4])+"|"+t.GetMethod("Inherited")!.Invoke(b,null));}
        check(ownerStates.SequenceEqual(new[]{"21|17","21|17"}),"Owner source and restored execution preserve inheritance, parameter shadowing and other-component writes");
        var ownerTamper=false;try{ActorScripts.ToUnity(ownerCpp.Select(f=>f with{Code=f.Code.Replace("int32 EM_Field_Owner =","int32 Owner =")}).ToArray());}catch(NotSupportedException){ownerTamper=true;}check(ownerTamper,"Owner field metadata cannot silently bind AActor.Owner during reverse conversion");
        var dynamicUiFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/dynamic-ui.cs.txt"));var dynamicUiBack=ActorScripts.ToUnity(dynamicUiFiles).Single().Code;
        check(dynamicUiFiles.Single().Code.Contains("EM_UiAddCanvas(EM_NativeGameObject(EM_Field_Owner))")&&dynamicUiFiles.Single().Code.Contains("EMUIProxy")&&dynamicUiBack.Contains("AddComponent<Canvas>()")&&dynamicUiBack.Contains("AddComponent<Button>()"),"Dynamic Canvas/Button adds use per-instance identity and restore generic source calls");
        var dynamicUiTamper=false;try{ActorScripts.ToUnity(dynamicUiFiles.Select(f=>f with{Code=f.Code.Replace("EM_Value->Destroy();","EM_Value->SetActorHiddenInGame(true);")}).ToArray());}catch(NotSupportedException){dynamicUiTamper=true;}check(dynamicUiTamper,"Dynamic UI deletion helpers cannot silently lose stale-reference invalidation");
        var uiNameFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/ui-name.cs.txt"));var uiNameBack=ActorScripts.ToUnity(uiNameFiles).Single().Code;
        check(uiNameFiles.Single().Code.Contains("\"uiOwnerNames\":true")&&uiNameFiles.Single().Code.Contains("EM_UiSetObjectName(EM_NativeGameObject(Panel)")&&uiNameBack.Contains("Panel.name"),"UI name writes target the logical owner and restore the original component property");
        var legacyUiNames=uiNameFiles.Select(f=>f with{Code=f.Code.Replace(UiNamePorting.Helpers,"").Replace(",\"uiOwnerNames\":true","").Replace("EM_UiSetObjectName(","EM_SetObjectName(")}).ToArray();
        check(ActorScripts.ToUnity(legacyUiNames).Single().Code.Contains("Panel.name"),"Earlier raw-owner UI naming headers remain readable with their original backend metadata");
        var uiNameTamper=false;try{ActorScripts.ToUnity(uiNameFiles.Select(f=>f with{Code=f.Code.Replace("EM_UiSetObjectName(EM_NativeGameObject(Panel)","EM_SetObjectName(EM_NativeGameObject(Panel)")}).ToArray());}catch(NotSupportedException){uiNameTamper=true;}check(uiNameTamper,"New UI name writes cannot silently downgrade to proxy-object naming");
        var uiNameMissing=false;try{ActorScripts.ToUnity(uiNameFiles.Select(f=>f with{Code=f.Code.Replace(UiNamePorting.Helpers,"")}).ToArray());}catch(NotSupportedException){uiNameMissing=true;}check(uiNameMissing,"UI owner-name backend requires its runtime implementation");
        var scaleSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/time-scale.cs.txt");var scaleFiles=ActorScripts.FromUnity(scaleSource);var scaleBack=ActorScripts.ToUnity(scaleFiles).Single(f=>f.Name=="TimeScaleActor.cs").Code;
        var scaleStates=new List<string>();foreach(var code in new[]{scaleSource,scaleBack}){var asm=CompileActor(code,ActorScripts.ValidationStub);var t=asm.GetType("TimeScaleActor")!;var actor=Activator.CreateInstance(t)!;var time=asm.GetType("UnityEngine.Time")!;time.GetField("timeScale")!.SetValue(null,1f);time.GetField("deltaTime")!.SetValue(null,.01f);var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;t.GetMethod("Start",flags)!.Invoke(actor,null);var values=new List<string>();for(int frame=0;frame<22;frame++){t.GetMethod("Update",flags)!.Invoke(actor,null);values.Add(t.GetField("ScaleBefore")!.GetValue(actor)+"/"+t.GetField("ScaleAfter")!.GetValue(actor));}scaleStates.Add(string.Join("|",values));}
        check(scaleStates.Count==2&&scaleStates[0]==scaleStates[1]&&scaleBack.Contains("Time.timeScale=0.0f"),"Time-scale read/write roundtrip preserves pause, resume and changing rates in executed managed source");
        check(ActorScripts.ToUnity(scaleFiles.Select(f=>f with{Code=f.Code.ReplaceLineEndings("\r\n")}).ToArray()).Count==2,"Time-scale helper validation accepts equivalent Windows header line endings");
        var oldScaleFiles=scaleFiles.Select(f=>f with{Code=f.Code.Replace(TimeScalePorting.Runtime,TimeScalePorting.LegacyRuntime).Replace(",\"timeScaleClock\":2","")}).ToArray();
        check(ActorScripts.ToUnity(oldScaleFiles).Count==2,"Earlier published time-scale headers restore through their original runtime contract");
        var mixedScale=false;try{ActorScripts.ToUnity(new[]{scaleFiles[0],oldScaleFiles[1]});}catch(NotSupportedException){mixedScale=true;}check(mixedScale,"Mixed time-scale runtime definitions are rejected before an inconsistent native build");
        var unknownScale=false;try{ActorScripts.ToUnity(scaleFiles.Select(f=>f with{Code=f.Code.Replace("\"timeScaleClock\":2","\"timeScaleClock\":42")}).ToArray());}catch(NotSupportedException){unknownScale=true;}check(unknownScale,"Unknown time-scale runtime versions fail explicitly");
        check(TimeScalePorting.NeedsRuntime(System.Text.Json.Nodes.JsonNode.Parse("{\"hasTiming\":true,\"timing\":{\"timeScale\":0}}"))&&TimeScalePorting.NeedsRuntime(System.Text.Json.Nodes.JsonNode.Parse("{\"hasTiming\":true,\"timing\":{\"timeScale\":0.5}}"))&&!TimeScalePorting.NeedsRuntime(System.Text.Json.Nodes.JsonNode.Parse("{\"hasTiming\":true,\"timing\":{\"timeScale\":1}}")),"Non-default project timing requests runtime bootstrap even without user scripts");
        var scaleTamper=false;try{ActorScripts.ToUnity(scaleFiles.Select(f=>f with{Code=f.Code.Replace("Settings->MinGlobalTimeDilation=0.0f;","")}).ToArray());}catch(NotSupportedException){scaleTamper=true;}check(scaleTamper,"Time-scale backend cannot silently lose exact zero-rate support");
        var scaleMissing=false;try{ActorScripts.ToUnity(scaleFiles.Select(f=>f with{Code=f.Code.Replace(",\"timeScale\":true","")}).ToArray());}catch(NotSupportedException){scaleMissing=true;}check(scaleMissing,"Time-scale runtime requires explicit conversion metadata");
        var scaleCompound=false;try{ActorScripts.FromUnity("using UnityEngine;public class BadScale:MonoBehaviour{public void Set(){Time.timeScale*=.5f;}}");}catch(NotSupportedException){scaleCompound=true;}check(scaleCompound,"Unsupported compound time-scale evaluation is rejected explicitly");
        var lateSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/late-update.cs.txt");var lateFiles=ActorScripts.FromUnity(lateSource);var lateBack=ActorScripts.ToUnity(lateFiles);var lateStates=new List<string>();foreach(var code in new[]{lateSource,string.Join("\n",lateBack.Select(f=>f.Code.Replace("using UnityEngine;","")))}){var asm=CompileActor(code,ActorScripts.ValidationStub);var time=asm.GetType("UnityEngine.Time")!;time.GetField("deltaTime")!.SetValue(null,.01f);var ledger=Activator.CreateInstance(asm.GetType("LateLedger")!)!;var t=asm.GetType("LateWorker")!;var worker=Activator.CreateInstance(t)!;t.GetField("Ledger")!.SetValue(worker,ledger);foreach(var ev in new[]{"Start","Update","LateUpdate"})t.GetMethod(ev,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(worker,null);lateStates.Add(t.GetField("Updates")!.GetValue(worker)+"|"+t.GetField("Lates")!.GetValue(worker)+"|"+t.GetField("DerivedLates")!.GetValue(worker)+"|"+ledger.GetType().GetField("Code")!.GetValue(ledger));}check(lateStates.Count==2&&lateStates[0]==lateStates[1]&&lateStates[0]=="1|1|1|32740","Late source/restored execution preserves update-before-late ordering and explicit inherited parent calls");
        var lateMissing=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.Helpers,"")}).ToArray());}catch(NotSupportedException){lateMissing=true;}check(lateMissing,"Late callbacks require the verified lifecycle runtime");
        var latePhaseTamper=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace("OnWorldPostActorTick.AddStatic(&Tick)","OnWorldPreActorTick.AddStatic(&Tick)")}).ToArray());}catch(NotSupportedException){latePhaseTamper=true;}check(latePhaseTamper,"Late callback phase cannot silently change to pre-update dispatch");
        var lateGraph=false;try{ActorBlueprintGraphs.FromUnity("using UnityEngine;public class LateGraph:MonoBehaviour{public int Value;protected virtual void LateUpdate(){Value++;}}");}catch(NotSupportedException){lateGraph=true;}check(lateGraph,"LateUpdate routes to native phase dispatch instead of an unbound Blueprint custom event");
        check(lateFiles.Single(f=>f.Name=="LateBase.h").Code.Contains("bool EM_RuntimeLateEligible()"),"Late phase eligibility UFUNCTION is emitted across source newline formats");
        var legacyLate=lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.Runtime,LateUpdatePorting.LegacyRuntime).Replace(LateUpdatePorting.Helpers,LateUpdatePorting.LegacyHelpers).Replace(LateUpdatePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(Path.GetFileNameWithoutExtension(f.Name))).Replace(LateUpdatePorting.EnableStamp,"").Replace(LateUpdatePorting.TickOrder,LateUpdatePorting.PreviousTickOrder).Replace(LateUpdatePorting.BeforeStart,"const bool EM_BeforeUpdate=!GetWorld()->bInTick;").Replace(LateUpdatePorting.BeforeDestroy,"const bool EM_NextPre=EM_IsPostStartDispatch();").Replace(",\"lateClock\":3","")}).ToArray();
        check(ActorScripts.ToUnity(legacyLate).Count==5,"Published first-version LateUpdate headers retain reverse compatibility");
        var lateStartTamper=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(Path.GetFileNameWithoutExtension(f.Name)))}).ToArray());}catch(NotSupportedException){lateStartTamper=true;}check(lateStartTamper,"Late-created component scheduling cannot be downgraded while retaining third-version metadata");
        var lateDestroyTamper=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.BeforeDestroy,"const bool EM_NextPre=EM_IsPostStartDispatch();")}).ToArray());}catch(NotSupportedException){lateDestroyTamper=true;}check(lateDestroyTamper,"Late destruction scheduling requires the validated next-frame pre-update commit");
        var mutationFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/late-mutation.cs.txt"));var mutationBack=ActorScripts.ToUnity(mutationFiles);check(mutationBack.Count==3&&mutationBack.Single(f=>f.Name=="LateMutationController.cs").Code.Contains("LateUpdate()"),"Late mutation fixture preserves native phase and all dynamic creation, cloning, activation and destruction bodies through reverse conversion");
        var lateUnknown=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace("\"lateClock\":3","\"lateClock\":77")}).ToArray());}catch(NotSupportedException){lateUnknown=true;}check(lateUnknown,"Unknown LateUpdate runtime metadata is rejected instead of guessing lifecycle semantics");
        var lateMixed=false;try{ActorScripts.ToUnity(legacyLate.Take(1).Concat(lateFiles.Skip(1)).ToArray());}catch(NotSupportedException){lateMixed=true;}check(lateMixed,"Mixed incompatible LateUpdate runtime versions cannot silently select the first header's phase policy");
        var previousLate=lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.Runtime,LateUpdatePorting.PreviousRuntime).Replace(LateUpdatePorting.Helpers,LateUpdatePorting.PreviousHelpers).Replace(LateUpdatePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(Path.GetFileNameWithoutExtension(f.Name)).Replace("const bool EM_BeforeUpdate=!GetWorld()->bInTick;",LateUpdatePorting.BeforeStart)).Replace(LateUpdatePorting.EnableStamp,"").Replace(LateUpdatePorting.TickOrder,LateUpdatePorting.PreviousTickOrder).Replace("\"lateClock\":3","\"lateClock\":2")}).ToArray();check(ActorScripts.ToUnity(previousLate).Count==5,"Published second-version LateUpdate headers remain reversible after phase queue expansion");
        var lateEnableTamper=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.EnableStamp,"")}).ToArray());}catch(NotSupportedException){lateEnableTamper=true;}check(lateEnableTamper,"Removing the same-phase reactivation marker cannot silently change current-frame LateUpdate membership");
        var phaseChain=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/phase-chain.cs.txt"));check(ActorScripts.ToUnity(phaseChain).Count==4&&phaseChain.All(f=>f.Code.Contains("\"lateClock\":3")),"Nested Start and OnEnable creation bundles retain explicit third-version lifecycle metadata through reverse conversion");
        var lateOrderTamper=false;try{ActorScripts.ToUnity(lateFiles.Select(f=>f with{Code=f.Code.Replace(LateUpdatePorting.TickOrder,LateUpdatePorting.PreviousTickOrder)}).ToArray());}catch(NotSupportedException){lateOrderTamper=true;}check(lateOrderTamper,"Removing prerequisites from existing higher-order actors cannot silently reorder dynamic scripts after another owner is destroyed");
        var bootFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/start-chain.cs.txt"));
        check(bootFiles.Count==5&&bootFiles.All(f=>f.Code.Contains("\"startQueue\":1"))&&ActorScripts.ToUnity(bootFiles).Count==5,"No-LateUpdate nested startup bundles roundtrip all five classes with an explicit queue version");
        foreach(var code in new[]{File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/start-chain.cs.txt"),"using UnityEngine;\n"+string.Join("\n",ActorScripts.ToUnity(bootFiles).Select(f=>f.Code.Replace("using UnityEngine;","")))}){
            var bootLedgerType=CompileActor(code,ActorScripts.ValidationStub).GetType("BootLedger")!;
            int BootOrder(params int[] values){var instance=Activator.CreateInstance(bootLedgerType)!;foreach(var value in values)bootLedgerType.GetMethod("Record")!.Invoke(instance,[value]);return (int)bootLedgerType.GetField("Code")!.GetValue(instance)!;}
            check(BootOrder(12,22,112)==BootOrder(22,12,112)&&BootOrder(12,112,22)!=BootOrder(12,22,112)&&BootOrder(11,21)!=BootOrder(21,11),"Startup order oracle allows only same-class Update permutations while detecting cross-class order and distinct Start events");
        }
        foreach(var removed in new[]{StartQueuePorting.Includes,StartQueuePorting.Runtime,StartQueuePorting.Helpers,StartQueuePorting.DeferStart,StartQueuePorting.Startup,StartQueuePorting.TickPrefix,LateUpdatePorting.TickOrder}){
            var bootRejected=false;try{ActorScripts.ToUnity(bootFiles.Select(f=>f with{Code=f.Code.Replace(removed,"")}).ToArray());}catch(NotSupportedException){bootRejected=true;}
            check(bootRejected,"Reverse conversion rejects removal of verified startup runtime: "+removed.Split('\n')[0]);
        }
        var bootUnknown=false;try{ActorScripts.ToUnity(bootFiles.Select(f=>f with{Code=f.Code.Replace("\"startQueue\":1","\"startQueue\":99")}).ToArray());}catch(NotSupportedException){bootUnknown=true;}check(bootUnknown,"Unknown startup queue versions cannot silently select a scheduling policy");
        var bootNoMeta=false;try{ActorScripts.ToUnity(bootFiles.Select(f=>f with{Code=f.Code.Replace(",\"startQueue\":1","")}).ToArray());}catch(NotSupportedException){bootNoMeta=true;}check(bootNoMeta,"Startup queue implementation requires its matching reverse metadata");
        var bootInherited=ActorScripts.FromUnity("using UnityEngine;public class BootBase:MonoBehaviour{public int Value;protected virtual void Start(){Value++;}}public class BootDerived:BootBase{protected override void Start(){base.Start();Value++;}}");
        var bootMixed=false;try{ActorScripts.ToUnity(bootInherited.Select(f=>f.Name=="BootDerived.h"?f with{Code=f.Code.Replace(",\"startQueue\":1","")}:f).ToArray());}catch(NotSupportedException){bootMixed=true;}check(bootMixed,"Inherited script bundles cannot mix old and current startup queue metadata");
        var bootLegacy=bootFiles.Select(f=>f with{Code=f.Code.Replace(StartQueuePorting.Runtime,"").Replace(StartQueuePorting.Helpers,"").Replace(StartQueuePorting.DeferStart,LateUpdatePorting.LegacyStartHelper(Path.GetFileNameWithoutExtension(f.Name))).Replace(StartQueuePorting.Startup,StartQueuePorting.OldStartup).Replace(StartQueuePorting.TickPrefix,"").Replace(StartQueuePorting.BeginSuffix,"").Replace(LateUpdatePorting.TickOrder,LateUpdatePorting.PreviousTickOrder).Replace(",\"startQueue\":1","")}).ToArray();
        check(ActorScripts.ToUnity(bootLegacy).Count==5,"Previously published no-LateUpdate native scripts remain reversible after shared startup queues");
        var fixedFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/fixed-update.cs.txt"));var fixedBack=ActorScripts.ToUnity(fixedFiles);
        check(FixedUpdatePorting.Runtime.Contains("EM_RuntimeFixedBoundaryStart")&&FixedUpdatePorting.CurrentHelpers(false).Contains("EM_RuntimeFixedBoundaryStart")&&FixedUpdatePorting.CurrentHelpers(true).Contains("FEMLateRuntime::PreStartWorld()"),"Fixed-step boundaries and both startup contexts are emitted despite source newline conventions");
        var fixedFourth=fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.Runtime,FixedUpdatePorting.FourthRuntime).Replace(FixedUpdatePorting.CurrentHelpers(false),FixedUpdatePorting.Helpers).Replace("\"fixedClock\":5","\"fixedClock\":4")}).ToArray();
        check(ActorScripts.ToUnity(fixedFourth).Count==4,"Published quantized fixed-clock version four remains reversible after fixed-phase spawn support");
        var fixedBoundaryTamper=false;try{ActorScripts.ToUnity(fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.CurrentHelpers(false),FixedUpdatePorting.Helpers)}).ToArray());}catch(NotSupportedException){fixedBoundaryTamper=true;}check(fixedBoundaryTamper,"Fixed version five rejects loss of its scoped start boundary dispatcher");
        var fixedSpawnFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/fixed-spawn.cs.txt"));var fixedSpawnBack=ActorScripts.ToUnity(fixedSpawnFiles);
        check(fixedSpawnFiles.Count==6&&fixedSpawnBack.Single(f=>f.Name=="FLoopDerived.cs").Code.Contains("base.FixedUpdate()")&&fixedSpawnBack.Single(f=>f.Name=="FLoopDriver.cs").Code.Contains("CloneSameChild"),"Fixed spawn/clone bundle restores all six classes with explicit inherited callbacks and alias observations");
        check(fixedFiles.Count==4&&fixedFiles.Single(f=>f.Name=="FixedBase.h").Code.Contains("EM_FixedUpdate(float DeltaSeconds)")&&fixedFiles.Single(f=>f.Name=="FixedBase.h").Code.Contains("141120000")&&fixedBack.Single(f=>f.Name=="FixedWorker.cs").Code.Contains("base.FixedUpdate()"),"Fixed events preserve explicit phase, shared integer clock and inherited parent calls");
        var fixedStates=new List<string>();foreach(var code in new[]{File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/fixed-update.cs.txt"),string.Join("\n",fixedBack.Select(f=>f.Code.Replace("using UnityEngine;","")))}){var asm=CompileActor(code,ActorScripts.ValidationStub);var time=asm.GetType("UnityEngine.Time")!;time.GetField("deltaTime")!.SetValue(null,.02f);time.GetField("fixedDeltaTime")!.SetValue(null,.02f);var ledger=Activator.CreateInstance(asm.GetType("FixedLedger")!)!;var type=asm.GetType("FixedWorker")!;var worker=Activator.CreateInstance(type)!;type.GetField("Ledger")!.SetValue(worker,ledger);foreach(var ev in new[]{"Awake","OnEnable","Start","FixedUpdate","FixedUpdate","Update"})type.GetMethod(ev,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(worker,null);fixedStates.Add(string.Join("|",new[]{"StartCount","FixedCount","DerivedFixed","Updates","FixedElapsed","StepSeen","StartedBeforeFixed"}.Select(n=>type.GetField(n)!.GetValue(worker)))+"|"+ledger.GetType().GetField("Code")!.GetValue(ledger));}
        check(fixedStates[0]==fixedStates[1]&&fixedStates[0].StartsWith("1|2|2|1|"),"Executed original/restored fixed inheritance preserves counters, delta context and call order");
        var legacyFixed=fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.Runtime,FixedUpdatePorting.LegacyRuntime).Replace(FixedUpdatePorting.CurrentHelpers(false),FixedUpdatePorting.Helpers).Replace(",\"fixedClock\":5","")}).ToArray();
        check(ActorScripts.ToUnity(legacyFixed).Count==4,"Previously published per-world fixed-clock headers remain reversible after game-instance clock support");
        var mixedFixed=false;try{ActorScripts.ToUnity(fixedFiles.Select((f,i)=>i==0?legacyFixed[i]:f).ToArray());}catch(NotSupportedException){mixedFixed=true;}check(mixedFixed,"Mixed fixed-clock definitions cannot silently select a runtime by C++ include order");
        var unknownFixed=false;try{ActorScripts.ToUnity(fixedFiles.Select(f=>f with{Code=f.Code.Replace("\"fixedClock\":5","\"fixedClock\":52")}).ToArray());}catch(NotSupportedException){unknownFixed=true;}check(unknownFixed,"Unknown fixed-clock backend versions are rejected");
        var fixedTamper=false;try{ActorScripts.ToUnity(fixedFiles.Select(f=>f with{Code=f.Code.Replace("EM_FixedUpdate(EM_Delta);","EM_Update(EM_Delta);")}).ToArray());}catch(NotSupportedException){fixedTamper=true;}check(fixedTamper,"Fixed events cannot silently dispatch through the variable frame update");
        var fixedMissing=false;try{ActorScripts.ToUnity(fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.Runtime,"")}).ToArray());}catch(NotSupportedException){fixedMissing=true;}check(fixedMissing,"Fixed backend metadata requires its complete timing implementation");
        var previousFixed=fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.Runtime,FixedUpdatePorting.PreviousRuntime).Replace(FixedUpdatePorting.CurrentHelpers(false),FixedUpdatePorting.Helpers).Replace("\"fixedClock\":5","\"fixedClock\":2")}).ToArray();check(ActorScripts.ToUnity(previousFixed).Count==4,"Published scene-continuous fixed clock version two remains restorable after zero-rate startup correction");
        var startupFixed=fixedFiles.Select(f=>f with{Code=f.Code.Replace(FixedUpdatePorting.Runtime,FixedUpdatePorting.StartupRuntime).Replace(FixedUpdatePorting.CurrentHelpers(false),FixedUpdatePorting.Helpers).Replace("\"fixedClock\":5","\"fixedClock\":3")}).ToArray();check(ActorScripts.ToUnity(startupFixed).Count==4,"Published zero-start clock version three remains restorable after render-before-scale quantization correction");
        var fixedSetter=false;try{ActorScripts.FromUnity("using UnityEngine;public class FixedSetter:MonoBehaviour{public void Change(){Time.fixedDeltaTime=.01f;}}");}catch(NotSupportedException){fixedSetter=true;}check(fixedSetter,"Untested runtime fixed-step mutation is rejected before native generation");
        const string otherIncrement="using UnityEngine;public class IncrementTarget:MonoBehaviour{public int Value=2147483647;}public class IncrementUser:MonoBehaviour{public void Apply(IncrementTarget other){other.Value++;--other.Value;++other.Value;other.Value--;}}";
        var incrementFiles=ActorScripts.FromUnity(otherIncrement);var incrementBack=string.Join("\n",ActorScripts.ToUnity(incrementFiles).Select(f=>f.Code.Replace("using UnityEngine;","")));var incrementValues=new List<int>();foreach(var code in new[]{otherIncrement,incrementBack}){var asm=CompileActor(code);var target=Activator.CreateInstance(asm.GetType("IncrementTarget")!)!;var user=Activator.CreateInstance(asm.GetType("IncrementUser")!)!;user.GetType().GetMethod("Apply")!.Invoke(user,[target]);incrementValues.Add((int)target.GetType().GetField("Value")!.GetValue(target)!);}
        check(incrementValues.SequenceEqual(new[]{int.MaxValue,int.MaxValue})&&incrementFiles.Single(f=>f.Name=="IncrementUser.h").Code.Contains("EM_ComponentWrite<int32>"),"Other-component increments and decrements use reflected writes and preserve overflow through actual C# execution");
        var fixedGraphRejected=false;try{ActorBlueprintGraphs.FromUnity("using UnityEngine;public class FixedGraph:MonoBehaviour{public double Value;public void FixedUpdate(){Value+=1.0;}}");}catch(NotSupportedException){fixedGraphRejected=true;}check(fixedGraphRejected,"FixedUpdate cannot be misrouted to an unbound Blueprint custom event");
        var intTypes=new[]{CompileActor(intSource).GetType("IntActor")!,CompileActor(intBack).GetType("IntActor")!};var intStates=new List<string>();
        foreach(var type in intTypes){var instance=Activator.CreateInstance(type)!;foreach(var ev in new[]{"Awake","Start","Update","Update","Update","Update","OnDestroy"})type.GetMethod(ev,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(instance,null);intStates.Add(string.Join("|",new[]{"Count","Product","Flag"}.Select(n=>type.GetField(n)!.GetValue(instance)))+"|"+type.GetMethod("Mix")!.Invoke(instance,[19]));}
        check(intStates[0]==intStates[1]&&intStates[0].StartsWith("-2147483643|"),"Integer overflow, increments, compound state, method arguments and reverse C# execution agree");
        rejected=false;try{ActorScripts.FromUnity(intSource.Replace("amount * 7","amount / (amount - amount)"));}catch(NotSupportedException){rejected=true;}check(rejected,"Integer division with an unproven divisor remains explicitly blocked");
        var intRoot=Path.GetFullPath("artifacts/int-actor-check");Directory.CreateDirectory(intRoot);File.WriteAllText(Path.Combine(intRoot,"original.cs"),intSource);File.WriteAllText(Path.Combine(intRoot,"IntActor.h"),intCpp.Single().Code);File.WriteAllText(Path.Combine(intRoot,"IntActor.cs"),intBack);File.WriteAllLines(Path.Combine(intRoot,"expected.txt"),intStates);
        const string referenceSource="""
using UnityEngine;
public class ReferenceActor : MonoBehaviour {
 public GameObject Target;
 public GameObject Empty = null;
 public bool HasTarget() { return Target != null; }
 public bool IsEmpty() { return Empty == null; }
 public void SetTarget(GameObject target) { Target = target; }
}
""";
        var referenceCpp=ActorScripts.FromUnity(referenceSource);var referenceBack=ActorScripts.ToUnity(referenceCpp).Single().Code;
        check(referenceCpp.Single().Code.Contains("TObjectPtr<AActor> Target")&&referenceCpp.Single().Code.Contains("EM_ObjectValid(Target)"),"GameObject fields map to tracked native Actor references and validity checks");
        var referenceStates=new List<string>();foreach(var code in new[]{referenceSource,referenceBack}){var assembly=CompileActor(code);var type=assembly.GetType("ReferenceActor")!;var instance=Activator.CreateInstance(type)!;var before=type.GetMethod("HasTarget")!.Invoke(instance,null);type.GetMethod("SetTarget")!.Invoke(instance,[Activator.CreateInstance(assembly.GetType("UnityEngine.GameObject")!)]);var after=type.GetMethod("HasTarget")!.Invoke(instance,null);referenceStates.Add(before+"|"+after+"|"+type.GetMethod("IsEmpty")!.Invoke(instance,null));}
        check(referenceStates.All(s=>s=="False|True|True"),"GameObject reference assignment/null checks survive native-body reverse conversion");
        const string parenthesizedNullSource="using UnityEngine; public class NullActor:MonoBehaviour { public GameObject Target; public Transform Pose; public NullActor Peer; public bool A(){return Target == ((null));} public bool B(){return (null) != Target;} public bool C(){return (Pose) == (null);} public bool D(){return ((null)) != (Peer);} }";
        var nullCpp=ActorScripts.FromUnity(parenthesizedNullSource);
        var nullBack=ActorScripts.ToUnity(nullCpp).Single().Code;
        check(nullCpp.Single().Code.Contains("(!EM_ObjectValid(Target))")&&nullCpp.Single().Code.Contains("EM_ObjectValid(Peer)"),"Parenthesized null operands use native object validity instead of raw pointer equality");
        var nullStates=new List<string>();
        foreach(var code in new[]{parenthesizedNullSource,nullBack}){
            var asm=CompileActor(code,"namespace UnityEngine { public class MonoBehaviour {} public class GameObject {} public class Transform {} }");
            var actor=asm.GetType("NullActor")!;var obj=Activator.CreateInstance(actor)!;
            string Read()=>string.Join("|",new[]{"A","B","C","D"}.Select(n=>actor.GetMethod(n)!.Invoke(obj,null)));
            var empty=Read();
            foreach(var field in new[]{"Target","Pose","Peer"}){var f=actor.GetField(field)!;f.SetValue(obj,Activator.CreateInstance(f.FieldType));}
            nullStates.Add(empty+"/"+Read());
        }
        check(nullStates.All(s=>s=="True|False|True|False/False|True|False|True"),"Null and live GameObject, Transform and behaviour checks execute identically after parenthesized-null roundtrip");
        const string uiNullSource="using UnityEngine; using UnityEngine.UI; public class UiNullActor:MonoBehaviour { public Canvas Panel;public Button Action;public bool A(){return Panel == (null);}public bool B(){return ((null)) != Action;} }";
        var uiNullCpp=ActorScripts.FromUnity(uiNullSource);var uiNullBack=ActorScripts.ToUnity(uiNullCpp).Single().Code;
        check(uiNullCpp.Single().Code.Contains("EM_UIValid(Panel, TEXT(\"Canvas\"))")&&uiNullCpp.Single().Code.Contains("EM_UIValid(Action, TEXT(\"Button\"))")&&uiNullBack.Contains("Panel")&&uiNullBack.Contains("Action"),"Canvas and Button parenthesized null comparisons retain kind-aware validity and reverse conversion");
        const string defaultSource="using UnityEngine; public class DefaultActor:MonoBehaviour { public GameObject Target=default(GameObject); public Transform Pose=default; public DefaultActor Peer=default(DefaultActor); public bool A(){return Target == (default(GameObject));} public bool B(){return default != Pose;} public bool C(){return Peer != default(DefaultActor);} public GameObject Nothing(){return default;} public void Clear(){GameObject value=default;Target=value;Pose=default(Transform);Peer=default;} }";
        var defaultCpp=ActorScripts.FromUnity(defaultSource);var defaultBack=ActorScripts.ToUnity(defaultCpp).Single().Code;
        check(defaultCpp.Single().Code.Contains("(!EM_ObjectValid(Target))")&&defaultCpp.Single().Code.Contains("EM_ObjectValid(Pose)")&&defaultCpp.Single().Code.Contains("Target = nullptr"),"Typed and inferred reference defaults lower to null initialization and native validity comparisons");
        var defaultVariants=ActorScripts.FromUnity(defaultSource.Replace("default != Pose","((default)) != (Pose)").Replace("Target == (default(GameObject))","Target == default"));
        check(defaultVariants.SequenceEqual(defaultCpp),"Parenthesized inferred defaults and typed defaults produce identical native code for reference comparisons");
        var defaultStates=new List<string>();
        foreach(var code in new[]{defaultSource,defaultBack}){
            var asm=CompileActor(code,"namespace UnityEngine { public class MonoBehaviour {} public class GameObject {} public class Transform {} }");
            var actor=asm.GetType("DefaultActor")!;var obj=Activator.CreateInstance(actor)!;
            string Read()=>string.Join("|",new[]{"A","B","C"}.Select(n=>actor.GetMethod(n)!.Invoke(obj,null)));
            var empty=Read();
            foreach(var field in new[]{"Target","Pose","Peer"}){var f=actor.GetField(field)!;f.SetValue(obj,Activator.CreateInstance(f.FieldType));}
            var live=Read();actor.GetMethod("Clear")!.Invoke(obj,null);
            defaultStates.Add(empty+"/"+live+"/"+Read()+"/"+(actor.GetMethod("Nothing")!.Invoke(obj,null)==null));
        }
        check(defaultStates.All(s=>s=="True|False|False/False|True|True/True|False|False/True"),"Reference default fields, locals, assignments, returns and comparisons preserve executed managed state after roundtrip");
        const string uiDefaultSource="using UnityEngine;using UnityEngine.UI;public class UiDefaultActor:MonoBehaviour {public Canvas Panel=default;public Button Action=default(Button);public bool Empty(){return Panel==default(Canvas);}public bool Live(){return default!=Action;}public Button None(){return default;} }";
        var uiDefaultCpp=ActorScripts.FromUnity(uiDefaultSource);var uiDefaultBack=ActorScripts.ToUnity(uiDefaultCpp).Single().Code;
        check(uiDefaultCpp.Single().Code.Contains("Panel = nullptr")&&uiDefaultCpp.Single().Code.Contains("EM_UIValid(Action, TEXT(\"Button\"))")&&uiDefaultBack.Contains("return null;"),"Canvas and Button reference defaults survive generation and native-body restoration");
        var uiIdentitySource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/ui-equality.cs.txt");
        var uiIdentityFiles=ActorScripts.FromUnity(uiIdentitySource);var uiIdentityNative=uiIdentityFiles.Single().Code;
        check(uiIdentityNative.Contains("\"weakUI\":true")&&uiIdentityNative.Contains("using EM_UIRef_Canvas=TWeakObjectPtr<AActor>;")&&uiIdentityNative.Contains("EM_UIEqual(Left, TEXT(\"Canvas\"), LeftButton, TEXT(\"Button\"))"),"UI equality retains owner identity and distinct Canvas/Button kinds");
        check(ActorScripts.ToUnity(uiIdentityFiles).Single().Code.Contains("Canvas Echo(Canvas value)"),"UI function inputs and returns restore their source types");
        var uiAliasTamperRejected=false;try{ActorScripts.ToUnity(uiIdentityFiles.Select(f=>f with{Code=f.Code.Replace("using EM_UIRef_Canvas=TWeakObjectPtr<AActor>;","using EM_UIRef_Canvas=AActor*;")}).ToArray());}catch(NotSupportedException){uiAliasTamperRejected=true;}
        check(uiAliasTamperRejected,"UI reference aliases cannot silently lose GC identity during reverse conversion");
        var uiDestroyFiles=ActorScripts.FromUnity(File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/ui-destruction.cs.txt"));
        check(uiDestroyFiles.Single().Code.Contains("EM_DestroyCanvas(EM_NativeGameObject(Panel))")&&uiDestroyFiles.Single().Code.Contains("EM_DestroyButton(EM_NativeGameObject(Action))")&&ActorScripts.ToUnity(uiDestroyFiles).Single().Code.Contains("Destroy(Panel)"),"Canvas and Button destruction select deferred component removal and restore Unity calls");
        check(uiDestroyFiles.Single().Code.Contains("EM_NormalizeClonedUI_AuditUIDestruction")&&uiDestroyFiles.Single().Code.Contains("EM_Copy->Tags.RemoveAll")&&uiDestroyFiles.Single().Code.Contains("EM_Copy->ProcessEvent(EM_Function,&EM_Args)"),"UI cloning normalizes missing serialized component references and clears inherited removal requests");
        var uiCloneTamperRejected=false;try{ActorScripts.ToUnity(uiDestroyFiles.Select(f=>f with{Code=f.Code.Replace("if(!EM_UIValid(Panel,TEXT(\"Canvas\")))Panel=EM_Old->Panel;","")}).ToArray());}catch(NotSupportedException){uiCloneTamperRejected=true;}
        check(uiCloneTamperRejected,"Edited UI clone reference normalization is rejected during reverse conversion");
        var legacyUiRemoval=uiDestroyFiles.Select(f=>f with{Code=f.Code.Replace(WeakUiPorting.Helpers,WeakUiPorting.LegacyHelpers)}).ToArray();
        check(ActorScripts.ToUnity(legacyUiRemoval).Single().Code.Contains("Destroy(Panel)"),"Previously released UI removal headers remain reversible after callback lifetime correction");
        var defaultNumericRejected=false;try{ActorScripts.FromUnity("using UnityEngine;public class NumericDefault:MonoBehaviour{public int Read(){return default(int);}}");}catch(NotSupportedException){defaultNumericRejected=true;}
        check(defaultNumericRejected,"Value-type default expressions remain explicitly unsupported instead of being mapped to object null");
        const string loopSource="""
using UnityEngine;
public class LoopActor:MonoBehaviour {
 public int Sum; public int Index;
 public void Bump(){Index++;}
 public void Run(){
  for(int i=0,j=8;i<6;i++,j-=2){if(i==1)continue;if(j<0)break;Sum+=i*10+j;}
  for(;Index<3;Index++){for(int inner=0;inner<4;inner++){if(inner==2)break;Sum+=Index+inner;}}
  int local=0;for(local=0;local<4;local+=1){if(local==2)continue;Sum+=local;}
  for(;;){Sum+=1;break;}
  for(int i=0;i<2;i++);
  do{Index--;if(Index==1)continue;Sum+=Index;}while(Index>0);
  for(Bump();Index<3;Bump()){Sum+=Index;}
 }
 public int Early(){for(int i=0;i<5;i++){if(i==3)return i;}return -1;}
 public int Overflow(){int count=0;for(int i=2147483646;i>0;i++){count++;}return count;}
 public int Ordered(){int first=0;int second=0;for(first=1,second=first+1;first<3;++first,second+=first){}return second;}
 public int Inferred(){int total=0;for(var step=0;step<4;step++){total+=step;}return total;}
}
""";
        var loopCpp=ActorScripts.FromUnity(loopSource);var loopBack=ActorScripts.ToUnity(loopCpp).Single().Code;
        check(loopCpp.Single().Code.Contains("for (")&&loopCpp.Single().Code.Contains("EM_local_i = EM_IntAdd(EM_local_i, 1)")&&loopCpp.Single().Code.Contains("do "),"For and do/while loops use native loop scope and defined integer update helpers");
        var loopStates=new List<string>();
        foreach(var code in new[]{loopSource,loopBack}){
            var actor=CompileActor(code).GetType("LoopActor")!;var obj=Activator.CreateInstance(actor)!;
            actor.GetMethod("Run")!.Invoke(obj,null);
            loopStates.Add(actor.GetField("Sum")!.GetValue(obj)+"|"+actor.GetField("Index")!.GetValue(obj)+"|"+actor.GetMethod("Early")!.Invoke(obj,null)+"|"+actor.GetMethod("Overflow")!.Invoke(obj,null)+"|"+actor.GetMethod("Ordered")!.Invoke(obj,null)+"|"+actor.GetMethod("Inferred")!.Invoke(obj,null));
        }
        check(loopStates.All(s=>s=="123|3|3|2|7|6"),"Nested loops, continue/break, ordered multiple clauses, inferred locals, empty bodies, calls, early returns and overflow agree after managed roundtrip execution");
        var loopRoot=Path.GetFullPath("artifacts/loop-statement-check");Directory.CreateDirectory(loopRoot);
        File.WriteAllText(Path.Combine(loopRoot,"original.cs"),loopSource);
        File.WriteAllText(Path.Combine(loopRoot,"LoopActor.h"),loopCpp.Single().Code);
        File.WriteAllText(Path.Combine(loopRoot,"restored.cs"),loopBack);
        File.WriteAllLines(Path.Combine(loopRoot,"managed-states.txt"),loopStates);
        var loopDivisionBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class LoopDivision:MonoBehaviour{public void Run(int divisor){for(int i=8;i>0;i/=divisor){}}}");}catch(NotSupportedException){loopDivisionBlocked=true;}
        check(loopDivisionBlocked,"For-loop updates retain the existing integer division semantics guard");
        var loopEffectsBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class LoopEffects:MonoBehaviour{public int Next(){return 1;}public void Run(){for(int i=0;i<3;i=Next()+Next()){}}}");}catch(NotSupportedException){loopEffectsBlocked=true;}
        check(loopEffectsBlocked,"For-loop updates cannot bypass the existing side-effect evaluation-order guard");
        const string switchSource="""
using UnityEngine;
public class SwitchActor:MonoBehaviour {
 public int Calls;public int Score;
 public int Next(int value){Calls++;return value;}
 public int Pick(int value){switch(Next(value)){
  case -2147483648:return 1;
  case -1:return 2;
  case 1+1:case 3:int local=10;return local+value;
  default:int other=20;return other+value;
 }}
 public int NoDefault(int value){int result=0;switch(value){case 1:result=7;break;}return result;}
 public void Empty(int value){switch(Next(value)){}}
 public int DefaultOnly(int value){switch(value){default:return value+1;}}
 public int Shared(int value){switch(Next(value)){
  case 0:int first=Next(4),second=Next(first+1);return first+second;
  case 1:first=Next(8);second=Next(first+2);return first+second;
  case 2:int pending;pending=Next(11);return pending;
  case 3:pending=Next(12);return pending;
  default:return 0;
 }}
 public void Run(){for(int i=0;i<5;i++){switch(i){
  case 0:Score+=1;break;
  case 1:continue;
  case 2:switch(i-1){case 1:Score+=20;break;default:Score+=200;break;}break;
  default:Score+=i;break;
 }Score+=10;}}
}
""";
        var switchCpp=ActorScripts.FromUnity(switchSource);var switchBack=ActorScripts.ToUnity(switchCpp).Single().Code;
        check(switchCpp.Single().Code.Contains("switch (")&&switchCpp.Single().Code.Contains("case 2:")&&switchCpp.Single().Code.Contains("case (-2147483647 - 1):"),"Integer switch labels are emitted as compile-time constants with scoped section bodies and a signed-safe minimum value");
        var switchStates=new List<string>();
        foreach(var code in new[]{switchSource,switchBack}){
            var actor=CompileActor(code).GetType("SwitchActor")!;var obj=Activator.CreateInstance(actor)!;
            var selected=string.Join("|",new[]{int.MinValue,-1,2,3,9}.Select(v=>actor.GetMethod("Pick")!.Invoke(obj,[v])));
            actor.GetMethod("Run")!.Invoke(obj,null);actor.GetMethod("Empty")!.Invoke(obj,[9]);
            var shared=string.Join("|",new[]{0,1,2,3,9}.Select(v=>actor.GetMethod("Shared")!.Invoke(obj,[v])));
            switchStates.Add(selected+"|"+shared+"|"+actor.GetField("Calls")!.GetValue(obj)+"|"+actor.GetField("Score")!.GetValue(obj)+"|"+actor.GetMethod("NoDefault")!.Invoke(obj,[9])+"|"+actor.GetMethod("NoDefault")!.Invoke(obj,[1])+"|"+actor.GetMethod("DefaultOnly")!.Invoke(obj,[4]));
        }
        check(switchStates.All(s=>s=="1|2|12|13|29|9|18|11|12|0|17|68|0|7|5"),"Switch selection, shared local scope, initializer ordering, nested control flow and empty/default-only branches preserve managed results");
        var switchRoot=Path.GetFullPath("artifacts/switch-statement-check");Directory.CreateDirectory(switchRoot);
        File.WriteAllText(Path.Combine(switchRoot,"original.cs"),switchSource);File.WriteAllText(Path.Combine(switchRoot,"SwitchActor.h"),switchCpp.Single().Code);
        File.WriteAllText(Path.Combine(switchRoot,"restored.cs"),switchBack);File.WriteAllLines(Path.Combine(switchRoot,"managed-states.txt"),switchStates);
        var sharedHeaders=ActorScripts.FromUnity("using UnityEngine;public class SharedCase:MonoBehaviour{public int Pick(int value){switch(value){case 0:int shared=4;return shared;case 1:shared=8;return shared;default:return 0;}}}");
        check(ActorScripts.ToUnity(sharedHeaders).Single().Code.Contains("shared"),"Cross-section switch locals retain a shared enclosing scope through native-body restoration");
        const string sharedReferenceSource="using UnityEngine;public class SharedReference:MonoBehaviour{public bool Pick(int value,GameObject input){switch(value){case 0:GameObject target=input;return target!=null;case 1:target=input;return target==null;default:return false;}}}";
        var sharedReferenceCpp=ActorScripts.FromUnity(sharedReferenceSource);var sharedReferenceBack=ActorScripts.ToUnity(sharedReferenceCpp).Single().Code;
        var sharedReferenceStates=new List<string>();
        foreach(var code in new[]{sharedReferenceSource,sharedReferenceBack}){
            var asm=CompileActor(code);var actor=asm.GetType("SharedReference")!;var obj=Activator.CreateInstance(actor)!;var input=Activator.CreateInstance(asm.GetType("UnityEngine.GameObject")!)!;
            sharedReferenceStates.Add(string.Join("|",new[]{actor.GetMethod("Pick")!.Invoke(obj,[0,null]),actor.GetMethod("Pick")!.Invoke(obj,[1,null]),actor.GetMethod("Pick")!.Invoke(obj,[0,input]),actor.GetMethod("Pick")!.Invoke(obj,[1,input])}));
        }
        check(sharedReferenceStates.All(s=>s=="False|True|True|False"),"Shared switch GameObject locals retain null/live reference assignment and comparisons in managed roundtrip execution");
        var switchPatternBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class PatternCase:MonoBehaviour{public int Pick(int value){switch(value){case > 0:return 1;default:return 0;}}}");}catch(NotSupportedException ex){switchPatternBlocked=ex.Message.Contains("정수 상수 case");}
        check(switchPatternBlocked,"Switch pattern labels cannot silently enter the constant-label native conversion");
        var gotoCaseBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class GotoCase:MonoBehaviour{public int Pick(int value){switch(value){case 0:goto case 1;case 1:return 1;default:return 0;}}}");}catch(NotSupportedException ex){gotoCaseBlocked=ex.Message.Contains("GotoCaseStatement");}
        check(gotoCaseBlocked,"Unsupported goto-case control flow remains explicitly rejected");
        var boolSwitchBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class BoolCase:MonoBehaviour{public int Pick(bool value){switch(value){case true:return 1;default:return 0;}}}");}catch(NotSupportedException ex){boolSwitchBlocked=ex.Message.Contains("int 선택값");}
        check(boolSwitchBlocked,"Non-int switch selection is rejected with the precise supported-type diagnostic");
        const string conditionalSource="""
using UnityEngine;
public class ConditionalActor:MonoBehaviour {
 public int Calls;
 public bool Ready(int value){Calls++;return value>0;}
 public int Next(int value){Calls++;return value;}
 public int Choose(int value){return Ready(value)?Next(10):Next(-10);}
 public float Scalar(bool condition){return condition?1.25f:2;}
 public bool BooleanChoice(bool condition){return condition?true:false;}
 public int Nested(int value){return value<0?-1:(value==0?0:1);}
 public double Wide(bool condition){return condition?1:2.5d;}
}
""";
        var conditionalCpp=ActorScripts.FromUnity(conditionalSource);var conditionalBack=ActorScripts.ToUnity(conditionalCpp).Single().Code;
        var conditionalStates=new List<string>();
        foreach(var code in new[]{conditionalSource,conditionalBack}){
            var actor=CompileActor(code).GetType("ConditionalActor")!;var obj=Activator.CreateInstance(actor)!;
            object? Call(string name,object value)=>actor.GetMethod(name)!.Invoke(obj,[value]);
            var values=new object?[]{Call("Choose",1),Call("Choose",0),Call("Choose",-1),actor.GetField("Calls")!.GetValue(obj),Call("Scalar",true),Call("Scalar",false),Convert.ToInt32(Call("BooleanChoice",true)),Convert.ToInt32(Call("BooleanChoice",false)),Call("Nested",-1),Call("Nested",0),Call("Nested",1),Call("Wide",true),Call("Wide",false)};
            conditionalStates.Add(string.Join("|",values.Select(v=>Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture))));
        }
        check(conditionalStates.All(s=>s=="10|-10|-10|6|1.25|2|1|0|-1|0|1|1|2.5"),"Conditional expressions preserve selected-only side effects, nested branches, boolean values and numeric promotion in managed roundtrip execution");
        var conditionalRoot=Path.GetFullPath("artifacts/conditional-statement-check");Directory.CreateDirectory(conditionalRoot);
        File.WriteAllText(Path.Combine(conditionalRoot,"original.cs"),conditionalSource);File.WriteAllText(Path.Combine(conditionalRoot,"ConditionalActor.h"),conditionalCpp.Single().Code);
        File.WriteAllText(Path.Combine(conditionalRoot,"restored.cs"),conditionalBack);File.WriteAllLines(Path.Combine(conditionalRoot,"managed-states.txt"),conditionalStates);
        const string referenceConditionalSource="""
using UnityEngine;
public class ReferenceConditional:MonoBehaviour {
 public GameObject Left;
 public GameObject Right;
 public int Calls;
 public GameObject Next(GameObject value){Calls++;return value;}
 public GameObject Pick(bool choose){return choose?Left:Right;}
 public GameObject Optional(bool choose){return choose?Left:null;}
 public GameObject Reverse(bool choose){return choose?null:Right;}
 public GameObject Empty(bool choose){return choose?null:null;}
 public GameObject Lazy(bool choose){return choose?Next(Left):Next(Right);}
 public GameObject Nested(bool choose,bool other){return choose?(other?Left:null):Right;}
 public GameObject Local(bool choose){GameObject value=choose?Left:Right;return value;}
 public Transform TransformPick(bool choose,Transform left,Transform right){return choose?left:right;}
}
""";
        var referenceConditionalCpp=ActorScripts.FromUnity(referenceConditionalSource);var referenceConditionalBack=ActorScripts.ToUnity(referenceConditionalCpp).Single().Code;
        var referenceConditionalStates=new List<string>();
        foreach(var code in new[]{referenceConditionalSource,referenceConditionalBack}){
            var asm=CompileActor(code);var actor=asm.GetType("ReferenceConditional")!;var obj=Activator.CreateInstance(actor)!;var objectType=asm.GetType("UnityEngine.GameObject")!;
            var left=Activator.CreateInstance(objectType)!;var right=Activator.CreateInstance(objectType)!;actor.GetField("Left")!.SetValue(obj,left);actor.GetField("Right")!.SetValue(obj,right);
            string Identity(object? value)=>ReferenceEquals(value,left)?"L":ReferenceEquals(value,right)?"R":value is null?"N":"?";
            object? Call(string name,params object?[] args)=>actor.GetMethod(name)!.Invoke(obj,args);
            var values=new List<string>();foreach(var name in new[]{"Pick","Optional","Reverse","Lazy","Local"})foreach(var choose in new[]{true,false})values.Add(Identity(Call(name,choose)));
            foreach(var choose in new[]{true,false})foreach(var other in new[]{true,false})values.Add(Identity(Call("Nested",choose,other)));if(Call("Empty",true)is not null||Call("Empty",false)is not null)throw new Exception("Target-typed null conditional differs");
            var transformType=asm.GetType("UnityEngine.Transform")!;var firstTransform=Activator.CreateInstance(transformType)!;var secondTransform=Activator.CreateInstance(transformType)!;if(!ReferenceEquals(Call("TransformPick",true,firstTransform,secondTransform),firstTransform)||!ReferenceEquals(Call("TransformPick",false,firstTransform,secondTransform),secondTransform))throw new Exception("Transform conditional identity differs");values.Add(actor.GetField("Calls")!.GetValue(obj)!.ToString()!);referenceConditionalStates.Add(string.Join("|",values));
        }
        check(referenceConditionalStates.All(s=>s=="L|R|L|N|N|R|L|R|L|R|L|N|R|R|2"),"Reference conditional roundtrip preserves null/live identity, field selection, nested/local values and selected-only calls");
        var referenceConditionalRoot=Path.GetFullPath("artifacts/reference-conditional-check");Directory.CreateDirectory(referenceConditionalRoot);
        File.WriteAllText(Path.Combine(referenceConditionalRoot,"ReferenceConditional.h"),referenceConditionalCpp.Single().Code);File.WriteAllText(Path.Combine(referenceConditionalRoot,"original.cs"),referenceConditionalSource);File.WriteAllText(Path.Combine(referenceConditionalRoot,"restored.cs"),referenceConditionalBack);File.WriteAllLines(Path.Combine(referenceConditionalRoot,"managed-states.txt"),referenceConditionalStates);
        const string uiConditionalSource="using UnityEngine;using UnityEngine.UI;public class UiConditional:MonoBehaviour{public Canvas CanvasLeft;public Canvas CanvasRight;public Button ButtonLeft;public Button ButtonRight;public Canvas PickCanvas(bool choose){return choose?CanvasLeft:CanvasRight;}public Button PickButton(bool choose){return choose?ButtonLeft:ButtonRight;}}";
        var uiConditionalCpp=ActorScripts.FromUnity(uiConditionalSource);var uiConditionalBack=ActorScripts.ToUnity(uiConditionalCpp).Single().Code;
        var uiConditionalIdentity=true;
        const string uiConditionalStub="namespace UnityEngine{public class MonoBehaviour{}public class Canvas{}}namespace UnityEngine.UI{public class Button{}}";
        foreach(var code in new[]{uiConditionalSource,uiConditionalBack}){
            var asm=CompileActor(code,uiConditionalStub);var actor=asm.GetType("UiConditional")!;var instance=Activator.CreateInstance(actor)!;
            foreach(var kind in new[]{"Canvas","Button"}){var type=asm.GetType(kind=="Canvas"?"UnityEngine.Canvas":"UnityEngine.UI.Button")!;var left=Activator.CreateInstance(type)!;var right=Activator.CreateInstance(type)!;actor.GetField(kind+"Left")!.SetValue(instance,left);actor.GetField(kind+"Right")!.SetValue(instance,right);uiConditionalIdentity&=ReferenceEquals(actor.GetMethod("Pick"+kind)!.Invoke(instance,[true]),left)&&ReferenceEquals(actor.GetMethod("Pick"+kind)!.Invoke(instance,[false]),right);}
        }
        check(uiConditionalIdentity,"Canvas and Button conditional references preserve selected component identity through managed roundtrip execution");
        const string inheritedConditionalSource="using UnityEngine;public class ReferenceBase:MonoBehaviour{}public class ReferenceChild:ReferenceBase{}public class InheritedConditional:MonoBehaviour{public ReferenceBase Pick(bool choose,ReferenceBase left,ReferenceChild right){return choose?left:right;}}";
        var inheritedConditionalCpp=ActorScripts.FromUnity(inheritedConditionalSource);var inheritedConditionalBack=string.Join("\n",ActorScripts.ToUnity(inheritedConditionalCpp).Select(f=>f.Code.Replace("using UnityEngine;","")));
        var inheritedConditionalIdentity=true;
        foreach(var code in new[]{inheritedConditionalSource,inheritedConditionalBack}){var asm=CompileActor(code);var actor=asm.GetType("InheritedConditional")!;var instance=Activator.CreateInstance(actor)!;var left=Activator.CreateInstance(asm.GetType("ReferenceBase")!)!;var right=Activator.CreateInstance(asm.GetType("ReferenceChild")!)!;inheritedConditionalIdentity&=ReferenceEquals(actor.GetMethod("Pick")!.Invoke(instance,[true,left,right]),left)&&ReferenceEquals(actor.GetMethod("Pick")!.Invoke(instance,[false,left,right]),right);}
        check(inheritedConditionalIdentity,"Inherited component conditional references preserve base/derived selection through managed roundtrip execution");
        var referenceComponentsSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/reference-components.cs.txt");
        var referenceComponentsCpp=ActorScripts.FromUnity(referenceComponentsSource);
        var referenceComponentsBack="using UnityEngine;using UnityEngine.UI;"+string.Join("\n",ActorScripts.ToUnity(referenceComponentsCpp).Select(f=>f.Code.Replace("using UnityEngine;","").Replace("using UnityEngine.UI;","")));
        const string referenceComponentsStub="namespace UnityEngine{public class MonoBehaviour{}public class Canvas{public bool enabled=true;}}namespace UnityEngine.UI{public class Button{public bool interactable=true;}}";
        var componentReferenceStates=new List<string>();
        foreach(var code in new[]{referenceComponentsSource,referenceComponentsBack}){
            var asm=CompileActor(code,referenceComponentsStub);var uiType=asm.GetType("AuditUiConditional")!;var ui=Activator.CreateInstance(uiType)!;
            var canvas=asm.GetType("UnityEngine.Canvas")!;var button=asm.GetType("UnityEngine.UI.Button")!;
            var cl=Activator.CreateInstance(canvas)!;var cr=Activator.CreateInstance(canvas)!;var bl=Activator.CreateInstance(button)!;var br=Activator.CreateInstance(button)!;
            foreach(var pair in new[]{("CanvasLeft",cl),("CanvasRight",cr),("ButtonLeft",bl),("ButtonRight",br)})uiType.GetField(pair.Item1)!.SetValue(ui,pair.Item2);
            var type=asm.GetType("AuditInheritedConditional")!;var inherited=Activator.CreateInstance(type)!;var left=Activator.CreateInstance(asm.GetType("AuditReferenceBase")!)!;var right=Activator.CreateInstance(asm.GetType("AuditReferenceChild")!)!;
            object? Ui(string name,params object[] args)=>uiType.GetMethod(name)!.Invoke(ui,args);
            object? Ref(string name,params object[] args)=>type.GetMethod(name)!.Invoke(inherited,args);
            var passed=0;
            void Require(bool value){if(!value)throw new Exception("Component reference execution differs");passed++;}
            foreach(var choose in new[]{true,false}){
                Require(ReferenceEquals(Ui("PickCanvas",choose),choose?cl:cr));Require(ReferenceEquals(Ui("PickButton",choose),choose?bl:br));
                Require(ReferenceEquals(Ui("OptionalCanvas",choose),choose?cl:null));Require(ReferenceEquals(Ui("OptionalButton",choose),choose?null:br));
                var before=(int)uiType.GetField("Calls")!.GetValue(ui)!;Require(ReferenceEquals(Ui("LazyCanvas",choose),choose?cl:cr)&&(int)uiType.GetField("Calls")!.GetValue(ui)! ==before+1);
                before=(int)uiType.GetField("Calls")!.GetValue(ui)!;Require(ReferenceEquals(Ui("LazyButton",choose),choose?bl:br)&&(int)uiType.GetField("Calls")!.GetValue(ui)! ==before+1);
                Require(ReferenceEquals(Ref("Pick",choose,left,right),choose?left:right));Require(ReferenceEquals(Ref("Optional",choose,left),choose?left:null));
                before=(int)type.GetField("Calls")!.GetValue(inherited)!;Require(ReferenceEquals(Ref("Lazy",choose,left,right),choose?left:right)&&(int)type.GetField("Calls")!.GetValue(inherited)! ==before+1);
            }
            Ui("SetCanvas",true,false);Require(!(bool)Ui("ReadCanvas",true)!&&(bool)Ui("ReadCanvas",false)!);
            Ui("SetCanvas",false,false);Ui("SetCanvas",true,true);Require((bool)Ui("ReadCanvas",true)!&&!(bool)Ui("ReadCanvas",false)!);
            Ui("SetButton",false,false);Require((bool)Ui("ReadButton",true)!&&!(bool)Ui("ReadButton",false)!);
            Ui("SetButton",true,false);Ui("SetButton",false,true);Require(!(bool)Ui("ReadButton",true)!&&(bool)Ui("ReadButton",false)!);
            componentReferenceStates.Add("PASS:"+passed);
        }
        check(componentReferenceStates.SequenceEqual(new[]{"PASS:22","PASS:22"}),"Canvas/Button/inherited conditionals preserve null, selected-only calls and isolated UI writes through executed source/restored C#");
        var componentReferenceRoot=Path.GetFullPath("artifacts/component-reference-check");Directory.CreateDirectory(componentReferenceRoot);
        File.WriteAllText(Path.Combine(componentReferenceRoot,"original.cs"),referenceComponentsSource);File.WriteAllText(Path.Combine(componentReferenceRoot,"restored.cs"),referenceComponentsBack);File.WriteAllLines(Path.Combine(componentReferenceRoot,"managed-states.txt"),componentReferenceStates);
        var conditionalEvaluationBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class ConditionalEvaluation:MonoBehaviour{public int Next(){return 1;}public int Pick(bool condition){return 1+(condition?Next():Next());}}");}catch(NotSupportedException ex){conditionalEvaluationBlocked=ex.Message.Contains("평가 순서");}
        check(conditionalEvaluationBlocked,"Conditional expressions do not bypass enclosing arithmetic side-effect evaluation-order protection");
        const string constantSource="""
using UnityEngine;
public class ConstantActor:MonoBehaviour {
 public int Calls;
 public int Next(int value){Calls++;return value;}
 public int Compute(int value){const int Limit=2+3,Offset=Limit+4;const bool Enabled=true;int sum=0;for(int i=0;i<Limit;i++){sum+=Offset;}if(Enabled){sum+=1;}switch(value){case Offset:sum+=2;break;default:sum+=3;break;}return sum;}
 public int Bounds(){const int Min=-2147483648;const int Max=2147483647;return Min+Max;}
 public float Ratio(){const float Small=1.25f;const float Larger=Small*2f;return Larger;}
 public double Wide(){const double Small=1.25d;const double Larger=Small+2d;return Larger;}
 public int Cross(int value){switch(value){case 0:const int Amount=7;return Amount;case 1:return Amount+1;default:return 0;}}
 public int Guard(bool choose){const bool Yes=true;return choose?Next(Yes?10:20):Next(-10);}
 public float Zero(){const float Negative=-0.0f;return Negative;}
 public float Precision(){const float Value=1.1f;return Value;}
 public double Negative(){const double Value=-0.125d;return Value;}
}
""";
        var constantCpp=ActorScripts.FromUnity(constantSource);var constantBack=ActorScripts.ToUnity(constantCpp).Single().Code;
        var constantStates=new List<string>();
        foreach(var code in new[]{constantSource,constantBack}){
            var actor=CompileActor(code).GetType("ConstantActor")!;var obj=Activator.CreateInstance(actor)!;
            object? Call(string name,params object[] values)=>actor.GetMethod(name)!.Invoke(obj,values);
            var values=new object?[]{Call("Compute",9),Call("Compute",0),Call("Bounds"),Call("Ratio"),Call("Wide"),Call("Cross",0),Call("Cross",1),Call("Cross",9),Call("Guard",true),Call("Guard",false),actor.GetField("Calls")!.GetValue(obj),BitConverter.SingleToInt32Bits((float)Call("Zero")!)<0?1:0,BitConverter.SingleToInt32Bits((float)Call("Precision")!),Call("Negative")};
            constantStates.Add(string.Join("|",values.Select(v=>Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture))));
        }
        check(constantStates.All(s=>s=="48|49|-1|2.5|3.25|7|8|0|10|-10|2|1|1066192077|-0.125"),"Local constants preserve computed labels, shared switch scope, integer bounds, floating bits, negative zero and selected-only calls after managed roundtrip execution");
        var constantRoot=Path.GetFullPath("artifacts/constant-statement-check");Directory.CreateDirectory(constantRoot);
        File.WriteAllText(Path.Combine(constantRoot,"original.cs"),constantSource);File.WriteAllText(Path.Combine(constantRoot,"ConstantActor.h"),constantCpp.Single().Code);
        File.WriteAllText(Path.Combine(constantRoot,"restored.cs"),constantBack);File.WriteAllLines(Path.Combine(constantRoot,"managed-states.txt"),constantStates);
        var nonFiniteConstantBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class NonFiniteConstant:MonoBehaviour{public float Read(){const float Bad=float.NaN;return Bad;}}");}catch(NotSupportedException ex){nonFiniteConstantBlocked=ex.Message.Contains("NaN/Infinity");}
        check(nonFiniteConstantBlocked,"Non-finite local constants are explicitly rejected instead of generating invalid native numeric literals");
        var stringConstantBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class StringConstant:MonoBehaviour{public int Read(){const string Text=\"demo\";return 1;}}");}catch(NotSupportedException ex){stringConstantBlocked=ex.Message.Contains("string");}
        check(stringConstantBlocked,"Unsupported local constant types are diagnosed even when the constant is unused");
        var bitwiseSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/bitwise.cs.txt");
        var bitwiseCpp=ActorScripts.FromUnity(bitwiseSource);var bitwiseBack="using UnityEngine;"+string.Join("\n",ActorScripts.ToUnity(bitwiseCpp).Select(f=>f.Code.Replace("using UnityEngine;","")));
        var bitwiseRuns=new List<List<int[]>>();
        foreach(var code in new[]{bitwiseSource,bitwiseBack}){
            var asm=CompileActor(code);var type=asm.GetType("AuditBitwiseActor")!;var instance=Activator.CreateInstance(type)!;
            var target=Activator.CreateInstance(asm.GetType("AuditBitTarget")!)!;var driverType=asm.GetType("AuditBitDriver")!;var driver=Activator.CreateInstance(driverType)!;driverType.GetField("Target")!.SetValue(driver,target);
            var rows=new List<int[]>();
            foreach(var value in new[]{int.MinValue,int.MinValue+1,-17,-1,0,1,17,int.MaxValue})foreach(var count in new[]{int.MinValue,-65,-33,-32,-31,-1,0,1,2,30,31,32,33,63,64,int.MaxValue}){
                int Call(string name)=> (int)type.GetMethod(name)!.Invoke(instance,name=="Not"?[value]:[value,count])!;
                var values=new[]{value,count,Call("And"),Call("Or"),Call("Xor"),Call("Not"),Call("Left"),Call("Right"),Call("Local"),Call("FieldUpdate"),(int)driverType.GetMethod("Run")!.Invoke(driver,[value,count])!};rows.Add(values);
            }
            if((int)type.GetMethod("Loop")!.Invoke(instance,null)! !=31)throw new Exception("Signed shift loop termination differs");bitwiseRuns.Add(rows);
        }
        check(bitwiseRuns[0].Zip(bitwiseRuns[1]).All(pair=>pair.First.SequenceEqual(pair.Second)),"Bitwise int operators and local/field/component compound writes preserve 128 signed/count boundary rows after executed managed roundtrip");
        var bitwiseRoot=Path.GetFullPath("artifacts/bitwise-statement-check");Directory.CreateDirectory(bitwiseRoot);
        foreach(var file in bitwiseCpp)File.WriteAllText(Path.Combine(bitwiseRoot,file.Name),file.Code);
        File.WriteAllText(Path.Combine(bitwiseRoot,"original.cs"),bitwiseSource);File.WriteAllText(Path.Combine(bitwiseRoot,"restored.cs"),bitwiseBack);File.WriteAllText(Path.Combine(bitwiseRoot,"managed-rows.json"),System.Text.Json.JsonSerializer.Serialize(bitwiseRuns));
        var bitwiseCallBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class BitCall:MonoBehaviour{public int Next(){return 1;}public int Run(){return Next()<<Next();}}");}catch(NotSupportedException ex){bitwiseCallBlocked=ex.Message.Contains("평가 순서");}check(bitwiseCallBlocked,"Bitwise support retains call evaluation order guard");
        var alteredBitHelperBlocked=false;try{ActorScripts.ToUnity(bitwiseCpp.Select(f=>f with{Code=f.Code.Replace("uint32(EM_B)&31u","uint32(EM_B)&15u")}).ToArray());}catch(NotSupportedException){alteredBitHelperBlocked=true;}check(alteredBitHelperBlocked,"Reverse conversion rejects modified native shift helpers instead of silently restoring different behavior");
        var boolBitCompoundBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class BoolBit:MonoBehaviour{public bool Flag;public void Run(bool value){Flag&=value;}}");}catch(NotSupportedException ex){boolBitCompoundBlocked=ex.Message.Contains("int만");}check(boolBitCompoundBlocked,"Integer bitwise extension does not silently accept unsupported bool compound semantics");
        var dynamicSceneSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/dynamic-scene.cs.txt");var dynamicSceneCpp=ActorScripts.FromUnity(dynamicSceneSource);var dynamicSceneBack=ActorScripts.ToUnity(dynamicSceneCpp).Single().Code;
        var emptyUnitRoot=Path.GetFullPath("artifacts/empty-first-project-unit-"+Guid.NewGuid().ToString("N"));var emptyUnitPackage=Path.Combine(emptyUnitRoot,"package");var emptyUnitNative=Path.Combine(emptyUnitRoot,"Native/Plugins/EMActorPort/Source/EMActorPort/Public");var emptyUnitUnity=Path.Combine(emptyUnitRoot,"Unity");Directory.CreateDirectory(emptyUnitPackage);Directory.CreateDirectory(emptyUnitNative);Directory.CreateDirectory(Path.Combine(emptyUnitUnity,"Assets"));File.WriteAllText(Path.Combine(emptyUnitPackage,"package.json"),"{\"scripts\":[]}");var emptyUnitCode="using UnityEngine;public class EmptyFirstCatalog:MonoBehaviour{public int Value;void Update(){Value++;}}";foreach(var file in ActorScripts.FromUnity(emptyUnitCode))File.WriteAllText(Path.Combine(emptyUnitNative,file.Name),file.Code);SceneScriptPorting.RestoreInto(emptyUnitPackage,Path.Combine(emptyUnitRoot,"Native"),emptyUnitUnity,new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject{["name"]="EmptyFirstCatalog.cs",["source"]=emptyUnitCode,["guid"]="d0d0d0d0d0d0d0d0d0d0d0d0d0d0d0d0",["executionOrder"]=-50}));check(File.Exists(Path.Combine(emptyUnitUnity,"Assets/EngineMigratorScripts/EmptyFirstCatalog.cs"))&&File.ReadAllText(Path.Combine(emptyUnitUnity,"Assets/EngineMigratorScripts/EmptyFirstCatalog.cs.meta")).Contains("executionOrder: -50"),"A script-free first project unit cannot silently discard the merged native script catalog or its GUID/order");
        var sceneCatalog=new[]{new System.Text.Json.Nodes.JsonObject{["name"]="First",["path"]="Assets/First.unity"},new System.Text.Json.Nodes.JsonObject{["name"]="Second",["path"]="Assets/Second.unity"}};
        ProjectPorting.ValidateSceneCalls("using UnityEngine;using UnityEngine.SceneManagement;public class CatalogTravel:MonoBehaviour{public int Calls;public int Next(){Calls++;return Calls;}void Update(){SceneManager.LoadScene(Next());}}",sceneCatalog);check(true,"Project scene preflight accepts the supported runtime int overload without evaluating side effects");
        var foldedSceneBlocked=false;try{ProjectPorting.ValidateSceneCalls("using UnityEngine;using UnityEngine.SceneManagement;public class CatalogInvalid:MonoBehaviour{void Update(){SceneManager.LoadScene(1+1);}}",sceneCatalog);}catch(NotSupportedException){foldedSceneBlocked=true;}check(foldedSceneBlocked,"Folded constant out-of-range scene indices are blocked before target creation");
        var negativeSceneBlocked=false;try{ProjectPorting.ValidateSceneCalls("using UnityEngine;using UnityEngine.SceneManagement;public class CatalogNegative:MonoBehaviour{void Update(){SceneManager.LoadScene(-1);}}",sceneCatalog);}catch(NotSupportedException){negativeSceneBlocked=true;}check(negativeSceneBlocked,"Negative scene constants are validated semantically rather than bypassed as nonliteral syntax");
        ProjectPorting.ValidateSceneCalls("using UnityEngine;using UnityEngine.SceneManagement;public class CatalogNamed:MonoBehaviour{void Update(){const int next=1;SceneManager.LoadScene(next);SceneManager.LoadScene(0+1);}}",sceneCatalog);check(true,"Local and folded valid scene constants share the build catalog validation");
        const string dynamicSceneStub="namespace UnityEngine{public class MonoBehaviour{}}namespace UnityEngine.SceneManagement{public static class SceneManager{public static int Last;public static int Loads;public static void LoadScene(int value){Last=value;Loads++;}public static void LoadScene(string value){}}}";
        var sceneStates=new List<string>();
        foreach(var code in new[]{dynamicSceneSource,dynamicSceneBack}){var asm=CompileActor(code,dynamicSceneStub);var type=asm.GetType("DynamicSceneActor")!;var instance=Activator.CreateInstance(type)!;var scene=asm.GetType("UnityEngine.SceneManagement.SceneManager")!;var rows=new List<string>();foreach(var value in new[]{0,1,7})foreach(var method in new[]{"Direct","Called","Computed","Constant"}){type.GetField("Index")!.SetValue(instance,value);type.GetMethod(method)!.Invoke(instance,null);rows.Add(scene.GetField("Last")!.GetValue(null)+":"+scene.GetField("Loads")!.GetValue(null)+":"+type.GetField("Calls")!.GetValue(instance));}sceneStates.Add(string.Join("|",rows));}
        check(sceneStates[0]==sceneStates[1]&&sceneStates[0].EndsWith("1:12:3"),"Runtime int LoadScene preserves field/computed indices, constant locals and exactly one invocation through managed roundtrip");
        var sceneRoot=Path.GetFullPath("artifacts/dynamic-scene-check");Directory.CreateDirectory(sceneRoot);File.WriteAllText(Path.Combine(sceneRoot,"original.cs"),dynamicSceneSource);File.WriteAllText(Path.Combine(sceneRoot,"restored.cs"),dynamicSceneBack);File.WriteAllLines(Path.Combine(sceneRoot,"managed-states.txt"),sceneStates);
        foreach(var endings in new[]{"\n","\r\n"}){var variant=dynamicSceneCpp.Select(f=>f with{Code=f.Code.Replace("\r\n","\n").Replace("\r","\n").Replace("\n",endings)}).ToArray();check(ActorScripts.ToUnity(variant).Single().Code.Replace("\r\n","\n").Replace("\r","\n")==dynamicSceneBack.Replace("\r\n","\n").Replace("\r","\n"),"Generated runtime scene helper reverse validation accepts equivalent "+(endings=="\n"?"LF":"CRLF")+" line endings");}
        var alteredSceneHelperBlocked=false;try{ActorScripts.ToUnity(dynamicSceneCpp.Select(f=>f with{Code=f.Code.Replace("#%d","#%d1")}).ToArray());}catch(NotSupportedException){alteredSceneHelperBlocked=true;}check(alteredSceneHelperBlocked,"Reverse conversion rejects altered dynamic scene-index mapping helpers");
        var truthSource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/implicit-truth.cs.txt");var truthCpp=ActorScripts.FromUnity(truthSource);var truthBack=ActorScripts.ToUnity(truthCpp).Single().Code;
        const string truthStub="namespace UnityEngine{public class Object{public bool Destroyed;public static implicit operator bool(Object value)=>!ReferenceEquals(value,null)&&!value.Destroyed;public static bool operator ==(Object a,Object b){if(ReferenceEquals(a,null))return ReferenceEquals(b,null)||b.Destroyed;if(ReferenceEquals(b,null))return a.Destroyed;return ReferenceEquals(a,b);}public static bool operator !=(Object a,Object b)=>!(a==b);public override bool Equals(object value)=>ReferenceEquals(this,value);public override int GetHashCode()=>base.GetHashCode();}public class MonoBehaviour:Object{}public class GameObject:Object{}public class Transform:Object{}public class Canvas:Object{}}namespace UnityEngine.UI{public class Button:UnityEngine.Object{}}";
        check(truthCpp.Single().Code.Contains("#include \"UObject/Package.h\""),"Generated runtime headers explicitly include UPackage instead of depending on a project PCH");
        var legacyTruthCpp=truthCpp.Select(f=>f with{Code=System.Text.RegularExpressions.Regex.Replace(f.Code,"^#include \"UObject/Package.h\"\r?\n","",System.Text.RegularExpressions.RegexOptions.Multiline)}).ToArray();check(ActorScripts.ToUnity(legacyTruthCpp).Single().Code==truthBack,"Legacy generated runtime headers remain reverse-compatible after adding the direct UPackage include");
        var truthRuns=new List<List<int[]>>();
        foreach(var code in new[]{truthSource,truthBack}){
            var asm=CompileActor(code,truthStub);var type=asm.GetType("AuditImplicitTruth")!;var rows=new List<int[]>();
            foreach(var leftState in new[]{0,1,2,3})foreach(var rightState in new[]{0,1,2,3}){
                var actor=Activator.CreateInstance(type)!;
                object? Value(Type kind,int state){if(state==0)return null;var value=Activator.CreateInstance(kind)!;kind.GetField("Destroyed")!.SetValue(value,state==2);return value;}
                foreach(var field in new[]{"Target","Pose","CanvasTarget","ButtonTarget","Peer"}){var info=type.GetField(field)!;info.SetValue(actor,Value(info.FieldType,leftState));}
                var right=type.GetField("Right")!;right.SetValue(actor,Value(right.FieldType,rightState));
                int Call(string name,params object[] args)=>Convert.ToInt32(type.GetMethod(name)!.Invoke(actor,args));
                var row=new List<int>{leftState,rightState};foreach(var name in new[]{"Truth","Not","Branch","Local","Arg","Explicit","EqualTrue","PeerNotFalse","Loops","ReadFields","Lazy"})row.Add(Call(name));row.Add((int)type.GetField("Calls")!.GetValue(actor)!);row.Add(Call("LazyOr"));row.Add((int)type.GetField("Calls")!.GetValue(actor)!);row.Add(Call("Choose",true));row.Add(Call("Choose",false));rows.Add(row.ToArray());
            }
            truthRuns.Add(rows);
        }
        check(truthRuns[0].Zip(truthRuns[1]).All(p=>p.First.SequenceEqual(p.Second)),"Unity object truth conversion preserves null/live/destroyed states, five reference kinds, explicit/argument/local conversions and selected-only calls through managed roundtrip");
        var truthRoot=Path.GetFullPath("artifacts/implicit-truth-check");Directory.CreateDirectory(truthRoot);File.WriteAllText(Path.Combine(truthRoot,"original.cs"),truthSource);File.WriteAllText(Path.Combine(truthRoot,"restored.cs"),truthBack);File.WriteAllText(Path.Combine(truthRoot,"AuditImplicitTruth.h"),truthCpp.Single().Code);File.WriteAllText(Path.Combine(truthRoot,"managed-rows.json"),System.Text.Json.JsonSerializer.Serialize(truthRuns));
        var equalitySource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/object-equality.cs.txt");
        var equalityCpp=ActorScripts.FromUnity(equalitySource,true);var equalityBack=ActorScripts.ToUnity(equalityCpp,true).Single().Code;
        check(ActorScripts.FromUnity(equalitySource).Single().Code==equalityCpp.Single().Code,"Production conversion automatically selects GC-stable references for GameObject identity comparisons");
        check(ActorScripts.ToUnity(equalityCpp).Single().Code==equalityBack,"Weak-reference backend markers preserve GC-safe GameObject reverse conversion without changing legacy headers");
        var equalityRuns=new List<List<int[]>>();var equalityFlowRuns=new List<List<int[]>>();
        foreach(var code in new[]{equalitySource,equalityBack}){
            var asm=CompileActor(code,truthStub);var type=asm.GetType("AuditObjectEquality")!;var objectType=asm.GetType("UnityEngine.GameObject")!;var rows=new List<int[]>();var flows=new List<int[]>();
            foreach(var ls in Enumerable.Range(0,4))foreach(var mode in Enumerable.Range(0,5)){
                object? Make(int state){if(state==0)return null;var value=Activator.CreateInstance(objectType)!;objectType.GetField("Destroyed")!.SetValue(value,state==2);return value;}
                var actor=Activator.CreateInstance(type)!;var left=Make(ls);var right=mode==1?left:Make(mode==0?0:mode==3?2:1);
                type.GetField("Peer")!.SetValue(actor,actor);
                type.GetField("Left")!.SetValue(actor,left);type.GetField("Right")!.SetValue(actor,right);
                var row=new List<int>{ls,mode};foreach(var name in new[]{"Equal","Unequal","LeftMissing","RightMissing","LeftAlive","RightAlive","ViaLocals","Negated"})row.Add(Convert.ToInt32(type.GetMethod(name)!.Invoke(actor,null)));rows.Add(row.ToArray());
                int Flow(string name,params object[] args)=>Convert.ToInt32(type.GetMethod(name)!.Invoke(actor,args));
                flows.Add([ls,mode,Flow("ViaArguments"),Flow("ViaReturns"),Flow("ViaConditional",true),Flow("ViaConditional",false),Flow("ViaReflectedArguments"),Flow("ViaReflectedReturns"),Flow("ViaParameterCopy"),Flow("ResetThenCompare")]);
            }
            equalityRuns.Add(rows);equalityFlowRuns.Add(flows);
        }
        check(equalityRuns[0].Zip(equalityRuns[1]).All(p=>p.First.SequenceEqual(p.Second)),"GameObject identity comparisons preserve alias/distinct/null/dead managed wrappers in 20 roundtrip cases");
        check(equalityFlowRuns[0].Zip(equalityFlowRuns[1]).All(p=>p.First.SequenceEqual(p.Second)),"GameObject identity flows survive arguments, return values, conditionals and explicit null reassignment through managed roundtrip");
        check(equalityCpp.Single().Code.Contains("EM_GameObjectEqual(Left, Right)")&&equalityCpp.Single().Code.Contains("TWeakObjectPtr<AActor> Left")&&equalityRuns[0].Single(r=>r[0]==2&&r[1]==3)[2]==0&&equalityRuns[0].Single(r=>r[0]==2&&r[1]==0)[2]==1,"GameObject identity candidates retain weak index/serial, keeping distinct destroyed objects unequal and destroyed-vs-null equal");
        var equalityRoot=Path.GetFullPath("artifacts/object-equality-check");Directory.CreateDirectory(equalityRoot);File.WriteAllText(Path.Combine(equalityRoot,"restored.cs"),equalityBack);File.WriteAllText(Path.Combine(equalityRoot,"AuditObjectEquality.h"),equalityCpp.Single().Code);File.WriteAllText(Path.Combine(equalityRoot,"managed-rows.json"),System.Text.Json.JsonSerializer.Serialize(equalityRuns));
        File.WriteAllText(Path.Combine(equalityRoot,"managed-flow-rows.json"),System.Text.Json.JsonSerializer.Serialize(equalityFlowRuns));
        var equalityTamperRejected=false;try{ActorScripts.ToUnity(equalityCpp.Select(f=>f with{Code=f.Code.Replace("return EM_A==EM_B;","return true;")}).ToArray(),true);}catch(NotSupportedException){equalityTamperRejected=true;}
        check(equalityTamperRejected,"GameObject equality reverse conversion rejects tampered runtime equality helpers");
        var weakIdentityTamperRejected=false;try{ActorScripts.ToUnity(equalityCpp.Select(f=>f with{Code=f.Code.Replace("EM_A.HasSameIndexAndSerialNumber(EM_B)","EM_A.IsValid()==EM_B.IsValid()")}).ToArray());}catch(NotSupportedException){weakIdentityTamperRejected=true;}
        check(weakIdentityTamperRejected,"Weak identity helper tampering is rejected instead of accepting equal liveness as object identity");
        var weakParameterCopyTamperRejected=false;try{ActorScripts.ToUnity(equalityCpp.Select(f=>f with{Code=f.Code.Replace("TWeakObjectPtr<AActor> EM_arg_a = EM_in_a;","")}).ToArray());}catch(NotSupportedException){weakParameterCopyTamperRejected=true;}
        check(weakParameterCopyTamperRejected,"Weak reference input copy prefix is validated so C# by-value parameter semantics cannot silently change");
        var weakMarkerTamperRejected=false;try{ActorScripts.ToUnity(equalityCpp.Select(f=>f with{Code=f.Code.Replace("\"weakGameObjects\":true","\"weakGameObjects\":false")}).ToArray());}catch(NotSupportedException){weakMarkerTamperRejected=true;}
        check(weakMarkerTamperRejected,"Weak reference backend metadata cannot be downgraded while retaining weak identity code");
        var legacyNeighbor=ActorScripts.FromUnity("using UnityEngine;public class LegacyIdentityNeighbor:MonoBehaviour{public GameObject Target;public bool Alive(){return Target!=null;}}");
        check(ActorScripts.ToUnity(equalityCpp.Concat(legacyNeighbor).ToArray()).Count==2,"Mixed legacy and GC-stable Actor headers retain each class reference backend through reverse conversion");
        var mixedEqualityRejected=false;try{ActorScripts.FromUnity("using UnityEngine;public class MixedEquality:MonoBehaviour{public GameObject Target;public Transform Pose;public bool Compare(){return Target==Pose;}}",true);}catch(NotSupportedException){mixedEqualityRejected=true;}
        check(mixedEqualityRejected,"GameObject and Transform equality is rejected instead of comparing their collapsed native owner pointers");
        var equalityOrderRejected=false;try{ActorScripts.FromUnity("using UnityEngine;public class EqualityOrder:MonoBehaviour{public GameObject Target;public GameObject Next(){return Target;}public bool Compare(){return Next()==Next();}}",true);}catch(NotSupportedException){equalityOrderRejected=true;}
        check(equalityOrderRejected,"Stateful GameObject comparison operands retain the C# evaluation-order guard");
        var componentIdentitySource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/component-equality.cs.txt");
        var componentIdentityFiles=ActorScripts.FromUnityComponentsForValidation(componentIdentitySource);
        var componentIdentityNative=componentIdentityFiles.Single().Code;
        check(ActorScripts.FromUnity(componentIdentitySource).Single().Code==componentIdentityNative,"Public component equality conversion matches the GC-stable validation backend");
        check(componentIdentityNative.Contains("\"weakComponents\":true")&&componentIdentityNative.Contains("using EM_ComponentRef_AuditComponentEquality=TWeakObjectPtr<AActor>;") ,"Candidate component identity uses weak references and explicit backend metadata");
        var componentIdentityRestored=ActorScripts.ToUnity(componentIdentityFiles).Single().Code;
        check(componentIdentityRestored.Contains("AuditComponentEquality Left")&&componentIdentityRestored.Contains("a==b"),"Candidate component identity roundtrip preserves component types and local comparisons");
        var componentAliasTamperRejected=false;try{ActorScripts.ToUnity(componentIdentityFiles.Select(f=>f with{Code=f.Code.Replace("using EM_ComponentRef_AuditComponentEquality=TWeakObjectPtr<AActor>;","using EM_ComponentRef_AuditComponentEquality=AActor*;")}).ToArray());}catch(NotSupportedException){componentAliasTamperRejected=true;}
        check(componentAliasTamperRejected,"Candidate weak component aliases cannot be silently downgraded during reverse conversion");
        const string componentCallSource="using UnityEngine;public class ComponentCall:MonoBehaviour{public ComponentCall Other;public ComponentCall Echo(ComponentCall value){value=Other;return value;}public ComponentCall Invoke(){return Other.Echo(Other);}}";
        var componentCallFiles=ActorScripts.FromUnityComponentsForValidation(componentCallSource);
        var componentCallNative=componentCallFiles.Single().Code;
        check(componentCallNative.Contains("const TWeakObjectPtr<AActor>& EM_in_value")&&componentCallNative.Contains("TWeakObjectPtr<AActor> EM_arg_value = EM_in_value;"),"Candidate component inputs retain weak identity and C# by-value assignment semantics");
        check(ActorScripts.ToUnity(componentCallFiles).Single().Code.Contains("ComponentCall Echo(ComponentCall value)"),"Candidate reflected component calls restore their source return and input types");
        var transformIdentitySource=File.ReadAllText("tests/EngineMigrator.Checks/Fixtures/transform-equality.cs.txt");
        var transformIdentityFiles=ActorScripts.FromUnity(transformIdentitySource);var transformIdentityNative=transformIdentityFiles.Single().Code;
        check(transformIdentityNative.Contains("\"weakTransforms\":true")&&transformIdentityNative.Contains("using EM_TransformRef=TWeakObjectPtr<AActor>;"),"Transform comparisons select GC-stable owner identity with source-type aliases");
        var transformIdentityBack=ActorScripts.ToUnity(transformIdentityFiles).Single().Code;
        check(transformIdentityBack.Contains("Transform Echo(Transform value)")&&transformIdentityBack.Contains("Left.SetParent(Right,true)")&&transformIdentityBack.Contains("Left.localPosition"),"Transform arguments, returns, properties and parent operations survive reverse conversion");
        var transformAliasTamperRejected=false;try{ActorScripts.ToUnity(transformIdentityFiles.Select(f=>f with{Code=f.Code.Replace("using EM_TransformRef=TWeakObjectPtr<AActor>;","using EM_TransformRef=AActor*;")}).ToArray());}catch(NotSupportedException){transformAliasTamperRejected=true;}
        check(transformAliasTamperRejected,"Weak Transform aliases cannot be downgraded while retaining GC metadata");
        check(ActorScripts.ToUnity(transformIdentityFiles.Concat(legacyNeighbor).ToArray()).Count==2,"Weak Transform and legacy reference backends remain distinct in mixed-header reverse conversion");
        const string divisionSource="""
using UnityEngine;
public class DivisionActor:MonoBehaviour {
 public int Field;
 public int Q2(int value){return value/2;}
 public int QNeg2(int value){return value/-2;}
 public int Mod3(int value){return value%3;}
 public int ModNeg3(int value){return value%-3;}
 public int QMax(int value){const int Divisor=2147483647;return value/Divisor;}
 public int QMin(int value){const int Divisor=-2147483648;return value/Divisor;}
 public int ModMin(int value){const int Divisor=-2147483648;return value%Divisor;}
 public int Compound(int value){Field=value;Field/=2;Field%=3;int local=value;local/=-2;local%=5;return Field+local;}
 public int Loop(){int count=0;for(int value=64;value>0;value/=2){count++;}return count;}
}
""";
        var divisionCpp=ActorScripts.FromUnity(divisionSource);var divisionBack=ActorScripts.ToUnity(divisionCpp).Single().Code;
        var divisionStates=new List<string>();
        var dividends=new[]{int.MinValue,int.MinValue+1,-17,-1,0,1,17,int.MaxValue};
        foreach(var code in new[]{divisionSource,divisionBack}){
            var actor=CompileActor(code).GetType("DivisionActor")!;var obj=Activator.CreateInstance(actor)!;
            var rows=dividends.Select(v=>v+":"+string.Join(",",new[]{"Q2","QNeg2","Mod3","ModNeg3","QMax","QMin","ModMin","Compound"}.Select(n=>actor.GetMethod(n)!.Invoke(obj,[v]))));
            divisionStates.Add(string.Join("/",rows)+"/loop:"+actor.GetMethod("Loop")!.Invoke(obj,null));
        }
        check(divisionStates[0]==divisionStates[1]&&divisionStates[0].EndsWith("/loop:7"),"Safe constant integer division, signed remainder, field/local compound updates and loop division preserve boundary-value managed results");
        var divisionRoot=Path.GetFullPath("artifacts/division-statement-check");Directory.CreateDirectory(divisionRoot);
        File.WriteAllText(Path.Combine(divisionRoot,"original.cs"),divisionSource);File.WriteAllText(Path.Combine(divisionRoot,"DivisionActor.h"),divisionCpp.Single().Code);
        File.WriteAllText(Path.Combine(divisionRoot,"restored.cs"),divisionBack);File.WriteAllLines(Path.Combine(divisionRoot,"managed-states.txt"),divisionStates);
        var unsafeDivisorsBlocked=0;
        foreach(var operation in new[]{"/","%"})foreach(var divisor in new[]{"0","-1","divisor"}){
            try{ActorScripts.FromUnity("using UnityEngine;public class UnsafeDivision:MonoBehaviour{public int Read(int value,int divisor){return value"+operation+divisor+";}}");}catch(NotSupportedException){unsafeDivisorsBlocked++;}
        }
        check(unsafeDivisorsBlocked==6,"Zero, negative-one and dynamic integer divisors remain blocked for both quotient and remainder");
        const string componentDivisionSource="using UnityEngine;public class DivideCounter:MonoBehaviour{public int Value;}public class DivideDriver:MonoBehaviour{public DivideCounter Target;public void Run(){Target.Value/=2;Target.Value%=3;}}";
        var componentDivisionCpp=ActorScripts.FromUnity(componentDivisionSource);var componentDivisionBack=string.Join("\n",ActorScripts.ToUnity(componentDivisionCpp).Select(f=>f.Code));
        var componentDivisionStates=new List<int>();
        foreach(var code in new[]{componentDivisionSource,componentDivisionBack}){
            var asm=CompileActor(code);var target=Activator.CreateInstance(asm.GetType("DivideCounter")!)!;target.GetType().GetField("Value")!.SetValue(target,-17);var driver=Activator.CreateInstance(asm.GetType("DivideDriver")!)!;driver.GetType().GetField("Target")!.SetValue(driver,target);driver.GetType().GetMethod("Run")!.Invoke(driver,null);componentDivisionStates.Add((int)target.GetType().GetField("Value")!.GetValue(target)!);
        }
        check(componentDivisionStates.All(v=>v==-2),"Component field division and remainder compound writes preserve negative managed results after native-body restoration");
        var floatRemainderBlocked=false;try{ActorScripts.FromUnity("using UnityEngine;public class FloatRemainder:MonoBehaviour{public void Run(){float value=3f;value%=2f;}}");}catch(NotSupportedException ex){floatRemainderBlocked=ex.Message.Contains("int만");}
        check(floatRemainderBlocked,"Float compound remainder cannot enter the integer-only native operator path");
        const string privateSource="using UnityEngine; public class PrivateActor : MonoBehaviour { [SerializeField] private int health=7; public int Read(){return health;} public void Add(int amount){health+=amount;} }";
        var privateCpp=ActorScripts.FromUnity(privateSource);var privateBack=ActorScripts.ToUnity(privateCpp).Single().Code;
        check(privateCpp.Single().Code.Contains("AllowPrivateAccess")&&privateBack.Contains("[SerializeField]"),"Private SerializeField visibility and editor serialization metadata survive roundtrip");
        var privateStates=new List<int>();foreach(var code in new[]{privateSource,privateBack}){var type=CompileActor(code).GetType("PrivateActor")!;var instance=Activator.CreateInstance(type)!;type.GetMethod("Add")!.Invoke(instance,[9]);privateStates.Add((int)type.GetMethod("Read")!.Invoke(instance,null)!);}check(privateStates.All(v=>v==16),"Private serialized integer state executes identically after native-body restoration");
        const string shadowSource="using UnityEngine; public class ShadowActor : MonoBehaviour { public int Value=5; public int A=1; public int B=2; public int Bits; public int Reason; public int Step(int Value){int Bits=Value*2;this.Value+=Bits;return this.Value;} }";
        var shadowCpp=ActorScripts.FromUnity(shadowSource);var shadowBack=ActorScripts.ToUnity(shadowCpp).Single().Code;
        check(shadowCpp.Single().Code.Contains("EM_arg_Value")&&shadowCpp.Single().Code.Contains("EM_local_Bits")&&shadowCpp.Single().Code.Contains("EM_Reason"),"Generated helpers, user parameters and locals avoid native member shadowing");
        var shadowStates=new List<int>();foreach(var code in new[]{shadowSource,shadowBack}){var type=CompileActor(code).GetType("ShadowActor")!;shadowStates.Add((int)type.GetMethod("Step")!.Invoke(Activator.CreateInstance(type),[7])!);}check(shadowStates.All(v=>v==19),"Field/parameter/local shadowing retains C# symbol binding after native reverse conversion");
        var shadowRoot=Path.GetFullPath("artifacts/shadow-actor-check");Directory.CreateDirectory(shadowRoot);File.WriteAllText(Path.Combine(shadowRoot,"original.cs"),shadowSource);File.WriteAllText(Path.Combine(shadowRoot,"ShadowActor.h"),shadowCpp.Single().Code);
        const string movementSource="""
using UnityEngine;
public class MoveActor : MonoBehaviour {
 public Vector3 Velocity = new Vector3(1.25f,0.0f,0.5f);
 public Vector3 Last;
 public float SampleX;
 public void Step(){transform.position += Velocity * 0.25f;Last=transform.position;SampleX=Last.x;}
 public Vector3 Echo(Vector3 value){return value+Vector3.up;}
}
""";
        const string vectorRuntime="""
namespace UnityEngine {
 public class MonoBehaviour { public Transform transform=new Transform(); }
 public class Transform { public Vector3 position,localPosition; }
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new Vector3(0,0,0);public static Vector3 up=>new Vector3(0,1,0);public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);}
}
""";
        var movementCpp=ActorScripts.FromUnity(movementSource);var movementBack=ActorScripts.ToUnity(movementCpp).Single().Code;
        check(movementCpp.Single().Code.Contains("FVector3f Velocity")&&movementCpp.Single().Code.Contains("EM_SetPosition"),"Vector3 state and Transform position produce float vectors and meter/centimeter owner accessors");
        var movementStates=new List<float>();foreach(var code in new[]{movementSource,movementBack}){var type=CompileActor(code,vectorRuntime).GetType("MoveActor")!;var instance=Activator.CreateInstance(type)!;for(var i=0;i<3;i++)type.GetMethod("Step")!.Invoke(instance,null);movementStates.Add((float)type.GetField("SampleX")!.GetValue(instance)!);}
        check(movementStates.All(v=>v==0.9375f),"Vector math, Transform assignment and component reads survive reverse execution");
        var moveRoot=Path.GetFullPath("artifacts/move-actor-check");Directory.CreateDirectory(moveRoot);File.WriteAllText(Path.Combine(moveRoot,"original.cs"),movementSource);File.WriteAllText(Path.Combine(moveRoot,"MoveActor.h"),movementCpp.Single().Code);File.WriteAllText(Path.Combine(moveRoot,"MoveActor.cs"),movementBack);
        var root=Path.GetFullPath("artifacts/actor-check");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"original.cs"),source);
        foreach(var cls in CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()){Directory.CreateDirectory(Path.Combine(root,"Original"));File.WriteAllText(Path.Combine(root,"Original",cls.Identifier.ValueText+".cs"),"using UnityEngine;\n"+cls.ToFullString());}
        foreach(var f in cpp){Directory.CreateDirectory(Path.Combine(root,"Cpp"));File.WriteAllText(Path.Combine(root,"Cpp",f.Name),f.Code);}
        foreach(var f in restored){Directory.CreateDirectory(Path.Combine(root,"Restored"));File.WriteAllText(Path.Combine(root,"Restored",f.Name),f.Code);}
    }
}




