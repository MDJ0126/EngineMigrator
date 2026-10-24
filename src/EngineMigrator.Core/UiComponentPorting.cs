namespace EngineMigrator.Core;
internal static class UiComponentPorting
{
    internal static string RuntimeHelpers=>RuntimeUiPrefabPorting.RuntimeHelpers+Api;
    private const string Api="""
using EM_UIRef_Canvas=AActor*;
using EM_UIRef_Button=AActor*;
static AActor* EM_UiGetCanvas(AActor* EM_Owner){return IsValid(EM_Owner)&&EM_Owner->Tags.Contains(TEXT("EMUI:Canvas"))?EM_Owner:nullptr;}
static AActor* EM_UiGetButton(AActor* EM_Owner){return IsValid(EM_Owner)&&EM_Owner->Tags.Contains(TEXT("EMUI:Button"))?EM_Owner:nullptr;}
static bool EM_UiReadCanvasEnabled(AActor* EM_Owner){return !EM_Owner->Tags.Contains(TEXT("EMUICanvasDisabled"));}
static bool EM_UiReadButtonInteractable(AActor* EM_Owner){return !EM_Owner->Tags.Contains(TEXT("EMUIButtonNonInteractable"));}
static bool EM_UiReadButtonEnabled(AActor* EM_Owner){return !EM_Owner->Tags.Contains(TEXT("EMUIButtonDisabled"));}
static void EM_UiFlag(AActor* EM_Owner,const TCHAR* EM_Tag,bool EM_Enabled){if(EM_Enabled)EM_Owner->Tags.Remove(FName(EM_Tag));else EM_Owner->Tags.AddUnique(FName(EM_Tag));EM_RefreshRuntimeUI(EM_Owner);}
static void EM_UiWriteCanvasEnabled(AActor* EM_Owner,bool EM_Enabled){EM_UiFlag(EM_Owner,TEXT("EMUICanvasDisabled"),EM_Enabled);}
static void EM_UiWriteButtonInteractable(AActor* EM_Owner,bool EM_Enabled){EM_UiFlag(EM_Owner,TEXT("EMUIButtonNonInteractable"),EM_Enabled);}
static void EM_UiWriteButtonEnabled(AActor* EM_Owner,bool EM_Enabled){EM_UiFlag(EM_Owner,TEXT("EMUIButtonDisabled"),EM_Enabled);}
static AActor* EM_UiOwner(AActor* EM_Owner){return EM_Owner;}

""";
}
