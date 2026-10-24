// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[Serializable] public class EMTexture { public string id, name, file; public bool srgb = true; public string filter = "Bilinear", wrapU = "Repeat", wrapV = "Repeat"; public bool mipmaps; }
[Serializable] public class EMMaterial { public string id, name, texture,sourceShader; public string shader = "unlit"; public Color color = Color.white; public float metallic, smoothness; }
[Serializable] public class EMMesh { public string id, name, file; public Vector3[] vertices, normals; public Vector2[] uv; public int[] triangles; }
[Serializable] public partial class EMNode { public string id, name, parent, mesh; public string[] materials; public Vector3 position, scale; public Quaternion rotation; public bool active = true; }
[Serializable] public partial class EMPackage
{
    public int schema = 1; public string sourceEngine, sourceVersion, scene;
    public List<EMTexture> textures = new List<EMTexture>(); public List<EMMaterial> materials = new List<EMMaterial>();
    public List<EMMesh> meshes = new List<EMMesh>(); public List<EMNode> nodes = new List<EMNode>(); public List<string> blockers = new List<string>();
}
[Serializable] public class EMBinding { public string sourceId, target, kind; public string[] references; }
[Serializable] public class EMImportResult { public bool savedAndReloaded = true, referencesVerified = true, sceneComponentsVerified=true,renderingEquivalent=false,projectPortComplete = false; public int behaviourInstancesVerified;public List<string> warnings=new List<string>();public string targetVersion, savedScene; public List<EMBinding> bindings = new List<EMBinding>(); }

public static partial class EngineMigratorBridge
{
    static string Arg(string key) { var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, key); if (i < 0 || i + 1 >= a.Length) throw new Exception("Missing " + key); return a[i + 1]; }
    static string PackageRoot;
    static string ResourceAssetPath(string root,string kind,string id,string name,string extension)
    {
        if(string.IsNullOrEmpty(name))name=id;
        if(name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new Exception("Unsafe resource name: "+name);
        var folder=root+"/"+kind+"/"+id;Directory.CreateDirectory(folder);AssetDatabase.Refresh();return folder+"/"+name+extension;
    }
    static string PackageFile(string relative)
    {
        var file = Path.GetFullPath(Path.Combine(PackageRoot, relative));
        if (!file.StartsWith(PackageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Package path escape");
        return file;
    }
    static string Id(Object obj)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out long local)) throw new Exception("No persistent asset ID: " + obj.name);
        return guid + "_" + local.ToString(CultureInfo.InvariantCulture).Replace('-', 'n');
    }
    public static void Run()
    {
        PackageRoot = Path.GetFullPath(Arg("-emPackage")); Directory.CreateDirectory(PackageRoot);
        switch (Arg("-emMode"))
        {
            case "fixture": Fixture(); Export(); break;
            case "export": Export(); break;
            case "export-project": ExportProject(); break;
            case "import-project": ImportProject(); break;
            case "import": Import(); break;
            default: throw new Exception("Unknown mode");
        }
        Debug.Log("ENGINE_MIGRATOR_BRIDGE_OK");
    }
    static void Fixture()
    {
        Directory.CreateDirectory("Assets/PortSource"); AssetDatabase.Refresh();
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.red, Color.green, Color.white, Color.yellow }); tex.Apply();
        File.WriteAllBytes("Assets/PortSource/Checker.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/PortSource/Checker.png"); importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapModeU = TextureWrapMode.Clamp; importer.wrapModeV = TextureWrapMode.Mirror; importer.SaveAndReimport();
        var material = new Material(Shader.Find("Unlit/Texture")); material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PortSource/Checker.png"); AssetDatabase.CreateAsset(material, "Assets/PortSource/Checker.mat");
        var mesh = new Mesh { name = "PortTetrahedron" };
        mesh.vertices = new[] { new Vector3(0,0,0), new Vector3(3,0,0), new Vector3(0,2,0), new Vector3(0,0,1) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0,2,1, 0,1,3, 0,3,2, 1,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, "Assets/PortSource/Tetrahedron.asset");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PortRoot"); root.transform.position = new Vector3(2, 1, 4); root.transform.rotation = Quaternion.Euler(0,30,0); root.transform.localScale = Vector3.one * 1.25f;
        var child = new GameObject("PortMesh"); child.transform.SetParent(root.transform, false); child.transform.localPosition = new Vector3(1, 2, 3); child.transform.localRotation = Quaternion.Euler(15,0,0); child.transform.localScale = new Vector3(1,2,1);
        child.AddComponent<MeshFilter>().sharedMesh = mesh; child.AddComponent<MeshRenderer>().sharedMaterial = material;
        var pbr = new Material(Shader.Find("Standard")); pbr.mainTexture = material.mainTexture; pbr.color = new Color(0.7f,0.3f,0.2f,1); pbr.SetFloat("_Metallic",0.25f); pbr.SetFloat("_Glossiness",0.6f); AssetDatabase.CreateAsset(pbr,"Assets/PortSource/Pbr.mat");
        var pbrObject = new GameObject("PortPbrMesh"); pbrObject.transform.SetParent(root.transform,false); pbrObject.transform.localPosition = new Vector3(-2,3,0); pbrObject.AddComponent<MeshFilter>().sharedMesh = mesh; pbrObject.AddComponent<MeshRenderer>().sharedMaterial = pbr;
        EditorSceneManager.SaveScene(scene, "Assets/PortSource/PortScene.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/PortSource/PortScene.unity", true) }; AssetDatabase.SaveAssets();
    }
    static void Export()
    {
        var p = new EMPackage { sourceEngine = "Unity", sourceVersion = Application.unityVersion };
        ExportTiming(p);
        p.gravity=Physics.gravity;
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (!ProjectExportScene.IsValid() && scenes.Length != 1) throw new Exception("여러 장면은 프로젝트 단위 포팅을 사용해 주세요.");
        var scene = ProjectExportScene.IsValid()?ProjectExportScene:EditorSceneManager.OpenScene(scenes[0].path, OpenSceneMode.Single); p.scene = ProjectExportName??scene.name;
        if(ProjectExportPath==null&&!string.IsNullOrEmpty(scene.path))foreach(var dependency in AssetDatabase.GetDependencies(scene.path,true))AuditYaml(dependency,p.blockers);
        Directory.CreateDirectory(Path.Combine(PackageRoot, "textures")); Directory.CreateDirectory(Path.Combine(PackageRoot, "meshes"));
        var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        var nodeIds = SceneNodeIds(scene,transforms);
        foreach (var t in transforms)
        {
            foreach (var component in t.GetComponents<Component>())
                if (!IsSceneComponentSupported(component)) p.blockers.Add((ProjectExportPath??scene.path)+" :: "+ObjectPath(t)+": 미지원 컴포넌트 " + (component ? component.GetType().FullName : "MissingScript")+" — 해당 컴포넌트의 동작을 지원 코드로 분리하거나 원본의 Missing Script를 복원해 주세요.");
            var n = new EMNode { id = nodeIds[t], name = t.name, parent = t.parent ? nodeIds[t.parent] : null, position = t.position, rotation = t.rotation, scale = t.lossyScale, active = t.gameObject.activeSelf, materials = new string[0] };
            ExportSceneComponents(t.gameObject,n,p);
            ExportPhysics(t.gameObject,n,p);
            ExportAnimation(t.gameObject,n,p,nodeIds);
            ExportUI(t.gameObject,n,p,nodeIds);
            ExportBehaviours(t.gameObject,n,p,nodeIds);
            ExportPrefabLink(t,n,p,nodeIds);
            AuditReferences(t,p);
            var matrix = t.localToWorldMatrix;
            var xAxis = ((Vector3)matrix.GetColumn(0)).normalized; var yAxis = ((Vector3)matrix.GetColumn(1)).normalized; var zAxis = ((Vector3)matrix.GetColumn(2)).normalized;
            if (Mathf.Abs(Vector3.Dot(xAxis,yAxis)) > 0.00001f || Mathf.Abs(Vector3.Dot(xAxis,zAxis)) > 0.00001f || Mathf.Abs(Vector3.Dot(yAxis,zAxis)) > 0.00001f) p.blockers.Add(t.name + ": shear transform 미지원");
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            if (mf && mf.sharedMesh)
            {
                var mesh = mf.sharedMesh; n.mesh = Id(mesh);
                if (mesh.subMeshCount != 1) p.blockers.Add(t.name + ": 다중 서브메시 미지원");
                else if (!p.meshes.Any(m => m.id == n.mesh))
                {
                    if(mesh.GetTopology(0)!=MeshTopology.Triangles){p.blockers.Add(t.name+": 삼각형 외 메시 토폴로지 미지원");continue;}
                    var em = ReadMesh(mesh,n.mesh);
                    p.meshes.Add(em); WriteObj(em);
                }
            }
            if (mr)
            {
                n.hasRenderer=true;n.rendererEnabled=mr.enabled;n.castShadows=mr.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off;
                if(mr.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.On&&mr.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off)p.blockers.Add(t.name+": 양면/그림자 전용 렌더러 미지원");
                if(!mr.receiveShadows)p.blockers.Add(t.name+": 개별 그림자 수신 해제 매핑 미지원");
                n.materials = mr.sharedMaterials.Select(m => m ? Id(m) : null).ToArray();
                foreach (var mat in mr.sharedMaterials.Where(m => m))
                {
                    var mid = Id(mat); if (p.materials.Any(m => m.id == mid)) continue;
                    var urp=mat.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal);
                    if(mat.HasProperty("_Cull")&&mat.GetFloat("_Cull")!=2){p.blockers.Add(mat.name+": 양면/앞면 컬링 머티리얼 매핑 미지원");continue;}
                    if (!new[]{"Unlit/Texture","Unlit/Color","Standard","Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit"}.Contains(mat.shader.name)) { p.blockers.Add(mat.name + ": 미지원 셰이더 " + mat.shader.name); continue; }
                    var em = new EMMaterial { id = mid, name = mat.name,sourceShader=mat.shader.name, color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.color : Color.white };
                    if(urp&&((mat.HasProperty("_Surface")&&mat.GetFloat("_Surface")!=0)||(mat.HasProperty("_AlphaClip")&&mat.GetFloat("_AlphaClip")!=0))){p.blockers.Add(mat.name+": URP 투명/알파 클립 미지원");continue;}
                    if (mat.shader.name == "Standard"||mat.shader.name=="Universal Render Pipeline/Lit")
                    {
                        em.shader = "standard"; em.metallic = mat.GetFloat("_Metallic"); em.smoothness = mat.GetFloat(urp?"_Smoothness":"_Glossiness");
                        if ((!urp&&mat.GetFloat("_Mode") != 0)||(urp&&mat.HasProperty("_WorkflowMode")&&mat.GetFloat("_WorkflowMode")!=1)||new[] { "_MetallicGlossMap", "_BumpMap", "_ParallaxMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap", "_ClearCoatMap" }.Any(key => mat.HasProperty(key) && mat.GetTexture(key))||(mat.HasProperty("_ClearCoatMask")&&mat.GetFloat("_ClearCoatMask")!=0)) { p.blockers.Add(mat.name + ": 투명/추가 PBR 맵 변환 미구현"); continue; }
                        if (mat.IsKeywordEnabled("_EMISSION") && mat.GetColor("_EmissionColor").maxColorComponent > 0) { p.blockers.Add(mat.name + ": emission 변환 미구현"); continue; }
                    }
                    if ((mat.HasProperty("_MainTex")||mat.HasProperty("_BaseMap")) && mat.mainTexture)
                    {
                        if (mat.mainTextureScale != Vector2.one || mat.mainTextureOffset != Vector2.zero) { p.blockers.Add(mat.name + ": UV tiling/offset 변환 미구현"); continue; }
                        var texture = mat.mainTexture; em.texture = PersistedResourceId(texture);
                        if (!p.textures.Any(tx => tx.id == em.texture))
                        {
                            var input = AssetDatabase.GetAssetPath(texture);
                            if (!new[]{".png",".jpg",".jpeg",".tga"}.Contains(Path.GetExtension(input).ToLowerInvariant())) { p.blockers.Add(input + ": 텍스처 형식 미지원 (PNG/JPEG/TGA 지원)"); continue; }
                            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(input);
                            if ((textureImporter.textureType != TextureImporterType.Default && textureImporter.textureType != TextureImporterType.Sprite) || textureImporter.wrapModeU == TextureWrapMode.MirrorOnce || textureImporter.wrapModeV == TextureWrapMode.MirrorOnce) { p.blockers.Add(input + ": 텍스처 유형/주소 모드 미지원"); continue; }
                            var data = new EMTexture { id = em.texture, name = texture.name, file = "textures/" + em.texture + ".png", srgb = textureImporter.sRGBTexture, filter = textureImporter.filterMode.ToString(), wrapU = textureImporter.wrapModeU.ToString(), wrapV = textureImporter.wrapModeV.ToString(), mipmaps = textureImporter.mipmapEnabled };
                            if(Path.GetExtension(input).Equals(".png",StringComparison.OrdinalIgnoreCase))File.Copy(input, Path.Combine(PackageRoot, data.file), true);
                            else ExportTexturePng(input,textureImporter,Path.Combine(PackageRoot,data.file));
                            p.textures.Add(data);
                        }
                    }
                    p.materials.Add(em);
                }
            }
            p.nodes.Add(n);
        }
        if(ProjectExportKind!="prefab"){p.environment=ExportEnvironment(nodeIds,p);p.hasEnvironment=true;}
        File.WriteAllText(Path.Combine(PackageRoot, "package.json"), JsonUtility.ToJson(p, true));
        if (p.blockers.Count > 0) throw new Exception("포팅 차단 항목: " + string.Join("; ", p.blockers));
    }
    static void WriteObj(EMMesh mesh)
    {
        var s = new StringBuilder(); s.AppendLine("# EngineMigrator: OBJ right-handed centimeters; the Unreal importer converts handedness");
        foreach (var v in mesh.vertices) s.AppendLine(FormattableString.Invariant($"v {v.z * 100} {-v.x * 100} {v.y * 100}"));
        foreach (var uv in mesh.uv) s.AppendLine(FormattableString.Invariant($"vt {uv.x} {uv.y}"));
        foreach (var n in mesh.normals) s.AppendLine(FormattableString.Invariant($"vn {n.z} {-n.x} {n.y}"));
        for (var i = 0; i < mesh.triangles.Length; i += 3)
        {
            var face = mesh.triangles.Skip(i).Take(3).Select(x => (x + 1).ToString(CultureInfo.InvariantCulture)).ToArray();
            var hasUv = mesh.uv.Length == mesh.vertices.Length; var hasNormals = mesh.normals.Length == mesh.vertices.Length;
            s.AppendLine("f " + string.Join(" ", face.Select(x => hasUv ? (hasNormals ? x + "/" + x + "/" + x : x + "/" + x) : (hasNormals ? x + "//" + x : x))));
        }
        File.WriteAllText(Path.Combine(PackageRoot, mesh.file), s.ToString());
    }
    static void Import()
    {
        if(!ProjectImportMode)ImportedUISprites.Clear();
        var p = JsonUtility.FromJson<EMPackage>(File.ReadAllText(Path.Combine(PackageRoot, "package.json")));
        if (p.schema != 1 || p.blockers.Count > 0) throw new Exception("Invalid or blocked package");
        ImportTiming(p);
        if(!ProjectImportMode)PhysicsMaterials.Clear();
        foreach (var id in p.textures.Select(t => t.id).Concat(p.materials.Select(m => m.id)).Concat(p.meshes.Select(m => m.id)).Concat(p.nodes.Select(n => n.id)).Append(p.scene))
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, "^[A-Za-z0-9_]+$")) throw new Exception("Invalid package identifier");
        var root = "Assets/EngineMigratorImported_"+Guid.NewGuid().ToString("N").Substring(0,8); if (AssetDatabase.IsValidFolder(root)) throw new Exception("이미 결과 폴더가 있습니다. 덮어쓰지 않습니다.");
        Directory.CreateDirectory(root); AssetDatabase.Refresh();
        var textures = ProjectImportMode?ProjectTextures:new Dictionary<string, Texture2D>(); var mats = ProjectImportMode?ProjectMaterials:new Dictionary<string, Material>(); var meshes = ProjectImportMode?ProjectMeshes:new Dictionary<string, Mesh>();
        foreach (var t in p.textures)
        {
            if(textures.ContainsKey(t.id))continue;
            var path = ResourceAssetPath(root,"Textures",t.id,t.name,".png"); File.Copy(PackageFile(t.file), path); AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(path); imp.sRGBTexture = t.srgb; imp.mipmapEnabled = t.mipmaps; imp.filterMode = (FilterMode)Enum.Parse(typeof(FilterMode), t.filter); imp.wrapModeU = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode),t.wrapU); imp.wrapModeV = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode),t.wrapV); imp.userData="EngineMigratorSourceId="+t.id; imp.SaveAndReimport(); textures[t.id] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        foreach (var m in p.materials)
        {
            if(mats.ContainsKey(m.id))continue;
            if (m.shader != "unlit" && m.shader != "standard") throw new Exception("Unsupported material shader");
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var urp=pipeline&&pipeline.GetType().FullName.Contains("Universal");
            if(pipeline&&!urp)throw new Exception("대상 렌더 파이프라인 미지원: "+pipeline.GetType().FullName);
            var shaderName=urp?(m.shader=="standard"?"Universal Render Pipeline/Lit":"Universal Render Pipeline/Unlit"):(m.shader=="standard"?"Standard":(string.IsNullOrEmpty(m.texture)?"Unlit/Color":"Unlit/Texture"));
            var shader=Shader.Find(shaderName);if(!shader)throw new Exception("대상 셰이더 없음: "+shaderName);var material = new Material(shader);
            if (!string.IsNullOrEmpty(m.texture)) material.mainTexture = textures[m.texture];
            if (material.HasProperty("_Color")) material.color = m.color;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",m.color);
            if (m.shader == "standard") { material.SetFloat("_Metallic",m.metallic); material.SetFloat(urp?"_Smoothness":"_Glossiness",m.smoothness); }
            AssetDatabase.CreateAsset(material, ResourceAssetPath(root,"Materials",m.id,m.name,".mat")); mats[m.id] = material;
        }
        foreach (var m in p.meshes)
        {
            if(meshes.ContainsKey(m.id))continue;
            var mesh = new Mesh { name = m.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = m.vertices, uv = m.uv, triangles = m.triangles };
            if (m.normals != null && m.normals.Length == m.vertices.Length) mesh.normals = m.normals; else mesh.RecalculateNormals();
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, ResourceAssetPath(root,"Meshes",m.id,m.name,".asset")); meshes[m.id] = mesh;
        }
        PendingComponentFields.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); var nodes = new Dictionary<string, GameObject>();
        foreach (var n in p.nodes)
        {
            var go = n.hasUI?new GameObject(n.name,typeof(RectTransform)):new GameObject(n.name); nodes[n.id] = go; go.transform.position = n.position; go.transform.rotation = n.rotation; go.transform.localScale = n.scale;
            if (!string.IsNullOrEmpty(n.mesh)) go.AddComponent<MeshFilter>().sharedMesh = meshes[n.mesh];
            if (n.materials != null && n.materials.Length > 0) go.AddComponent<MeshRenderer>().sharedMaterials = n.materials.Select(id => mats[id]).ToArray();
            var renderer=go.GetComponent<MeshRenderer>();if(renderer){renderer.enabled=n.rendererEnabled;renderer.shadowCastingMode=n.castShadows?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;}
            ImportSceneComponents(go,n,p,root);
            ImportPhysics(go,n,root);
            go.SetActive(n.active);
        }
        foreach (var n in p.nodes) if (!string.IsNullOrEmpty(n.parent)) nodes[n.id].transform.SetParent(nodes[n.parent].transform, true);
        foreach(var n in p.nodes)ImportUI(nodes[n.id],n,textures,root);
        Physics.gravity=p.gravity;
        foreach(var n in p.nodes)ImportBehaviours(nodes[n.id],n,nodes);
        foreach(var n in p.nodes)ImportUICallbacks(nodes[n.id],n,nodes,p);
        foreach(var n in p.nodes)ImportAnimation(nodes[n.id],n,root);
        if(p.hasEnvironment)ImportEnvironment(p.environment,nodes,root);
        foreach(var assign in PendingComponentFields)assign();PendingComponentFields.Clear();
        if(ProjectImportMode)RestorePrefabLinks(nodes,p);
        var scenePath = root + "/" + p.scene + ".unity"; EditorSceneManager.SaveScene(scene, scenePath); AssetDatabase.SaveAssets();
        var globalIds = nodes.ToDictionary(pair => pair.Key, pair => GlobalObjectId.GetGlobalObjectIdSlow(pair.Value).ToString());
        SaveNodeIds(root,globalIds);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var reloaded = new Dictionary<string, GameObject>();
        foreach (var pair in globalIds)
        {
            if (!GlobalObjectId.TryParse(pair.Value, out GlobalObjectId gid)) throw new Exception("Invalid saved scene ID");
            reloaded[pair.Key] = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        }
        if(ProjectImportMode)VerifyPrefabLinks(reloaded,p);
        var result = new EMImportResult { targetVersion = Application.unityVersion, savedScene = scenePath,warnings=p.warnings };
        if(p.hasEnvironment)VerifyEnvironment(p.environment,reloaded);
        foreach(var group in p.nodes.Where(n=>n.hasVolume).GroupBy(n=>n.volume.profileId))
        {
            var profiles=group.Select(n=>(ScriptableObject)Get(FindComponent(reloaded[n.id],VolumeType),"sharedProfile")).ToArray();
            if(profiles.Any(profile=>profile!=profiles[0]))throw new Exception("Shared volume profile reference was duplicated");
            result.bindings.Add(new EMBinding{sourceId=group.Key,target=AssetDatabase.GetAssetPath(profiles[0]),kind="VolumeProfile",references=new string[0]});
        }
        foreach (var entry in textures) result.bindings.Add(new EMBinding { sourceId = entry.Key, target = AssetDatabase.GetAssetPath(entry.Value), kind = "Texture2D", references = new string[0] });
        foreach(var entry in ImportedUISprites)result.bindings.Add(new EMBinding{sourceId=entry.Key,target=AssetDatabase.GetAssetPath(entry.Value),kind="Sprite / rect / pivot / pixelsPerUnit",references=new[]{PersistedResourceId(entry.Value.texture)}});
        foreach (var entry in mats) result.bindings.Add(new EMBinding { sourceId = entry.Key, target = AssetDatabase.GetAssetPath(entry.Value), kind = "Material", references = p.materials.Where(m => m.id == entry.Key && !string.IsNullOrEmpty(m.texture)).Select(m => m.texture).ToArray() });
        foreach (var entry in meshes) result.bindings.Add(new EMBinding { sourceId = entry.Key, target = AssetDatabase.GetAssetPath(entry.Value), kind = "Mesh", references = new string[0] });
        foreach (var n in p.nodes)
        {
            var go = reloaded[n.id]; if (!go) throw new Exception("Node missing after reload " + n.id);
            VerifyBehaviours(go,n,reloaded);
            foreach(var behaviour in n.behaviours){result.behaviourInstancesVerified++;result.bindings.Add(new EMBinding{sourceId=behaviour.id,target=globalIds[n.id]+":"+behaviour.className,kind=behaviour.className,references=new[]{n.id}.Concat(behaviour.fields.Where(f=>(f.kind=="node"||f.kind=="prefab"||f.kind=="transform"||f.kind=="component")&&!string.IsNullOrEmpty(f.value)).Select(f=>f.value)).Distinct().ToArray()});}
            if (!n.hasUI&&(go.transform.position - n.position).sqrMagnitude > 0.00001f) throw new Exception("Position mismatch " + n.id);
            if (!n.hasUI&&(Quaternion.Angle(go.transform.rotation, n.rotation) > 0.01f || (go.transform.lossyScale - n.scale).sqrMagnitude > 0.00001f)) throw new Exception("Rotation/scale mismatch " + n.id);
            if (!string.IsNullOrEmpty(n.parent) && go.transform.parent != reloaded[n.parent].transform) throw new Exception("Parent reference mismatch");
            VerifySceneComponents(go,n,p);
            VerifyPhysics(go,n);
            VerifyUI(go,n,reloaded.ToDictionary(e=>e.Value.transform,e=>e.Key));
            VerifyAnimation(go,n,reloaded.ToDictionary(e=>e.Value.transform,e=>e.Key));
            if(n.hasPhysics){var collider=go.GetComponent<Collider>();result.bindings.Add(new EMBinding{sourceId=n.id+".physics",target=GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString(),kind=collider.GetType().Name,references=new[]{n.id}});if(n.physics.customMaterial)result.bindings.Add(new EMBinding{sourceId=n.physics.materialId,target=AssetDatabase.GetAssetPath(collider.sharedMaterial),kind="PhysicsMaterial",references=new[]{n.id}});}
            if(n.hasAnimation)foreach(var clip in n.animation.clips)result.bindings.Add(new EMBinding{sourceId=clip.id,target=AssetDatabase.GetAssetPath(go.GetComponent<Animation>().GetClip(clip.name)),kind="AnimationClip",references=new[]{n.id}.Concat(clip.curves.Select(c=>c.target)).Distinct().ToArray()});
            if(n.hasUI){result.bindings.Add(new EMBinding{sourceId=n.id+".ui",target=GlobalObjectId.GetGlobalObjectIdSlow(go.transform).ToString(),kind="RectTransform / "+n.ui.visual,references=new[]{n.id}});for(int callback=0;callback<n.ui.onClick.Count;callback++){var call=n.ui.onClick[callback];result.bindings.Add(new EMBinding{sourceId=n.id+".onClick."+callback,target=globalIds[n.id]+" → "+call.className+"::"+call.method,kind="Button callback / "+call.callState,references=new[]{call.target,call.behaviour}});}}
            if (!string.IsNullOrEmpty(n.mesh) && !go.GetComponent<MeshFilter>().sharedMesh) throw new Exception("Mesh reference missing");
            var savedRenderer=go.GetComponent<MeshRenderer>();if(savedRenderer&&(savedRenderer.enabled!=n.rendererEnabled||(savedRenderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off)!=n.castShadows))throw new Exception("Renderer visibility/shadow state mismatch");
            for (var i = 0; i < n.materials.Length; i++)
            {
                var expected = p.materials.Single(m => m.id == n.materials[i]); var actual = go.GetComponent<MeshRenderer>().sharedMaterials[i];
                if (actual != mats[expected.id]) throw new Exception("Material chain missing");
                if (!string.IsNullOrEmpty(expected.texture) && actual.mainTexture != textures[expected.texture]) throw new Exception("Texture chain missing");
                if (expected.shader == "standard" && (Mathf.Abs(actual.GetFloat("_Metallic")-expected.metallic)>0.00001f || Mathf.Abs(actual.GetFloat(actual.HasProperty("_Smoothness")?"_Smoothness":"_Glossiness")-expected.smoothness)>0.00001f || Mathf.Abs(actual.color.r-expected.color.r)>0.00001f || Mathf.Abs(actual.color.g-expected.color.g)>0.00001f || Mathf.Abs(actual.color.b-expected.color.b)>0.00001f || Mathf.Abs(actual.color.a-expected.color.a)>0.00001f)) throw new Exception("PBR parameter mismatch");
            }
            result.bindings.Add(new EMBinding { sourceId = n.id, target = globalIds[n.id], kind = "GameObject", references = new[] { n.parent, n.mesh,n.hasVolume?n.volume.profileId:null }.Concat(n.materials).Where(id => !string.IsNullOrEmpty(id)).ToArray() });
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        File.WriteAllText(Path.Combine(PackageRoot, "unity-import-result.json"), JsonUtility.ToJson(result, true));
    }
}


