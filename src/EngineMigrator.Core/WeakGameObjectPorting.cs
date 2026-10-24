namespace EngineMigrator.Core;
internal static class WeakGameObjectPorting
{
    internal static string ParameterCopyPrefix(IEnumerable<string> names)
    {
        var lines=names.Select(n=>"TWeakObjectPtr<AActor> EM_arg_"+n+" = EM_in_"+n+";").ToArray();
        return lines.Length==0?"":"\n"+string.Join("\n",lines)+"\n";
    }
    // UE weak references retain index+serial after collection. Do not compare Get()
    // results: distinct stale references would both become nullptr.
    internal const string Helpers="""
protected:
static bool EM_ObjectValid(const TWeakObjectPtr<AActor>& EM_Value){return EM_Value.IsValid();}
static bool EM_ObjectValid(const TObjectPtr<AActor>& EM_Value){return IsValid(EM_Value.Get());}
static AActor* EM_NativeGameObject(const TWeakObjectPtr<AActor>& EM_Value){return EM_Value.Get();}
static TWeakObjectPtr<AActor> EM_ReferenceValue(const TWeakObjectPtr<AActor>& EM_Value){return EM_Value;}
static AActor* EM_ReferenceValue(const TObjectPtr<AActor>& EM_Value){return EM_Value.Get();}
static bool EM_GameObjectEqual(const TWeakObjectPtr<AActor>& EM_A,const TWeakObjectPtr<AActor>& EM_B){if(EM_A.IsExplicitlyNull())return !EM_B.IsValid();if(EM_B.IsExplicitlyNull())return !EM_A.IsValid();return EM_A.HasSameIndexAndSerialNumber(EM_B);}

""";
    private const string OldWrite="else CastFieldChecked<FObjectPropertyBase>(EM_P)->SetObjectPropertyValue(EM_Data,EM_Value);";
    private const string NewWrite="else if constexpr(std::is_same_v<EM_T,TWeakObjectPtr<AActor>>)EM_P->CopyCompleteValue(EM_Data,&EM_Value);else CastFieldChecked<FObjectPropertyBase>(EM_P)->SetObjectPropertyValue(EM_Data,EM_Value);";
    private const string OldReturn="else return Cast<AActor>(CastFieldChecked<FObjectPropertyBase>(EM_Result)->GetObjectPropertyValue(EM_Data));";
    private const string NewReturn="else if constexpr(std::is_same_v<EM_Return,TWeakObjectPtr<AActor>>){EM_Return EM_Value;EM_Result->CopyCompleteValue(&EM_Value,EM_Data);return EM_Value;}else return Cast<AActor>(CastFieldChecked<FObjectPropertyBase>(EM_Result)->GetObjectPropertyValue(EM_Data));";
    internal static string AdaptRuntime(string code)=>code.Replace(OldWrite,NewWrite).Replace(OldReturn,NewReturn);
    internal static string RestoreRuntime(string code)=>code.Replace(NewWrite,OldWrite).Replace(NewReturn,OldReturn);
}
