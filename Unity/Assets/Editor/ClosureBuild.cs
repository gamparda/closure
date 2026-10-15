#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Closure.Editor {
 // CI에서 -executeMethod Closure.Editor.ClosureBuild.BuildWindowsCI 로 호출한다.
 //   -closureVersion x.y.z   플레이어 버전(Application.version). 자동 업데이트가 이 값을 릴리스 태그와 비교한다.
 //   -customBuildPath 경로    exe 경로(.exe 생략 가능). 없으면 Builds/Windows/Closure.exe
 public static class ClosureBuild {
  public static void BuildWindowsCI(){
   var args=Environment.GetCommandLineArgs();
   string version=Arg(args,"-closureVersion");
   if(!string.IsNullOrEmpty(version)){
    if(!Regex.IsMatch(version,@"^\d+\.\d+\.\d+$")){Debug.LogError("잘못된 버전 형식: "+version);EditorApplication.Exit(2);return;}
    PlayerSettings.bundleVersion=version;
   }
   string exe=Arg(args,"-customBuildPath")??"Builds/Windows/Closure";
   if(!exe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))exe+=".exe";
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exe)));
   ClosureProjectSetup.Configure();
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName=exe,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
   if(report.summary.result!=BuildResult.Succeeded){Debug.LogError("빌드 실패: "+report.summary.result+" / 오류 "+report.summary.totalErrors+"건");EditorApplication.Exit(1);return;}
   Debug.Log("빌드 성공: "+exe+" / 버전 "+PlayerSettings.bundleVersion);
   EditorApplication.Exit(0);
  }
  static string Arg(string[] args,string name){for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];return null;}
 }
}
#endif
