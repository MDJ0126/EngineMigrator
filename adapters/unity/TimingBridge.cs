// Editor adapter. Install into a working copy's Assets/Editor, not the original project.
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
[Serializable] public class EMTiming {public float timeScale=1,fixedDeltaTime=.02f,maximumDeltaTime=.33333334f;public string unitySettings;}
public partial class EMPackage {public bool hasTiming;public EMTiming timing;}
public static partial class EngineMigratorBridge
{
    static void ExportTiming(EMPackage package){package.hasTiming=true;package.timing=new EMTiming{timeScale=Time.timeScale,fixedDeltaTime=Time.fixedDeltaTime,maximumDeltaTime=Time.maximumDeltaTime,unitySettings=File.ReadAllText("ProjectSettings/TimeManager.asset")};}
    static void ImportTiming(EMPackage package)
    {
        if(!package.hasTiming)return;var timing=package.timing;if(timing==null||string.IsNullOrEmpty(timing.unitySettings))throw new Exception("프로젝트 시간 설정 원본이 없습니다.");if(float.IsNaN(timing.timeScale)||float.IsInfinity(timing.timeScale)||timing.timeScale<0||float.IsNaN(timing.fixedDeltaTime)||float.IsInfinity(timing.fixedDeltaTime)||timing.fixedDeltaTime<=0||Math.Round((double)timing.fixedDeltaTime*141120000)<1||float.IsNaN(timing.maximumDeltaTime)||float.IsInfinity(timing.maximumDeltaTime)||timing.maximumDeltaTime<=0)throw new Exception("유효하지 않은 프로젝트 시간 설정입니다.");
        Time.timeScale=timing.timeScale;Time.fixedDeltaTime=timing.fixedDeltaTime;Time.maximumDeltaTime=timing.maximumDeltaTime;
        var text=timing.unitySettings;foreach(var entry in new[]{new{name="Fixed Timestep",value=timing.fixedDeltaTime},new{name="Maximum Allowed Timestep",value=timing.maximumDeltaTime},new{name="m_TimeScale",value=timing.timeScale}})text=Regex.Replace(text,"(?m)^(  "+Regex.Escape(entry.name)+": ).*$",m=>m.Groups[1].Value+entry.value.ToString("R",CultureInfo.InvariantCulture));
        File.WriteAllText("ProjectSettings/TimeManager.asset",text);
    }
}
