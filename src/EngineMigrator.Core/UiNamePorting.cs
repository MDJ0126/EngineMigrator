namespace EngineMigrator.Core;
internal static class UiNamePorting
{
    internal const string Helpers="""
protected:
static void EM_UiSetObjectName(AActor* EM_Value,const TCHAR* EM_Name){EM_SetObjectName(EM_UiOwner(EM_Value),EM_Name);}

""";
}
