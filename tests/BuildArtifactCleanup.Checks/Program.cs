using EngineMigrator.Core;
var fixture=Path.Combine(AppContext.BaseDirectory,"fixtures","artifacts");
Directory.CreateDirectory(fixture);
var count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
string Setup(string name){var run=Path.Combine(fixture,name);Directory.CreateDirectory(Path.Combine(run,"Project","Intermediate","Build"));File.WriteAllText(Path.Combine(run,"Project","Demo.uproject"),"{}");File.WriteAllText(Path.Combine(run,"Project","Intermediate","Build","test.pch"),"cache");Directory.CreateDirectory(Path.Combine(run,"Project","Binaries"));File.WriteAllText(Path.Combine(run,"Project","Binaries","test.dll"),"binary");File.WriteAllText(Path.Combine(run,"evidence.json"),"evidence");return run;}
var good=Setup("success");File.WriteAllText(Path.Combine(good,"Project","Binaries","test.pdb"),"symbols");using(BuildArtifactCleanup.Begin(good)){}
Check(!Directory.Exists(Path.Combine(good,"Project","Intermediate")),"cache removed after success");Check(!File.Exists(Path.Combine(good,"Project","Binaries","test.pdb")),"build symbols removed");
Check(File.ReadAllText(Path.Combine(good,"evidence.json"))=="evidence"&&File.ReadAllText(Path.Combine(good,"Project","Binaries","test.dll"))=="binary","evidence and executable preserved");
var bad=Setup("failure");try{using var cleanup=BuildArtifactCleanup.Begin(bad);throw new OperationCanceledException();}catch(OperationCanceledException){}
Check(!Directory.Exists(Path.Combine(bad,"Project","Intermediate")),"cache removed on cancellation");
var busy=Setup("busy");using(var lease=File.Open(Path.Combine(fixture,"offline-editor-runtime.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){using(BuildArtifactCleanup.Begin(busy)){}Check(Directory.Exists(Path.Combine(busy,"Project","Intermediate")),"active engine lease prevents deletion");}
using(BuildArtifactCleanup.Begin(busy)){}Check(!Directory.Exists(Path.Combine(busy,"Project","Intermediate")),"cleanup succeeds when lease released");
var outside=Path.Combine(Path.GetTempPath(),"EngineMigrator-cleanup-outside-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(outside,"Intermediate"));File.WriteAllText(Path.Combine(outside,"Demo.uproject"),"{}");using(BuildArtifactCleanup.Begin(outside)){}Check(Directory.Exists(Path.Combine(outside,"Intermediate")),"outside artifacts retained");
var unknown=Path.Combine(fixture,"unknown","Intermediate");Directory.CreateDirectory(unknown);using(BuildArtifactCleanup.Begin(Path.GetDirectoryName(unknown)!)){}Check(Directory.Exists(unknown),"unidentified directories retained");


var unity=Path.Combine(fixture,"unity");foreach(var part in new[]{"Assets","ProjectSettings","Library/Artifacts","Library/Bee","Library/PackageCache","Library/PackageManager"})Directory.CreateDirectory(Path.Combine(unity,part));
File.WriteAllText(Path.Combine(unity,"Library/Artifacts/cache"),"cache");File.WriteAllText(Path.Combine(unity,"Library/PackageCache/dependency"),"dependency");
using(BuildArtifactCleanup.Begin(unity)){}
Check(!Directory.Exists(Path.Combine(unity,"Library/Artifacts"))&&!Directory.Exists(Path.Combine(unity,"Library/Bee")),"Unity generated caches removed");
Check(File.ReadAllText(Path.Combine(unity,"Library/PackageCache/dependency"))=="dependency"&&Directory.Exists(Path.Combine(unity,"Library/PackageManager")),"offline Unity packages retained");

Directory.Delete(outside,true);
Console.WriteLine($"{count} cleanup checks passed");

Directory.Delete(Path.Combine(AppContext.BaseDirectory,"fixtures"),true);
