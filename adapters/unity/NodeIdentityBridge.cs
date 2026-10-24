// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
[Serializable] public class EMNodeIdentity {public string globalId,sourceId;}
[Serializable] public class EMNodeIdentities {public List<EMNodeIdentity> entries=new List<EMNodeIdentity>();}
public static partial class EngineMigratorBridge
{
    static Dictionary<Transform,string> SceneNodeIds(UnityEngine.SceneManagement.Scene scene,Transform[] transforms)
    {
        var path=string.IsNullOrEmpty(scene.path)?null:Path.Combine(Path.GetDirectoryName(scene.path),"EngineMigratorNodeIds.json");var restored=path!=null&&File.Exists(path)?JsonUtility.FromJson<EMNodeIdentities>(File.ReadAllText(path)):null;
        var result=new Dictionary<Transform,string>();var used=new HashSet<string>();
        foreach(var transform in transforms)
        {
            var globalId=GlobalObjectId.GetGlobalObjectIdSlow(transform.gameObject).ToString();var previous=restored?.entries.FirstOrDefault(e=>e.globalId==globalId);var id=previous?.sourceId;
            if(id==null)id=restored==null?"node_"+result.Count:"node_new_"+BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(globalId))).Replace("-","").Substring(0,20);
            if(!Regex.IsMatch(id,"^[A-Za-z0-9_]+$")||!used.Add(id))throw new Exception("Invalid/duplicate persisted scene identity");result.Add(transform,id);
        }
        return result;
    }
    static void SaveNodeIds(string root,Dictionary<string,string> ids)
    {
        var data=new EMNodeIdentities{entries=ids.Select(e=>new EMNodeIdentity{sourceId=e.Key,globalId=e.Value}).ToList()};File.WriteAllText(root+"/EngineMigratorNodeIds.json",JsonUtility.ToJson(data,true));
    }
}
