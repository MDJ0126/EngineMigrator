"""Prepare a real Unity check of the one-component-per-owner identity constraint."""
from pathlib import Path
import json
root=Path(__file__).resolve().parent.parent;home=root/'artifacts/ui-equality-check';unity=root/'artifacts/runtime-ui-api-tests/UnitySource';output=home/'ui-cardinality.json'
editor=unity/'Assets/EngineMigratorUIEqualityProbe/Editor/EMUICardinalityProbe.cs'
editor.write_text('''using System;using System.IO;using UnityEngine;using UnityEditor;
public static class EMUICardinalityProbe{public static void Run(){GameObject owner=null;try{owner=new GameObject("UICardinality");owner.AddComponent<Canvas>();try{owner.AddComponent<Canvas>();}catch(Exception ex){Debug.Log(ex.GetType().Name);}owner.AddComponent<UnityEngine.UI.Button>();try{owner.AddComponent<UnityEngine.UI.Button>();}catch(Exception ex){Debug.Log(ex.GetType().Name);}int canvases=owner.GetComponents<Canvas>().Length;int buttons=owner.GetComponents<UnityEngine.UI.Button>().Length;File.WriteAllText(OUTPUT,"{\\"status\\":\\"CAPTURED\\",\\"canvases\\":"+canvases+",\\"buttons\\":"+buttons+"}");UnityEngine.Object.DestroyImmediate(owner);owner=null;EditorApplication.Exit(0);}catch(Exception ex){Debug.LogException(ex);if(owner)UnityEngine.Object.DestroyImmediate(owner);EditorApplication.Exit(1);}}}
'''.replace('OUTPUT',json.dumps(str(output))))
runner=home/'cardinality-runner';runner.mkdir(exist_ok=True);(runner/'Runner.csproj').write_bytes((home/'runner/Runner.csproj').read_bytes())
(runner/'Program.cs').write_text('''using EngineMigrator.Core;
var unity=UNITY;var output=OUTPUT;var log=LOG;if(args.Length!=0||File.Exists(output))throw new IOException("Fresh fixed-scope capture required");var settings=Path.Combine(unity,"ProjectSettings/EditorSettings.asset");var before=File.ReadAllBytes(settings);try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(180));await ResourcePorting.RunEditor(EngineLaunchSafety.Unity,["-batchmode","-nographics","-projectPath",unity,"-executeMethod","EMUICardinalityProbe.Run","-logFile",log],Directory.GetParent(unity)!.FullName,timeout.Token);if(!File.Exists(output))throw new IOException("Actual component cardinality capture missing");}finally{File.WriteAllBytes(settings,before);}
'''.replace('UNITY',json.dumps(str(unity))).replace('OUTPUT',json.dumps(str(output))).replace('LOG',json.dumps(str(home/'ui-cardinality.log'))))
print('PRIVATE_UI_CARDINALITY_PROBE_PREPARED_NO_ENGINE_STARTED')
