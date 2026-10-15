using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Closure {
 [Serializable] public class Option { public string id, label; }
 [Serializable] public class Zone {
  public int id; public string name, floor, subtitle, rule, point, u, answer, unit, question, note;
  public float plan, p, k, length; public Option[] targets, choices; public string[] hint;
 }
 [Serializable] public class Ending { public string id, name, headline, text, after; }
 [Serializable] public class SurveyData { public Zone[] zones; public string[] route; public Ending[] endings; }
 [Serializable] public class Measurement { public int id, zone, time; public string target, unit, reference; public float value; public bool suspect; }
 [Serializable] public class Judgment { public int zone, help; public string type, status; public float value, error; }
 [Serializable] public class Located { public string id; public int zone; }
 [Serializable] public class SurveyState {
  public int version=1, zone=0, minutes=520, battery=7, points=4, maxZone=0;
  public float drift=0; public bool installed=false, closed=false, deadline=false;
  public string reference="", ending="", signature="";
  public List<Measurement> records=new List<Measurement>(); public List<Judgment> judgments=new List<Judgment>();
  public List<Located> located=new List<Located>(); public List<int> notes=new List<int>(); public List<string> route=new List<string>();
  public int[] help=new int[9]; public bool[] knownK=new bool[9], solved=new bool[9];
  public float x=0,z=2.2f,yaw=180,pitch=0;
 }
 public class SurveyEngine {
  public SurveyData Data { get; private set; } public SurveyState S { get; private set; }
  public Zone Current => S.zone>0 ? Data.zones[S.zone-1] : null;
  public SurveyEngine(SurveyData data, SurveyState state=null) { Data=data; S=state??new SurveyState(); }
  public void Spend(int minutes,float drift=0) { S.minutes=Math.Min(1740,S.minutes+minutes);S.drift=Mathf.Clamp(S.drift+drift,0,100);if(S.minutes>=1740)S.deadline=true; }
  public static string Clock(int minutes) => $"{minutes/60%24:00}:{minutes%60:00}";
  public string Install() {
   if(S.installed){S.installed=false;S.points++;S.reference=S.knownK[S.zone]?"K":"";Spend(5,-3);return "P 기준점을 회수했습니다.";}
   if(S.points<=0)return "남은 기준점이 없습니다.";
   S.installed=true;S.points--;S.reference="P";Spend(8,5);return "신규 P 기준점을 설치했습니다.";
  }
  public string Adopt() { S.reference="K";S.knownK[S.zone]=true;Spend(2);return Current.point+" · 1991 기준 채택"; }
  public string Toggle() { if(S.reference=="K"&&S.installed){S.reference="P";return "신규 P 기준";}if(S.knownK[S.zone]){S.reference="K";return "기존 K 기준";}return "복도 끝의 기존 기준점 가까이에서 E로 확인하세요."; }
  public string Measure(string target,out Measurement result) {
   result=null;if(S.deadline)return "마감 시각입니다. 성과도에 서명하세요.";
   if(S.battery<=0)return "입구의 녹색 충전함에서 충전하세요.";
   if(S.reference!="K"&&!S.installed)return "P로 신규 기준점을 먼저 설치하세요.";
   float value=Current==null?12.41f:S.reference=="K"?Current.k:Current.p;string unit="m";
   if(target=="standard")value=5;
   if(S.zone==1&&target.StartsWith("s")&&target!="standard"){int n=int.Parse(target.Substring(1));value=3.075f*n+(S.reference=="P"&&n>=6?16.6f:0);}
   switch(target){case "up":value=6.4f;break;case "down":value=-9.8f;break;case "angleA":value=38;unit="°";break;case "angleB":value=62;unit="°";break;case "old":value=8.4f;break;case "new":value=13.1f;break;case "lights":value=S.drift>=30?9:8;unit="개";break;case "loopA":value=S.reference=="K"?12.4f:14.6f;break;case "loopB":value=S.reference=="K"?17.2f:15;break;case "origin":value=0;break;}
   string reference=S.reference=="K"?Current.point:"P-"+(S.zone==0?"G":S.zone.ToString());
   bool repeat=S.records.Any(r=>r.zone==S.zone&&r.target==target&&r.reference==reference);
   if(S.drift>=60&&S.reference=="P"&&repeat&&target=="full"&&S.zone>1)value+=.04f;
   S.battery--;Spend(repeat?2:3,S.reference=="K"?0:repeat?1:2);
   result=new Measurement{id=S.records.Count+1,zone=S.zone,target=target,reference=reference,value=(float)Math.Round(value,3),unit=unit,time=S.minutes};S.records.Add(result);return null;
  }
  public bool Evidence() {
   var r=S.records.Where(m=>m.zone==S.zone).ToList();Func<string,bool> has=t=>r.Any(m=>m.target==t);
   switch(S.zone){case 1:return has("s5")&&has("s6")&&r.Any(m=>m.reference.StartsWith("K"))&&r.Any(m=>m.reference.StartsWith("P"));case 2:return has("up")&&has("down");case 3:return has("angleA")&&has("angleB");case 4:return has("old")&&has("new");case 5:return r.Any(m=>m.target=="lights"&&m.value==9);case 6:return has("loopA")&&has("loopB");case 7:return S.notes.Contains(7);default:return false;}
  }
  public string Locate(string answer) {
   if(!Evidence())return "판정 근거가 부족합니다. 대상 구간을 비교 측정하세요.";
   Spend(4);if(answer!=Current.answer){Spend(0,3);return "표시한 위치와 원본 기록이 맞지 않습니다.";}
   S.solved[S.zone]=true;if(!string.IsNullOrEmpty(Current.u)&&!S.located.Any(l=>l.id==Current.u))S.located.Add(new Located{id=Current.u,zone=S.zone});
   return string.IsNullOrEmpty(Current.u)?"1991년의 미서명 기록을 확인했습니다.":Current.u+" 위치를 확인했습니다. 기록실의 야장을 조사하세요.";
  }
  public string Judge(string type) {
   var r=S.records.Where(m=>m.zone==S.zone&&m.target=="full").ToList();if(r.Count==0)return "전체 구간을 먼저 측정하세요.";
   if(type!="hold"&&!r.Any(m=>m.reference.StartsWith(type)))return type+" 기준의 전체 구간 기록이 필요합니다.";
   S.judgments.RemoveAll(j=>j.zone==S.zone);S.judgments.Add(new Judgment{zone=S.zone,type=type,status=type=="K"?"확정":"보류",value=type=="K"?Current.k:Current.p,error=type=="K"?0:Mathf.Abs(Current.p-Current.k),help=S.help[S.zone]});Spend(6,type=="P"?8:0);return "구역 판정을 기록했습니다.";
  }
  public string Advance() {
   if(S.zone==8)return "최종 성과도입니다.";
   if(S.zone==0&&!S.records.Any(r=>r.zone==0))return "지상 구간을 먼저 측정하세요.";
   if(S.zone>0&&!S.judgments.Any(j=>j.zone==S.zone))return "야장에 구간 판정을 먼저 남겨 주세요.";
   if(S.installed){S.points++;S.installed=false;Spend(5,-3);}if(S.zone==0)S.minutes=872;else Spend(20);
   S.zone++;S.minutes=Math.Max(S.minutes,new int[]{0,872,990,1080,1170,1260,1350,1470,1650}[S.zone]);S.maxZone=Math.Max(S.maxZone,S.zone);S.reference="";S.x=0;S.z=2.2f;S.yaw=180;S.pitch=0;return null;
  }
  public string Revisit(int zone){if(zone<1||zone>S.maxZone||S.deadline)return "현재 재방문할 수 없습니다.";if(S.installed){S.points++;S.installed=false;Spend(5,-3);}Spend(12);S.zone=zone;S.reference="";S.x=0;S.z=2.2f;S.yaw=180;S.pitch=0;return null;}
  public string Recharge(){if(S.battery==7)return "충전되어 있습니다.";S.battery=7;Spend(25);return "배터리 충전 완료. 25분 경과.";}
  public string Hint(){int n=S.help[S.zone]++;Spend(10);var z=Current??Data.zones[0];return z.hint[Math.Min(n,2)];}
  public string ReadNote(){if(!S.notes.Contains(S.zone))S.notes.Add(S.zone);return Current.note;}
  public string Traverse(string id){if(S.located.Count<6||!S.notes.Contains(7))return "U 공간 여섯 곳과 마지막 야장을 확인해야 합니다.";if(S.deadline)return "마감 시각입니다.";if(S.battery<=0)return "장비를 충전하세요.";if(S.closed)return "이미 폐합했습니다.";if(S.route.Count>=7)S.route.Clear();S.battery--;S.route.Add(id);Spend(3);if(S.route.Count==7){S.closed=S.route.SequenceEqual(Data.route);return S.closed?"폐합 오차 0.000 m":"폐합 오차가 허용치를 초과합니다. 순서를 다시 검토하세요.";}return id+" 기록";}
  public string Sign(string type,string name){if(string.IsNullOrWhiteSpace(name))return "서명자 이름을 입력하세요.";if(type=="D"&&(!S.closed||S.located.Count!=6))return "모든 U 공간을 포함한 폐합이 필요합니다.";if(!Data.endings.Any(e=>e.id==type))return "서명 방식을 선택하세요.";S.signature=name.Trim();S.ending=type;return null;}
 }
}
