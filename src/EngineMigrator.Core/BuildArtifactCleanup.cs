using System.Text.Json;
namespace EngineMigrator.Core;

// Runs after the entire conversion, including failure/cancellation. Never removes evidence or binaries.
internal sealed class BuildArtifactCleanup : IDisposable
{
    private readonly string run;
    private BuildArtifactCleanup(string path) { run=Path.GetFullPath(path); }
    internal static IDisposable Begin(string path) => new BuildArtifactCleanup(path);
    public void Dispose()
    {
        if(!Directory.Exists(run)) return;
        var records=new List<object>();
        try
        {
            var current=new DirectoryInfo(run); DirectoryInfo? artifacts=null;
            for(var d=current;d is not null;d=d.Parent)
            {
                if((d.Attributes&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked cleanup path rejected");
                if(d.Name.Equals("artifacts",StringComparison.OrdinalIgnoreCase)){artifacts=d;break;}
            }
            // Public outputs outside private artifacts are retained.
            if(artifacts is null) return;
            using var lease=File.Open(Path.Combine(artifacts.FullName,"offline-editor-runtime.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            var pending=new Stack<string>();pending.Push(run);
            while(pending.Count>0)
            {
                var folder=pending.Pop();
                if(folder.Split(Path.DirectorySeparatorChar).Any(p=>p.Equals("Binaries",StringComparison.OrdinalIgnoreCase)||p.Equals("bin",StringComparison.OrdinalIgnoreCase)))
                {
                    foreach(var symbol in Directory.EnumerateFiles(folder,"*.pdb"))
                    {
                        if((File.GetAttributes(symbol)&FileAttributes.ReparsePoint)!=0) continue;
                        try{File.Delete(symbol);records.Add(new{path=symbol,status="removed"});}
                        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){records.Add(new{path=symbol,status="failed",error=ex.Message});}
                    }
                }

                var unityLibrary=Path.GetFileName(folder).Equals("Library",StringComparison.OrdinalIgnoreCase)&&Directory.Exists(Path.Combine(Path.GetDirectoryName(folder)!,"Assets"))&&Directory.Exists(Path.Combine(Path.GetDirectoryName(folder)!,"ProjectSettings"));
                var project=Directory.EnumerateFiles(folder).Any(p=>Path.GetExtension(p).Equals(".uproject",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(p).Equals(".uplugin",StringComparison.OrdinalIgnoreCase));
                foreach(var child in Directory.EnumerateDirectories(folder))
                {
                    if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0) continue;
                    if(unityLibrary&&new[]{"PackageCache","PackageManager"}.Contains(Path.GetFileName(child),StringComparer.OrdinalIgnoreCase)) continue;
                    if((project&&Path.GetFileName(child).Equals("Intermediate",StringComparison.OrdinalIgnoreCase))||unityLibrary)
                    {
                        try
                        {
                            // Refuse the entire cache if it contains a link.
                            var scan=new Stack<string>();scan.Push(child);
                            while(scan.Count>0)
                            {
                                foreach(var item in Directory.EnumerateFileSystemEntries(scan.Pop()))
                                {
                                    var attributes=File.GetAttributes(item);
                                    if((attributes&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked cache entry rejected");
                                    if((attributes&FileAttributes.Directory)!=0) scan.Push(item);
                                }
                            }
                            Directory.Delete(child,true);records.Add(new{path=child,status="removed"});
                        }
                        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){records.Add(new{path=child,status="failed",error=ex.Message});}
                    }
                    else pending.Push(child);
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){records.Add(new{path=run,status="deferred",error=ex.Message});}
        try { File.WriteAllText(Path.Combine(run,"cache-cleanup.json"),JsonSerializer.Serialize(new{finished=DateTimeOffset.Now,items=records},new JsonSerializerOptions{WriteIndented=true})); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Console.Error.WriteLine("Cache cleanup receipt failed: "+ex.Message);}
    }
}
