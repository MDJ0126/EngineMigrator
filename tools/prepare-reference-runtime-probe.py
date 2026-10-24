"""Prepare a private real-engine reference conditional regression fixture."""
from pathlib import Path
import json

root = Path('artifacts/core-engine-launch-tests')
run = Path(json.loads((root/'paths.json').read_text())['run'])
source = Path('tests/EngineMigrator.Checks/Fixtures/reference-components.cs.txt').read_text(encoding='utf-8')
(run/'reference-source.cs').write_text(source,encoding='utf-8')
program=root/'runner/Program.cs'
text=program.read_text(encoding='utf-8-sig')
marker='var run=Path.GetFullPath(args[0]);'
if 'args[1]=="references"' not in text:
 text=text.replace(marker,'''if(args[1]=="references"){
 var home=Path.GetFullPath(args[0]);var files=ActorScripts.FromUnity(File.ReadAllText(Path.Combine(home,"reference-source.cs")));
 var dest=Path.Combine(home,"Project/Plugins/EMActorPort/Source/EMActorPort/Public");
 foreach(var file in files)File.WriteAllText(Path.Combine(dest,file.Name),file.Code);
 var restored=ActorScripts.ToUnity(files);Directory.CreateDirectory(Path.Combine(home,"reference-restored"));
 foreach(var file in restored)File.WriteAllText(Path.Combine(home,"reference-restored",file.Name),file.Code);
 Console.WriteLine("REFERENCE_HEADERS_AND_RESTORED_SOURCE_GENERATED");return;
}
'''+marker)
 program.write_text(text,encoding='utf-8')
text=program.read_text(encoding='utf-8')
if 'Unknown runner mode' not in text:
 text=text.replace('var json=JsonNode.Parse(File.ReadAllText(descriptor))!;', 'if(args[1]!="native")throw new ArgumentException("Unknown runner mode; refusing engine launch");\nvar json=JsonNode.Parse(File.ReadAllText(descriptor))!;')
 program.write_text(text,encoding='utf-8')
module=run/'Project/Plugins/EMActorPort/Source/EMActorPort'
(module/'Public/EMConditionalProbe.h').write_text('''#pragma once
#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "EMConditionalProbe.generated.h"
UCLASS()
class UEMConditionalProbe:public UBlueprintFunctionLibrary {
 GENERATED_BODY()
 public: UFUNCTION(BlueprintCallable,Category="EngineMigratorTest") static FString Run();
};
''',encoding='utf-8')
(module/'Private').mkdir(exist_ok=True)
(module/'Private/EMConditionalProbe.cpp').write_text('''#include "EMConditionalProbe.h"
#include "UObject/Package.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "AuditReferenceConditional.h"
#include "AuditUiConditional.h"
#include "AuditReferenceBase.h"
#include "AuditReferenceChild.h"
#include "AuditInheritedConditional.h"
FString UEMConditionalProbe::Run(){
 UWorld* World=nullptr;
 for(const auto& Context:GEngine->GetWorldContexts())if(Context.WorldType==EWorldType::Editor){World=Context.World();break;}
 if(!World)return TEXT("FAIL:editor world missing");
 TArray<AActor*> Owned;
 auto Spawn=[&](UClass* Class){AActor* Actor=World->SpawnActor<AActor>(Class);Owned.Add(Actor);return Actor;};
 TArray<FString> Errors;int32 Checks=0;
 auto Require=[&](bool Good,const TCHAR* Name){++Checks;if(!Good)Errors.Add(Name);};
 AActor* Left=Spawn(AActor::StaticClass());AActor* Right=Spawn(AActor::StaticClass());
 auto* Transform=Cast<AAuditReferenceConditional>(Spawn(AAuditReferenceConditional::StaticClass()));
 auto* Ui=Cast<AAuditUiConditional>(Spawn(AAuditUiConditional::StaticClass()));
 auto* Base=Cast<AAuditReferenceBase>(Spawn(AAuditReferenceBase::StaticClass()));
 auto* Child=Cast<AAuditReferenceChild>(Spawn(AAuditReferenceChild::StaticClass()));
 auto* Inherited=Cast<AAuditInheritedConditional>(Spawn(AAuditInheritedConditional::StaticClass()));
 if(!Left||!Right||!Transform||!Ui||!Base||!Child||!Inherited)return TEXT("FAIL:spawn missing");
 Ui->CanvasLeft=Left;Ui->CanvasRight=Right;Ui->ButtonLeft=Left;Ui->ButtonRight=Right;
 for(bool Choose:{true,false}){
  Require(Transform->TransformPick(Choose,Left,Right)==(Choose?Left:Right),TEXT("transform identity"));
  Require(Transform->TransformPick(Choose,nullptr,Right)==(Choose?nullptr:Right),TEXT("transform null left"));
  Require(Transform->TransformPick(Choose,Left,nullptr)==(Choose?Left:nullptr),TEXT("transform null right"));
  Require(Ui->PickCanvas(Choose)==(Choose?Left:Right),TEXT("canvas identity"));
  Require(Ui->PickButton(Choose)==(Choose?Left:Right),TEXT("button identity"));
  Require(Ui->OptionalCanvas(Choose)==(Choose?Left:nullptr),TEXT("canvas null"));
  Require(Ui->OptionalButton(Choose)==(Choose?nullptr:Right),TEXT("button null"));
  int32 Before=Ui->Calls;
  Require(Ui->LazyCanvas(Choose)==(Choose?Left:Right)&&Ui->Calls==Before+1,TEXT("canvas selected call"));
  Before=Ui->Calls;
  Require(Ui->LazyButton(Choose)==(Choose?Left:Right)&&Ui->Calls==Before+1,TEXT("button selected call"));
  Require(Inherited->Pick(Choose,Base,Child)==(Choose?static_cast<AActor*>(Base):static_cast<AActor*>(Child)),TEXT("base derived identity"));
  Require(Inherited->Optional(Choose,Base)==(Choose?Base:nullptr),TEXT("base null"));
  Before=Inherited->Calls;
  Require(Inherited->Lazy(Choose,Base,Child)==(Choose?static_cast<AActor*>(Base):static_cast<AActor*>(Child))&&Inherited->Calls==Before+1,TEXT("inherited selected call"));
 }
 Ui->SetCanvas(true,false);Require(!Ui->ReadCanvas(true)&&Ui->ReadCanvas(false),TEXT("canvas left write"));
 Ui->SetCanvas(false,false);Ui->SetCanvas(true,true);Require(Ui->ReadCanvas(true)&&!Ui->ReadCanvas(false),TEXT("canvas right write"));
 Ui->SetButton(false,false);Require(Ui->ReadButton(true)&&!Ui->ReadButton(false),TEXT("button right write"));
 Ui->SetButton(true,false);Ui->SetButton(false,true);Require(!Ui->ReadButton(true)&&Ui->ReadButton(false),TEXT("button left write"));
 for(AActor* Actor:Owned)if(IsValid(Actor))World->DestroyActor(Actor);
 return Errors.Num()?TEXT("FAIL:")+FString::Join(Errors,TEXT("|")):FString::Printf(TEXT("PASS:%d"),Checks);
}
''',encoding='utf-8')
probe=run/'probe.py'
text=probe.read_text(encoding='utf-8').replace("OUTPUT=ROOT/'native-feature-verification.json'","OUTPUT=ROOT/'reference-native-feature-verification.json'")
if "checks['nativeReferenceComponents']" not in text:
 text=text.replace(" OUTPUT.write_text(json.dumps({'status':'PASS'", " result=unreal.EMConditionalProbe.run()\n require(result=='PASS:28','Native component references: '+result);checks['nativeReferenceComponents']=result\n OUTPUT.write_text(json.dumps({'status':'PASS'")
 text=text.replace("'runtimeReferenceKinds':['GameObject']", "'runtimeReferenceKinds':['GameObject','Transform','Canvas','Button','InheritedComponent']").replace("'transformConditionalRuntimeVerified':False", "'transformConditionalRuntimeVerified':True").replace("'uiConditionalRuntimeVerified':False", "'uiConditionalRuntimeVerified':True")
probe.write_text(text,encoding='utf-8')
print(run)
