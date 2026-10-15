using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Closure {
 // 키보드로만 조작하는 메뉴 한 줄. 마우스는 시점 전용이라 버튼은 쓰지 않는다.
 public sealed class MenuRow {
  public string Label = "", Value = "";
  public int Style;               // 0 선택 항목, 1 소제목(작고 흐림), 2 정보 줄
  public bool Enabled = true;
  public float Hold;              // 0보다 크면 Space를 그만큼 길게 눌러야 실행
  public Action Confirm;
  public bool Selectable => Style == 0;
 }

 // 화면 구성: 최소 HUD(시계·배터리·장비 LCD) + 키보드 메뉴(타이틀·일시정지·야장·서명) + 잠깐 나타났다 사라지는 한 줄 문구.
 public sealed class ClosureUI : MonoBehaviour {
  static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = Vector2.zero, BR = new Vector2(1, 0), BC = new Vector2(.5f, 0), CC = new Vector2(.5f, .5f);
  readonly Color paper = new Color(.82f, .81f, .72f), muted = new Color(.54f, .61f, .47f), light = new Color(.78f, .83f, .68f), shade = new Color(.02f, .045f, .03f);

  ClosureRuntime game; Font font;
  SurveyEngine E => game.Engine; SurveyState S => game.S;
  RectTransform root, hud, overlay, menuRoot, banner;
  Text clock, battery, floorTag, reading, meterRef, hint, toast, radio, keys, holdText;
  float toastUntil, radioUntil, keysUntil;
  bool title; int consumedFrame = -1, openedFrame = -1;

  // 메뉴 상태
  string menuTitle; string[] tabNames, notebookIds; int tabIndex, cursor; bool menuLeft, notebook;
  Func<List<MenuRow>> builder; List<MenuRow> items = new List<MenuRow>();
  bool holding; float holdTime; string holdBase = "", signType = "A";
  Action bannerAccept, bannerLater;

  public bool ModalOpen => overlay != null;
  public bool IsTitle => title;
  public bool ConsumedThisFrame => consumedFrame == Time.frameCount;
  public event Action TitleShown;

  // ───────── 초기화와 공통 도구 ─────────
  public void Init(ClosureRuntime runtime, Texture worldTexture, Font uiFont) {
   game = runtime; font = uiFont;
   var go = new GameObject("Crisp UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
   var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
   var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
   root = (RectTransform)go.transform;
   var bg = Rect("Low-resolution world", root, CC, CC, Vector2.zero, Vector2.zero); Stretch(bg);
   var raw = bg.gameObject.AddComponent<RawImage>(); raw.texture = worldTexture; raw.raycastTarget = false;
   var retro = Shader.Find("Closure/Retro Display"); if (retro) raw.material = new Material(retro);
  }
  RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size) {
   var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
   var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
  }
  static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
  Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color) {
   var image = Rect(name, parent, anchor, pivot, pos, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
  }
  Text Label(Transform parent, string content, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box) {
   var t = Rect("Text", parent, anchor, pivot, pos, box).gameObject.AddComponent<Text>();
   t.font = font; t.text = content; t.fontSize = size; t.color = color; t.alignment = align; t.supportRichText = true;
   t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; return t;
  }
  static void Delete(ref RectTransform rect) { if (rect != null) Destroy(rect.gameObject); rect = null; }

  // ───────── 타이틀 ─────────
  public void Title() {
   Close(); title = true; Delete(ref hud); HideUpdateBanner();
   hud = Rect("Title", root, CC, CC, Vector2.zero, Vector2.zero); Stretch(hud);
   var dim = Panel("Shade", hud, CC, CC, Vector2.zero, Vector2.zero, new Color(shade.r, shade.g, shade.b, .6f)); Stretch(dim.rectTransform);
   Label(hud, "서진산 지질관측소", 20, light, TextAnchor.UpperLeft, TL, TL, new Vector2(80, -55), new Vector2(700, 36));
   Label(hud, "폐 합", 112, paper, TextAnchor.UpperLeft, TL, TL, new Vector2(120, -255), new Vector2(700, 160));
   Label(hud, "C L O S U R E", 18, muted, TextAnchor.UpperLeft, TL, TL, new Vector2(130, -420), new Vector2(600, 30));
   Label(hud, "당신이 서명한 도면이\n현실이 된다.", 27, paper, TextAnchor.UpperLeft, TL, TL, new Vector2(130, -480), new Vector2(600, 90));
   Label(hud, "05:00 제출  ·  06:00 발파", 22, muted, TextAnchor.UpperRight, TR, TR, new Vector2(-80, -55), new Vector2(500, 36));
   Label(hud, "↑ ↓   Space", 14, muted, TextAnchor.LowerLeft, BL, BL, new Vector2(80, 36), new Vector2(400, 24));
   OpenMenu("", null, 0, TitleItems, true);
   TitleShown?.Invoke();
  }
  List<MenuRow> TitleItems() {
   return new List<MenuRow> {
    Item("새 현장", "", () => game.StartSurvey(false)),
    Item("이어서", "", () => game.StartSurvey(true), File.Exists(game.SavePath)),
    Item("종료", "", Application.Quit)
   };
  }

  // ───────── 게임 HUD ─────────
  public void Refresh() {
   Close(); title = false; Delete(ref hud); HideUpdateBanner();
   hud = Rect("HUD", root, CC, CC, Vector2.zero, Vector2.zero); Stretch(hud);
   clock = Label(hud, "", 32, paper, TextAnchor.UpperLeft, TL, TL, new Vector2(36, -28), new Vector2(420, 44));
   battery = Label(hud, "", 18, muted, TextAnchor.UpperLeft, TL, TL, new Vector2(38, -74), new Vector2(420, 28));
   floorTag = Label(hud, "", 14, muted, TextAnchor.UpperLeft, TL, TL, new Vector2(38, -102), new Vector2(420, 24));
   var meter = Panel("Meter", hud, BR, BR, new Vector2(-36, 36), new Vector2(320, 112), new Color(.07f, .1f, .06f, .82f)).rectTransform;
   meterRef = Label(meter, "", 14, muted, TextAnchor.UpperLeft, TL, TL, new Vector2(14, -8), new Vector2(292, 24));
   reading = Label(meter, "", 44, light, TextAnchor.MiddleRight, TL, TL, new Vector2(14, -34), new Vector2(292, 68));
   Panel("Reticle", hud, CC, CC, Vector2.zero, new Vector2(4, 4), new Color(paper.r, paper.g, paper.b, .8f));
   hint = Label(hud, "", 17, muted, TextAnchor.UpperCenter, CC, CC, new Vector2(0, -44), new Vector2(500, 30));
   toast = Label(hud, "", 20, paper, TextAnchor.MiddleCenter, BC, BC, new Vector2(0, 150), new Vector2(900, 36));
   radio = Label(hud, "", 21, light, TextAnchor.MiddleCenter, BC, BC, new Vector2(0, 96), new Vector2(1000, 44));
   keys = Label(hud, "", 13, muted, TextAnchor.LowerLeft, BL, BL, new Vector2(36, 28), new Vector2(900, 24));
   RefreshHud(); if (!string.IsNullOrEmpty(S.ending)) ShowEnding();
  }
  public void RefreshHud() {
   if (title || clock == null) return;
   clock.text = SurveyEngine.Clock(S.minutes) + " <size=16><color=#8a9c78>/ 05:00</color></size>";
   battery.text = new string('▮', S.battery) + new string('▯', 7 - S.battery);
   floorTag.text = E.Current?.floor ?? "지상";
   reading.text = game.Reading;
   string reference = S.reference == "K" ? E.Current.point : S.installed ? "P-" + S.zone : "—";
   meterRef.text = reference + "  ·  " + game.TargetLabel;
   hint.text = game.InteractionHint();
  }
  public void Toast(string message) { if (toast == null) return; toast.text = message; toastUntil = Time.unscaledTime + 2.5f; }
  public void Radio(string message) { if (radio == null) return; radio.text = message; radioUntil = Time.unscaledTime + 4.5f; }
  public void ShowKeys(float seconds) { if (keys == null) return; keys.text = "WASD 이동  ·  휠 대상  ·  클릭 측정  ·  P 기준점  ·  E 조사  ·  TAB 야장"; keysUntil = Time.unscaledTime + seconds; }

  // ───────── 업데이트 안내 (타이틀 전용, 키보드: U / N) ─────────
  public Text UpdateBanner(string message, Action accept, string acceptLabel, Action later) {
   if (hud == null || !title) return null;
   HideUpdateBanner(); bannerAccept = accept; bannerLater = later;
   banner = Panel("Update banner", hud, BL, BL, new Vector2(130, 70), new Vector2(980, 44), new Color(.2f, .29f, .17f, .95f)).rectTransform;
   return Label(banner, message + "   [U] " + acceptLabel + "   [N] 나중에", 16, paper, TextAnchor.MiddleLeft, CC, CC, Vector2.zero, new Vector2(950, 44));
  }
  public void HideUpdateBanner() { Delete(ref banner); bannerAccept = bannerLater = null; }

  // ───────── 입력과 연출 ─────────
  void Update() {
   float now = Time.unscaledTime;
   Fade(toast, toastUntil, now); Fade(radio, radioUntil, now); Fade(keys, keysUntil, now);
   var kb = Keyboard.current; if (kb == null) return;
   if (banner != null && title) {
    if (kb.uKey.wasPressedThisFrame) bannerAccept?.Invoke();
    else if (kb.nKey.wasPressedThisFrame) bannerLater?.Invoke();
   }
   if (overlay == null) return;
   consumedFrame = Time.frameCount;            // 메뉴가 열려 있는 프레임의 입력은 게임이 다시 쓰지 않는다
   if (openedFrame == Time.frameCount) return; // 메뉴를 연 키가 같은 프레임에 메뉴 조작으로 번지지 않게
   if (holding) { UpdateHold(kb); return; }
   if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
   else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
   else if (notebook && (kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame)) ChangeTab(-1);
   else if (notebook && (kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) ChangeTab(1);
   else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Confirm();
  }
  static void Fade(Text t, float until, float now) { if (t == null) return; var c = t.color; c.a = Mathf.Clamp01((until - now) / .5f); t.color = c; }

  // ───────── 메뉴 엔진 ─────────
  MenuRow Item(string label, string value, Action action, bool enabled = true) { return new MenuRow { Label = label, Value = value, Confirm = action, Enabled = enabled }; }
  MenuRow Head(string text) { return new MenuRow { Label = text, Style = 1 }; }
  MenuRow Line(string text, string value = "") { return new MenuRow { Label = text, Value = value, Style = 2 }; }

  void OpenMenu(string heading, string[] names, int tab, Func<List<MenuRow>> build, bool left, bool dim = true, bool isNotebook = false) {
   Close();
   menuTitle = heading; tabNames = names; tabIndex = tab; builder = build; menuLeft = left; notebook = isNotebook; cursor = 0; holding = false; holdTime = 0;
   overlay = Rect("Overlay", root, CC, CC, Vector2.zero, Vector2.zero); Stretch(overlay);
   if (dim && !left) { var d = Panel("Dim", overlay, CC, CC, Vector2.zero, Vector2.zero, new Color(shade.r, shade.g, shade.b, .88f)); Stretch(d.rectTransform); }
   openedFrame = Time.frameCount; Rebuild(true);
  }
  void Rebuild(bool reset = false) {
   items = builder(); if (reset || cursor >= items.Count) cursor = 0;
   if (items.Count > 0 && !items[cursor].Selectable) { int first = items.FindIndex(i => i.Selectable); cursor = first < 0 ? 0 : first; }
   Draw();
  }
  void Draw() {
   Delete(ref menuRoot); holdText = null;
   float w = menuLeft ? 560 : 900;
   menuRoot = Rect("Menu", overlay, menuLeft ? BL : CC, menuLeft ? BL : CC, menuLeft ? new Vector2(130, 100) : Vector2.zero, new Vector2(w, menuLeft ? 300 : 720));
   float y = 0; // 항목은 메뉴 영역의 위쪽(TL)에서 아래로 쌓는다
   if (!string.IsNullOrEmpty(menuTitle)) { Label(menuRoot, menuTitle, 28, paper, TextAnchor.UpperLeft, TL, TL, new Vector2(0, y), new Vector2(w, 40)); y -= 54; }
   if (tabNames != null) {
    var sb = new StringBuilder("◂   ");
    for (int i = 0; i < tabNames.Length; i++) sb.Append(i == tabIndex ? "<color=#d1cfb8>" + tabNames[i] + "</color>" : "<color=#6f7d62>" + tabNames[i] + "</color>").Append(i < tabNames.Length - 1 ? "    " : "");
    sb.Append("   ▸"); Label(menuRoot, sb.ToString(), 17, muted, TextAnchor.UpperLeft, TL, TL, new Vector2(0, y), new Vector2(w, 28)); y -= 46;
   }
   int visible = menuLeft ? 5 : 12, start = Mathf.Clamp(cursor - visible / 2, 0, Mathf.Max(0, items.Count - visible));
   for (int i = start; i < Mathf.Min(items.Count, start + visible); i++) {
    var it = items[i]; bool selected = i == cursor; float height = it.Style == 1 ? 30 : it.Style == 2 ? 32 : 40;
    Color color = it.Style == 1 ? muted : it.Style == 2 ? light : !it.Enabled ? new Color(muted.r, muted.g, muted.b, .55f) : selected ? paper : light;
    int size = it.Style == 1 ? 15 : it.Style == 2 ? 18 : 21;
    string prefix = it.Style == 0 ? (selected ? "▸  " : "    ") : "";
    Label(menuRoot, prefix + it.Label, size, color, TextAnchor.UpperLeft, TL, TL, new Vector2(0, y), new Vector2(w, height));
    var value = Label(menuRoot, it.Value, size - 2, selected ? paper : muted, TextAnchor.UpperRight, TL, TL, new Vector2(0, y), new Vector2(w, height));
    if (selected && it.Hold > 0) { holdText = value; holdBase = it.Value; }
    y -= height;
   }
   if (start + visible < items.Count) Label(menuRoot, "▼", 14, muted, TextAnchor.UpperRight, TL, TL, new Vector2(0, y), new Vector2(w, 24));
  }
  void Move(int direction) {
   int n = items.Count; if (n == 0) return; int next = cursor;
   for (int i = 0; i < n; i++) { next = (next + direction + n) % n; if (items[next].Selectable) { cursor = next; break; } }
   Draw();
  }
  void Confirm() {
   if (cursor < 0 || cursor >= items.Count) return; var item = items[cursor];
   if (!item.Selectable || !item.Enabled || item.Confirm == null) return;
   if (item.Hold > 0) { holding = true; holdTime = 0; return; }
   Run(item);
  }
  void Run(MenuRow item) { var before = builder; item.Confirm(); if (overlay != null && builder == before) Rebuild(); }
  void UpdateHold(Keyboard kb) {
   var item = items[cursor];
   if (!(kb.spaceKey.isPressed || kb.enterKey.isPressed)) { holding = false; holdTime = 0; if (holdText != null) holdText.text = holdBase; return; }
   holdTime += Time.unscaledDeltaTime; float progress = Mathf.Clamp01(holdTime / item.Hold);
   if (holdText != null) holdText.text = new string('▮', Mathf.FloorToInt(progress * 10)) + new string('▯', 10 - Mathf.FloorToInt(progress * 10));
   if (progress >= 1) { holding = false; holdTime = 0; Run(item); }
  }
  void ChangeTab(int direction) {
   if (notebookIds == null || notebookIds.Length == 0) return;
   OpenNotebook(notebookIds[(tabIndex + direction + notebookIds.Length) % notebookIds.Length]);
  }
  public void Close() { holding = false; StopAllCoroutines(); Delete(ref overlay); menuRoot = null; holdText = null; notebook = false; }

  // ───────── 야장 ─────────
  public void OpenNotebook(string nextTab = "field") {
   var ids = S.zone == 8 ? new[] { "field", "archive", "senior", "closure", "sign" } : new[] { "field", "archive", "senior" };
   string id = S.zone == 8 && nextTab == "field" ? "sign" : nextTab; int index = Array.IndexOf(ids, id); if (index < 0) index = 0;
   var names = ids.Select(i => i == "field" ? "현장 야장" : i == "archive" ? "수집 기록" : i == "senior" ? "선배 수첩" : i == "closure" ? "최종 폐합" : "서명").ToArray();
   OpenMenu("", names, index, () => BuildNotebook(ids[index]), false, true, true);
   notebookIds = ids;
  }
  List<MenuRow> BuildNotebook(string id) { switch (id) { case "archive": return ArchiveItems(); case "senior": return SeniorItems(); case "closure": return ClosureItems(); case "sign": return SignItems(); default: return FieldItems(); } }

  List<MenuRow> FieldItems() {
   var list = new List<MenuRow>(); var z = E.Current;
   list.Add(Head(z == null ? "지상 A-1  ·  12.400 m" : z.name + "  ·  도면 " + z.plan.ToString("0.0") + " m"));
   foreach (var record in S.records.Where(r => r.zone == S.zone)) {
    var r = record; string name = z?.targets.FirstOrDefault(t => t.id == r.target)?.label ?? "지상 A-1";
    list.Add(Item(r.id.ToString("00") + "  " + name + "  " + r.reference, r.value.ToString("0.000") + " " + r.unit + "   " + (r.suspect ? "☒" : "☐"), () => { r.suspect = !r.suspect; game.Save(); }));
   }
   if (z != null) {
    list.Add(Head(z.question));
    foreach (var choice in z.choices) { var c = choice; list.Add(Item("위치  " + c.label, "", () => { Toast(E.Locate(c.id)); game.Changed(); })); }
    if (S.solved[S.zone]) list.Add(Line((z.u ?? "기록") + "  확인"));
    var judgment = S.judgments.FirstOrDefault(j => j.zone == S.zone);
    foreach (var type in new[] { "K", "P", "hold" }) {
     string t = type; string label = t == "K" ? "K  " + z.k.ToString("0.000") + " m  확정" : t == "P" ? "P  " + z.p.ToString("0.000") + " m  보류" : "판정 보류";
     list.Add(Item(label, judgment != null && judgment.type == t ? "●" : "", () => { Toast(E.Judge(t)); game.Changed(); }));
    }
    list.Add(Line("폐합 오차", (judgment == null ? "—" : judgment.error.ToString("0.000")) + " / 0.050"));
   }
   if (S.zone < 8) list.Add(Item(S.zone == 0 ? "지하로  →" : "다음 구역  →", "", game.Advance));
   return list;
  }
  List<MenuRow> ArchiveItems() {
   var list = new List<MenuRow>();
   foreach (int id in S.notes) {
    var zone = E.Data.zones[id - 1]; list.Add(Head("Z" + id + "  " + zone.name));
    foreach (var line in zone.note.Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)) list.Add(Line(line));
   }
   if (S.notes.Count == 0) list.Add(Head("기록 없음"));
   list.Add(Head("U   " + string.Join("   ", E.Data.zones.Take(6).Select(z => z.u + (S.located.Any(l => l.id == z.u) ? " ✓" : " —")))));
   for (int id = 1; id <= S.maxZone; id++) { int n = id; list.Add(Item("Z" + id + "  " + E.Data.zones[id - 1].name, "재방문 · 12분", () => game.Revisit(n))); }
   return list;
  }
  List<MenuRow> SeniorItems() {
   var list = new List<MenuRow> { Line("숫자가 이상하면 표준척부터. 5m짜리."), Line("기존 점과 내 점은 보는 기준이 다르다."), Line("어디서 틀어지는지 모르겠으면 반으로 쪼개.") };
   if (S.zone >= 3) list.Add(Line("한 바퀴 돌아 안 맞으면, 그게 오차야."));
   if (S.zone >= 6) list.Add(Line("서명은 내가 한다. 내가 확인한 걸 믿는다."));
   return list;
  }
  List<MenuRow> ClosureItems() {
   var list = new List<MenuRow> { Head("U 여섯 곳  →  K-01") };
   foreach (string id in new[] { "U-1", "U-2", "U-3", "U-4", "U-5", "U-6", "K-01" }) { string node = id; list.Add(Item(node, "측정", () => { Toast(E.Traverse(node)); game.Changed(); })); }
   list.Add(Line(string.Join(" → ", S.route)));
   list.Add(Line(S.closed ? "폐합 오차 0.000 / 0.050" : "배터리 " + S.battery + " / 7"));
   list.Add(Item("순서 초기화", "", () => { S.route.Clear(); S.closed = false; game.Save(); }));
   return list;
  }
  List<MenuRow> SignItems() {
   var list = new List<MenuRow>();
   foreach (var z in E.Data.zones.Take(7)) { var j = S.judgments.FirstOrDefault(v => v.zone == z.id); list.Add(Line("Z" + z.id + "  " + z.name, j == null ? "—" : j.value.ToString("0.000") + "  " + j.status)); }
   var sum = E.Summary();
   list.Add(Head("확정 " + sum.confirmed + "  ·  보류 " + sum.held + "  ·  도움 " + sum.helped + "     U " + S.located.Count + "/6  ·  폐합 " + (S.closed ? "✓" : "—")));
   foreach (string id in new[] { "A", "B", "C", "D" }) {
    string type = id; var ending = E.Data.endings.First(e => e.id == type); string blocked = E.SignBlocked(type);
    list.Add(Item((signType == type ? "●  " : "○  ") + ending.name, blocked ?? "", () => signType = type, blocked == null));
   }
   var sign = Item("서명", "Space 길게", () => { string error = E.Sign(signType, "측량기술자"); if (error != null) Toast(error); else { game.Save(); ShowEnding(); } });
   sign.Hold = 1.6f; list.Add(sign);
   return list;
  }

  // ───────── 일시정지 ─────────
  public void Pause() {
   OpenMenu("작업 중지", null, 0, () => new List<MenuRow> {
    Item("복귀", "", Close),
    Item("성과도 보관", "", Export),
    Item("환경음", "켜기 / 끄기", game.ToggleAudio),
    Item("철수", "", () => { E.Sign("E", "미서명"); game.Save(); ShowEnding(); }, S.notes.Contains(1)),
    Item("타이틀로", "", game.ReturnTitle),
    Head("WASD 이동  ·  휠 대상 선택  ·  클릭 / Space 측정"),
    Head("P 기준점  ·  Q K/P  ·  E 조사  ·  R 무전  ·  F 손전등  ·  TAB 야장")
   }, false);
  }

  // ───────── 결말: 문장을 하나씩 천천히 ─────────
  public void ShowEnding() { Close(); StartCoroutine(EndingRoutine()); }
  IEnumerator EndingRoutine() {
   var ending = E.Data.endings.First(v => v.id == S.ending);
   overlay = Rect("Ending", root, CC, CC, Vector2.zero, Vector2.zero); Stretch(overlay);
   var dim = Panel("Dim", overlay, CC, CC, Vector2.zero, Vector2.zero, new Color(shade.r, shade.g, shade.b, .96f)); Stretch(dim.rectTransform);
   Label(overlay, ending.headline, 36, paper, TextAnchor.MiddleCenter, CC, CC, new Vector2(0, 120), new Vector2(1200, 120));
   var line = Label(overlay, "", 23, light, TextAnchor.MiddleCenter, CC, CC, new Vector2(0, -20), new Vector2(1100, 90));
   yield return new WaitForSecondsRealtime(2.4f);
   foreach (var sentence in Regex.Split(ending.text.Trim(), @"(?<=[.!?])\s+")) { line.text = sentence; yield return new WaitForSecondsRealtime(Mathf.Max(2.2f, sentence.Length * .07f)); }
   line.text = ending.after; line.color = muted; yield return new WaitForSecondsRealtime(1.6f);
   notebook = false; tabNames = null; menuLeft = true; menuTitle = "";
   builder = () => new List<MenuRow> { Item("성과도 보관", "", Export), Item("타이틀로", "", game.ReturnTitle) };
   openedFrame = Time.frameCount; Rebuild(true);
  }

  void Export() {
   var b = new StringBuilder("폐합 / 서진산 지질관측소 측량 성과도\n결말: " + S.ending + "\n\n");
   foreach (var r in S.records) b.AppendLine(SurveyEngine.Clock(r.time) + " / Z" + r.zone + " / " + r.reference + " / " + r.target + " / " + r.value.ToString("0.000") + " " + r.unit + " / " + (r.suspect ? "의심" : ""));
   b.AppendLine("귀환: " + string.Join(" → ", S.route));
   var path = Path.Combine(Application.persistentDataPath, "폐합_현장성과도.txt");
   File.WriteAllText(path, b.ToString(), Encoding.UTF8); Toast("성과도 저장"); Application.OpenURL("file:///" + Application.persistentDataPath.Replace('\\', '/'));
  }
 }
}
