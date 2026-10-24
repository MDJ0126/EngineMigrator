// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;
using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;
[Serializable] public class EMProjectUnit {public string id,name,path,kind,package,map,actualPath,unityGuid;public bool enabled;}
[Serializable] public class EMProjectPackage {public int schema=1;public List<EMProjectUnit> units=new List<EMProjectUnit>();public List<string> blockers=new List<string>();public List<string> ignoredPaths=new List<string>();public bool projectPortComplete=false;}
[Serializable] public class EMProjectImportResult:EMImportResult {public List<EMProjectUnit> maps;}
[Serializable] public class EMPrefabLink {public string asset,root,member;}
public partial class EMNode {public EMPrefabLink prefab;}
public static partial class EngineMigratorBridge {
 static Scene ProjectExportScene;static string ProjectExportName,ProjectExportKind,ProjectExportPath;
 static bool ProjectImportMode;
 static Dictionary<string,Texture2D> ProjectTextures=new Dictionary<string,Texture2D>();
 static Dictionary<string,Material> ProjectMaterials=new Dictionary<string,Material>();
 static Dictionary<string,Mesh> ProjectMeshes=new Dictionary<string,Mesh>();
 static Dictionary<string,ScriptableObject> ProjectProfiles=new Dictionary<string,ScriptableObject>();
 static Dictionary<string,string> ProjectPrefabs=new Dictionary<string,string>();
 static string ProjectAssetId(string path,string prefix){var importer=AssetImporter.GetAtPath(path);const string key="EngineMigratorSourceId=";return importer&&importer.userData.StartsWith(key,StringComparison.Ordinal)?importer.userData.Substring(key.Length):prefix+AssetDatabase.AssetPathToGUID(path);}
 static string ObjectPath(Transform t)=>t.parent?ObjectPath(t.parent)+"/"+t.name:t.name;
 static void AuditReferences(Transform t,EMPackage package){foreach(var c in t.GetComponents<Component>())if(c){var so=new SerializedObject(c);var p=so.GetIterator();while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue==null&&p.objectReferenceInstanceIDValue!=0)package.blockers.Add((ProjectExportPath??t.gameObject.scene.path)+" :: "+ObjectPath(t)+" :: "+c.GetType().Name+"."+p.propertyPath+": 끊어진 참조입니다. 원본 Inspector에서 다시 지정해 주세요.");}}
 static void AuditYaml(string path,List<string> errors){
  if(!File.Exists(path))return;var lines=File.ReadAllLines(path);if(lines.Length==0||!lines[0].StartsWith("%YAML"))return;
  var ids=new HashSet<string>(Regex.Matches(string.Join("\n",lines),@"(?m)^--- !u!\d+ &(-?\d+)").Cast<Match>().Select(m=>m.Groups[1].Value));
  for(int i=0;i<lines.Length;i++){
   var external=Regex.Match(lines[i],@"(?:[:]|-)\s*\{fileID:\s*-?\d+,\s*guid:\s*([a-fA-F0-9]{32}),\s*type:\s*\d+\}");
   if(external.Success){var guid=external.Groups[1].Value;if(!guid.StartsWith("0000000000000000",StringComparison.Ordinal)&&string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))errors.Add(path+":"+(i+1)+" :: GUID "+guid+": 참조 자산이 없습니다. 원본 자산과 .meta를 복원하거나 Inspector 참조를 다시 지정해 주세요.");}
   var local=Regex.Match(lines[i],@"(?:[:]|-)\s*\{fileID:\s*(-?\d+)\s*\}");if(local.Success&&local.Groups[1].Value!="0"&&!ids.Contains(local.Groups[1].Value))errors.Add(path+":"+(i+1)+" :: fileID "+local.Groups[1].Value+": 같은 파일 안의 대상 객체가 없습니다. 끊어진 참조를 다시 지정해 주세요.");
  }
 }
 static void ExportPrefabLink(Transform t,EMNode node,EMPackage p,Dictionary<Transform,string> ids){
  if(ProjectExportKind=="prefab"||!PrefabUtility.IsPartOfPrefabInstance(t.gameObject))return;
  var root=PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);var nearest=PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
  if(root!=nearest){p.blockers.Add(ObjectPath(t)+": 중첩 프리팹 인스턴스는 아직 미지원입니다.");return;}
  var path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);if(PrefabUtility.GetPrefabAssetType(root)!=PrefabAssetType.Regular){p.blockers.Add(ObjectPath(t)+": Variant/Model 프리팹은 아직 미지원입니다.");return;}
  if(t.gameObject==root&&(PrefabUtility.GetRemovedComponents(root).Count>0||PrefabUtility.GetRemovedGameObjects(root).Count>0)){p.blockers.Add(ObjectPath(t)+": 프리팹 객체/컴포넌트 삭제 오버라이드는 아직 미지원입니다.");return;}
  var original=PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
  if(!original||(t.gameObject!=root&&(original.name!=t.name||original.transform.GetSiblingIndex()!=t.GetSiblingIndex()))){p.blockers.Add(ObjectPath(t)+": 프리팹 자식 추가·이름 변경·순서 변경은 아직 미지원입니다.");return;}
  var steps=new List<string>();for(var current=t;current!=root.transform;current=current.parent)steps.Insert(0,current.GetSiblingIndex().ToString());
  node.prefab=new EMPrefabLink{asset=ProjectAssetId(path,"p_"),root=ids[root.transform],member=string.Join("/",steps)};
 }
 static void ExportProject(){
  var originalRoot=PackageRoot;var manifest=new EMProjectPackage();var build=EditorBuildSettings.scenes;var scenes=AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>{var i=Array.FindIndex(build,s=>s.path==p);return i<0?int.MaxValue:i;}).ThenBy(p=>p,StringComparer.Ordinal);
  var tracked=Path.Combine(Application.dataPath,"../.EngineMigratorProject.json");if(File.Exists(tracked)){manifest=JsonUtility.FromJson<EMProjectPackage>(File.ReadAllText(tracked));manifest.blockers.Clear();foreach(var unit in manifest.units){var moved=string.IsNullOrEmpty(unit.unityGuid)?"":AssetDatabase.GUIDToAssetPath(unit.unityGuid);unit.path=string.IsNullOrEmpty(moved)?unit.actualPath:moved;if(unit.kind=="scene")unit.enabled=build.Any(s=>s.path==unit.path&&s.enabled);}
   foreach(var path in scenes.Where(p=>!manifest.ignoredPaths.Contains(p)&&!manifest.units.Any(u=>u.path==p)))manifest.units.Add(new EMProjectUnit{id=ProjectAssetId(path,"s_"),path=path,name=Path.GetFileNameWithoutExtension(path),kind="scene",enabled=build.Any(s=>s.path==path&&s.enabled)});
   foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)&&!manifest.ignoredPaths.Contains(p)&&!manifest.units.Any(u=>u.path==p)))manifest.units.Add(new EMProjectUnit{id=ProjectAssetId(path,"p_"),path=path,name=Path.GetFileNameWithoutExtension(path),kind="prefab"});
  }
  else{
   foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p,StringComparer.Ordinal))manifest.units.Add(new EMProjectUnit{id=ProjectAssetId(path,"p_"),path=path,name=Path.GetFileNameWithoutExtension(path),kind="prefab"});
   foreach(var path in scenes)manifest.units.Add(new EMProjectUnit{id=ProjectAssetId(path,"s_"),path=path,name=Path.GetFileNameWithoutExtension(path),kind="scene",enabled=build.Any(s=>s.path==path&&s.enabled)});
  }
  if(!manifest.units.Any(u=>u.kind=="scene"&&u.enabled))manifest.blockers.Add("활성 빌드 장면이 없습니다. Build Settings에 시작 장면을 지정해 주세요.");
  foreach(var unit in manifest.units){GameObject prefab=null;try{
   if(!Regex.IsMatch(unit.id,"^[A-Za-z0-9_]+$"))throw new Exception("Invalid project unit identity");unit.package="units/"+unit.id;PackageRoot=Path.Combine(originalRoot,unit.package);Directory.CreateDirectory(PackageRoot);ProjectExportName=unit.id;ProjectExportKind=unit.kind;ProjectExportPath=unit.path;
   foreach(var dependency in AssetDatabase.GetDependencies(unit.path,true))AuditYaml(dependency,manifest.blockers);
   if(unit.kind=="prefab"){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(unit.path);if(PrefabUtility.GetPrefabAssetType(asset)!=PrefabAssetType.Regular)throw new Exception("Variant/Model 프리팹은 아직 미지원입니다.");prefab=PrefabUtility.LoadPrefabContents(unit.path);if(prefab.GetComponentsInChildren<Transform>(true).Any(t=>t!=prefab.transform&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))throw new Exception("중첩 프리팹 정의는 아직 미지원입니다.");ProjectExportScene=prefab.scene;}
   else ProjectExportScene=EditorSceneManager.OpenScene(unit.path,OpenSceneMode.Single);
   Export();
  }catch(Exception ex){manifest.blockers.Add(unit.path+" :: "+ex.Message);}finally{if(prefab)PrefabUtility.UnloadPrefabContents(prefab);ProjectExportScene=default(Scene);ProjectExportName=ProjectExportKind=ProjectExportPath=null;PackageRoot=originalRoot;}}
  File.WriteAllText(Path.Combine(originalRoot,"project-package.json"),JsonUtility.ToJson(manifest,true));
  File.WriteAllText(Path.Combine(originalRoot,"preflight.json"),JsonUtility.ToJson(manifest,true));
  if(manifest.blockers.Count>0)throw new Exception("프로젝트 사전 검사 차단 "+manifest.blockers.Count+"건. preflight.json의 파일·객체·속성을 확인해 주세요.");
 }
 static void RestorePrefabLinks(Dictionary<string,GameObject> nodes,EMPackage p){
  foreach(var group in p.nodes.Where(n=>n.prefab!=null&&!string.IsNullOrEmpty(n.prefab.asset)).GroupBy(n=>n.prefab.root)){
   var identity=group.First().prefab.asset;if(!ProjectPrefabs.ContainsKey(identity))throw new Exception("프리팹 정의 누락: "+identity);var instance=nodes[group.Key];var asset=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPrefabs[identity]);
   var settings=new ConvertToPrefabInstanceSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,changeRootNameToAssetName=false,recordPropertyOverridesOfMatches=true};
   PrefabUtility.ConvertToPrefabInstance(instance,asset,settings,InteractionMode.AutomatedAction);
   if(instance.GetComponentsInChildren<Transform>(true).Length!=group.Count())throw new Exception("프리팹 재연결 중 예기치 않은 자식 객체가 생성되었습니다: "+group.Key);
   foreach(var node in group){var actual=instance.transform;if(node.prefab.member!="")foreach(var index in node.prefab.member.Split('/'))actual=actual.GetChild(int.Parse(index));if(actual.gameObject!=nodes[node.id])throw new Exception("프리팹 연결 중 객체 참조가 변경되었습니다: "+node.id);}
  }
 }
 static void VerifyPrefabLinks(Dictionary<string,GameObject> nodes,EMPackage p){foreach(var group in p.nodes.Where(n=>n.prefab!=null&&!string.IsNullOrEmpty(n.prefab.asset)).GroupBy(n=>n.prefab.root)){var root=nodes[group.Key];var expected=ProjectPrefabs[group.First().prefab.asset];if(!PrefabUtility.IsPartOfPrefabInstance(root)||PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)!=expected)throw new Exception("저장 후 프리팹 연결이 사라졌습니다: "+group.Key);}}
 static void ImportProject(){
  var originalRoot=PackageRoot;var manifest=JsonUtility.FromJson<EMProjectPackage>(File.ReadAllText(Path.Combine(originalRoot,"project-package.json")));if(manifest.schema!=1||manifest.blockers.Count>0)throw new Exception("Invalid project package");
  manifest.ignoredPaths=AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Concat(AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"})).Select(AssetDatabase.GUIDToAssetPath).ToList();
  ProjectImportMode=true;ProjectTextures.Clear();ProjectMaterials.Clear();ProjectMeshes.Clear();ProjectProfiles.Clear();ImportedUISprites.Clear();PhysicsMaterials.Clear();ProjectPrefabs.Clear();var scenes=new List<EditorBuildSettingsScene>();var results=new List<EMImportResult>();
  try{RegisterProjectPrefabAssets(manifest,originalRoot);foreach(var unit in manifest.units.OrderBy(u=>u.kind=="prefab"?0:1)){
   PackageRoot=Path.GetFullPath(Path.Combine(originalRoot,unit.package));if(!PackageRoot.StartsWith(originalRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Project package path escape");
   Import();var result=JsonUtility.FromJson<EMImportResult>(File.ReadAllText(Path.Combine(PackageRoot,"unity-import-result.json")));results.Add(result);
   if(unit.kind=="prefab"){var roots=SceneManager.GetActiveScene().GetRootGameObjects();if(roots.Length!=1)throw new Exception("프리팹 정의 루트는 하나여야 합니다.");var path=ProjectPrefabs[unit.id];PrefabUtility.SaveAsPrefabAsset(roots[0],path);var importer=AssetImporter.GetAtPath(path);importer.userData="EngineMigratorSourceId="+unit.id;importer.SaveAndReimport();unit.actualPath=path;}
   else {if(string.IsNullOrEmpty(unit.name)||unit.name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||unit.name=="."||unit.name=="..")throw new Exception("Unsafe scene name");var named=Path.GetDirectoryName(result.savedScene).Replace('\\','/')+"/"+unit.name+".unity";if(named!=result.savedScene){var moved=AssetDatabase.MoveAsset(result.savedScene,named);if(!string.IsNullOrEmpty(moved))throw new Exception("Scene name restoration failed: "+moved);result.savedScene=named;}scenes.Add(new EditorBuildSettingsScene(result.savedScene,unit.enabled));unit.actualPath=result.savedScene;}
   if(unit.kind=="prefab")manifest.ignoredPaths.Add(result.savedScene);unit.unityGuid=AssetDatabase.AssetPathToGUID(unit.actualPath);
   var importerScene=AssetImporter.GetAtPath(result.savedScene);importerScene.userData="EngineMigratorSourceId="+unit.id;importerScene.SaveAndReimport();
  }EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
  File.WriteAllText(Path.Combine(Application.dataPath,"../.EngineMigratorProject.json"),JsonUtility.ToJson(manifest,true));
  var combined=new EMProjectImportResult{maps=manifest.units,targetVersion=Application.unityVersion,savedScene=scenes.First(s=>s.enabled).path};foreach(var r in results){combined.bindings.AddRange(r.bindings);combined.warnings.AddRange(r.warnings);combined.behaviourInstancesVerified+=r.behaviourInstancesVerified;}File.WriteAllText(Path.Combine(originalRoot,"unity-import-result.json"),JsonUtility.ToJson(combined,true));
  }finally{PackageRoot=originalRoot;ProjectImportMode=false;}
 }
 static void RegisterProjectPrefabAssets(EMProjectPackage manifest,string root){
  // Register stable asset identities first, so forward, mutual and self
  // references resolve independently of the manifest's import order.
  foreach(var unit in manifest.units.Where(u=>u.kind=="prefab")){
   if(!Regex.IsMatch(unit.id,"^[A-Za-z0-9_]+$")||string.IsNullOrEmpty(unit.name)||unit.name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||unit.name=="."||unit.name=="..")throw new Exception("Unsafe prefab identity/name");
   var packagePath=Path.GetFullPath(Path.Combine(root,unit.package,"package.json"));if(!packagePath.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Project package path escape");
   var p=JsonUtility.FromJson<EMPackage>(File.ReadAllText(packagePath));var node=p.nodes.Single(n=>string.IsNullOrEmpty(n.parent));
   var folder="Assets/EngineMigratorProjectPrefabs/"+unit.id;var path=folder+"/"+unit.name+".prefab";
   if(File.Exists(path))throw new Exception("Target prefab path already exists: "+path);
   Directory.CreateDirectory(folder);AssetDatabase.Refresh();var placeholder=new GameObject(node.name);
   try{PrefabUtility.SaveAsPrefabAsset(placeholder,path);}finally{UnityEngine.Object.DestroyImmediate(placeholder);}
   ProjectPrefabs.Add(unit.id,path);
  }
 }
}
