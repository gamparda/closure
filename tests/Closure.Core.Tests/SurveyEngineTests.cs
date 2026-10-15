using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Closure;
using Xunit;

namespace Closure.Core.Tests {
 public class SurveyEngineTests {
  static SurveyData LoadData() {
   var options = new JsonSerializerOptions { IncludeFields = true };
   return JsonSerializer.Deserialize<SurveyData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SurveyData.json")), options);
  }
  static SurveyEngine New() { return new SurveyEngine(LoadData()); }

  static Measurement Measure(SurveyEngine e, string target) {
   Measurement m; var error = e.Measure(target, out m);
   Assert.True(error == null, error); return m;
  }
  // 지상 측정을 끝내고 Z1 입구에 선 상태
  static SurveyEngine Ground() {
   var e = New(); e.Install(); Measure(e, "full");
   Assert.Null(e.Advance()); return e;
  }
  // 구역 하나를 규칙대로 풀고 다음 구역으로 이동
  static void SolveZone(SurveyEngine e) {
   int zone = e.S.zone; var z = e.Current;
   e.Recharge(); e.Install(); Measure(e, "full");
   if (zone == 1) { Measure(e, "s4"); Measure(e, "s6"); Measure(e, "s5"); }
   if (zone == 2) { Measure(e, "up"); Measure(e, "down"); }
   if (zone == 3) { Measure(e, "angleA"); Measure(e, "angleB"); }
   if (zone == 4) { Measure(e, "old"); Measure(e, "new"); }
   if (zone == 5) Measure(e, "lights");
   if (zone == 6) { Measure(e, "loopA"); Measure(e, "loopB"); }
   e.Adopt(); Measure(e, "full");
   if (zone == 7) e.ReadNote();
   e.Locate(z.answer); Assert.True(e.S.solved[zone], "구역 " + zone + " 위치 판정");
   e.ReadNote(); e.Judge("K"); Assert.Contains(e.S.judgments, j => j.zone == zone);
   Assert.Null(e.Advance());
  }
  static SurveyEngine ReachFinalZone() { var e = Ground(); while (e.S.zone < 8) SolveZone(e); return e; }

  [Fact] public void 측정은_기준점과_배터리를_요구한다() {
   var e = New(); Measurement m;
   Assert.Contains("설치", e.Measure("full", out m));
   e.Install(); e.S.battery = 0;
   Assert.Contains("충전", e.Measure("full", out m));
  }

  [Fact] public void 지상_완료_후_지하로_진입하고_기준점을_회수한다() {
   var e = Ground();
   Assert.Equal(1, e.S.zone); Assert.Equal(872, e.S.minutes); Assert.Equal(4, e.S.points); Assert.False(e.S.installed);
  }

  [Fact] public void 같은_복도가_P에서는_41_200_K에서는_24_600() {
   var e = Ground(); e.Install();
   var p = Measure(e, "full"); e.Adopt(); var k = Measure(e, "full");
   Assert.Equal(41.2, p.value, 3); Assert.Equal(24.6, k.value, 3); Assert.False(p.suspect);
  }

  [Fact] public void 표준척은_모든_기준에서_5m() {
   var e = Ground(); e.Install(); Assert.Equal(5.0, Measure(e, "standard").value, 3);
   e.Adopt(); Assert.Equal(5.0, Measure(e, "standard").value, 3);
  }

  [Fact] public void 이분_탐색으로_구간_6의_16_6m를_찾는다() {
   var e = Ground(); e.Install(); Measure(e, "full");
   Assert.Equal(12.3, Measure(e, "s4").value, 2); Assert.Equal(35.05, Measure(e, "s6").value, 2); Assert.Equal(15.375, Measure(e, "s5").value, 2);
   e.Adopt(); Measure(e, "full"); e.Locate("6");
   Assert.True(e.S.solved[1]); Assert.Contains(e.S.located, l => l.id == "U-1" && l.zone == 1);
  }

  [Fact] public void 추측만으로_U_공간을_찾을_수_없다() {
   var e = Ground(); e.Install(); Measure(e, "full"); e.Locate("6");
   Assert.False(e.S.solved[1]); Assert.Empty(e.S.located);
  }

  [Fact] public void K_측정은_불안정도를_올리지_않는다() {
   var e = Ground(); e.Adopt(); float d = e.S.drift; Measure(e, "full"); Assert.Equal(d, e.S.drift);
  }

  [Fact] public void 재측정_원본은_변하지_않고_새_기록이_추가된다() {
   var e = Ground(); e.S.zone = 2; e.Install();
   var a = Measure(e, "full"); e.S.drift = 70; var b = Measure(e, "full");
   Assert.Equal(9.8, a.value, 3); Assert.Equal(9.84, b.value, 3); Assert.NotEqual(a.id, b.id);
  }

  [Fact] public void 없는_K_측정으로_확정할_수_없다() {
   var e = Ground(); e.Install(); Measure(e, "full");
   e.Judge("K"); Assert.Empty(e.S.judgments);
   e.Judge("P"); Assert.Single(e.S.judgments); Assert.Equal(16.6, e.S.judgments[0].error, 2);
  }

  [Fact] public void 충전과_힌트는_시간을_쓰고_도움_이력이_남는다() {
   var e = Ground(); int m = e.S.minutes; e.Recharge(); Assert.Equal(m + 25, e.S.minutes);
   m = e.S.minutes; e.Hint(); Assert.Equal(m + 10, e.S.minutes); Assert.Equal(1, e.S.help[1]);
  }

  [Fact] public void 구역_재방문은_기록과_위치를_보존한다() {
   var e = Ground(); SolveZone(e); int n = e.S.records.Count;
   Assert.Null(e.Revisit(1)); Assert.Equal(1, e.S.zone); Assert.Equal(n, e.S.records.Count);
   Assert.Contains(e.S.located, l => l.id == "U-1"); Assert.NotNull(e.Revisit(8));
  }

  [Fact] public void 여덟_구역과_여섯_U_공간을_거쳐_진_엔딩까지_도달한다() {
   var e = ReachFinalZone();
   Assert.Equal(8, e.S.zone); Assert.Equal(6, e.S.located.Count); Assert.Equal(1650, e.S.minutes);
   e.Recharge();
   foreach (var id in e.Data.route) e.Traverse(id);
   Assert.True(e.S.closed);
   Assert.Null(e.Sign("D", "시험")); Assert.Equal("D", e.S.ending); Assert.True(e.S.minutes < 1740);
  }

  [Fact] public void 순서가_틀리면_폐합하지_않는다() {
   var e = ReachFinalZone(); e.Recharge();
   foreach (var id in e.Data.route.Reverse()) e.Traverse(id);
   Assert.False(e.S.closed); Assert.NotNull(e.Sign("D", "시험"));
  }

  [Fact] public void U_위치가_없으면_진_엔딩_서명이_불가능하다() {
   var e = New(); e.S.zone = 8; e.S.closed = true; Assert.NotNull(e.Sign("D", "시험"));
  }

  [Fact] public void A_C_E는_어떤_상태에서도_서명할_수_있다() {
   foreach (var id in new[] { "A", "C", "E" }) {
    var e = New(); Assert.Null(e.SignBlocked(id)); Assert.Null(e.Sign(id, "시험")); Assert.Equal(id, e.S.ending);
   }
  }

  [Fact] public void B_전체_서명은_모든_구역을_판정해야_열린다() {
   var fresh = New(); Assert.NotNull(fresh.SignBlocked("B"));
   var e = ReachFinalZone(); var summary = e.Summary();
   Assert.Equal(0, summary.unjudged); Assert.Equal(7, summary.confirmed);
   Assert.Null(e.Sign("B", "시험")); Assert.Equal("B", e.S.ending);
  }

  [Fact] public void 불안정도_80_이상이면_전체_서명이_막힌다() {
   var e = ReachFinalZone(); e.S.drift = 80;
   Assert.NotNull(e.SignBlocked("B")); Assert.NotNull(e.Sign("B", "시험")); Assert.Null(e.Sign("A", "시험"));
  }

  [Fact] public void 서명_요약은_확정_보류_미판정_도움을_센다() {
   var e = New();
   e.S.judgments.Add(new Judgment { zone = 1, status = "확정" }); e.S.judgments.Add(new Judgment { zone = 2, status = "보류" }); e.S.help[4] = 2;
   var m = e.Summary(); Assert.Equal(1, m.confirmed); Assert.Equal(1, m.held); Assert.Equal(5, m.unjudged); Assert.Equal(1, m.helped);
  }

  [Fact] public void 마감_이후에도_서명_선택이_남는다() {
   var e = Ground(); e.S.minutes = 1739; e.Spend(3);
   Assert.True(e.S.deadline); Assert.Equal(1740, e.S.minutes);
   Measurement m; Assert.NotNull(e.Measure("full", out m)); Assert.Null(e.Sign("A", "시험"));
  }

  [Fact] public void 빈_서명은_거부된다() { Assert.NotNull(New().Sign("A", " ")); }
 }
}
