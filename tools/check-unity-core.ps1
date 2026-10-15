$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskEngineSource = [IO.File]::ReadAllText((Join-Path $taskRoot 'Unity\Assets\Scripts\SurveyEngine.cs'))
$taskMathShim = @'
namespace UnityEngine {
 public static class Mathf {
  public static float Clamp(float v,float min,float max){return System.Math.Max(min,System.Math.Min(max,v));}
  public static float Abs(float v){return System.Math.Abs(v);}
 }
}
'@
Add-Type -TypeDefinition ($taskEngineSource + $taskMathShim)
$taskJsonOptions = [System.Text.Json.JsonSerializerOptions]::new()
$taskJsonOptions.IncludeFields = $true
$taskJson = [IO.File]::ReadAllText((Join-Path $taskRoot 'Unity\Assets\Resources\SurveyData.json'))
$taskData = [System.Text.Json.JsonSerializer]::Deserialize($taskJson,[Closure.SurveyData],$taskJsonOptions)
$taskEngine = [Closure.SurveyEngine]::new($taskData)
$taskMeasurement = [Closure.Measurement]$null
$null = $taskEngine.Install()
$taskError = $taskEngine.Measure('full',[ref]$taskMeasurement)
if($taskError){throw $taskError}
if($taskEngine.Advance()){throw 'Ground transition failed'}
while($taskEngine.S.zone -lt 8){
 $taskZone = $taskEngine.S.zone
 $null=$taskEngine.Recharge()
 $null=$taskEngine.Install()
 $taskTargets=@('full')
 switch($taskZone){1{$taskTargets+=@('s4','s6','s5')}2{$taskTargets+=@('up','down')}3{$taskTargets+=@('angleA','angleB')}4{$taskTargets+=@('old','new')}5{$taskTargets+='lights'}6{$taskTargets+=@('loopA','loopB')}}
 foreach($taskTarget in $taskTargets){$taskError=$taskEngine.Measure($taskTarget,[ref]$taskMeasurement);if($taskError){throw $taskError}}
 $null=$taskEngine.Adopt()
 $taskError=$taskEngine.Measure('full',[ref]$taskMeasurement)
 if($taskError){throw $taskError}
 if($taskZone -eq 7){$null=$taskEngine.ReadNote()}
 $null=$taskEngine.Locate($taskEngine.Current.answer)
 if(!$taskEngine.S.solved[$taskZone]){throw "Unsolved zone $taskZone"}
 $null=$taskEngine.ReadNote()
 $null=$taskEngine.Judge('K')
 $taskError=$taskEngine.Advance()
 if($taskError){throw $taskError}
}
$null=$taskEngine.Recharge()
foreach($taskNode in $taskData.route){$null=$taskEngine.Traverse($taskNode)}
if(!$taskEngine.S.closed){throw 'Final closure failed'}
$taskError=$taskEngine.Sign('D','시험 측량자')
if($taskError){throw $taskError}
"C# survey core compiled with Mathf shim; all 8 zones -> ending $($taskEngine.S.ending); U spaces $($taskEngine.S.located.Count); minutes $($taskEngine.S.minutes). Unity runtime/editor compilation not tested."
