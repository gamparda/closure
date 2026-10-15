using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Closure {
 [Serializable] public class ReleaseAsset { public string name, browser_download_url, digest; public long size; }
 [Serializable] public class ReleaseInfo { public string tag_name, html_url; public bool draft, prerelease; public ReleaseAsset[] assets; }

 // 네트워크 없이 검사할 수 있는 순수 규칙
 public static class UpdateRules {
  public const string Repo = "gamparda/closure";
  public static bool TryParseVersion(string text, out Version version) {
   version = null; if (string.IsNullOrEmpty(text)) return false;
   text = text.Trim(); if (text.StartsWith("v") || text.StartsWith("V")) text = text.Substring(1);
   return Version.TryParse(text, out version);
  }
  public static bool IsNewer(string remoteTag, string localVersion) {
   Version remote, local; return TryParseVersion(remoteTag, out remote) && TryParseVersion(localVersion, out local) && remote > local;
  }
  public static bool IsTrustedUrl(string url) => !string.IsNullOrEmpty(url) && url.StartsWith("https://github.com/" + Repo + "/releases/download/", StringComparison.Ordinal);
  public static ReleaseAsset FindInstaller(ReleaseInfo info) {
   if (info == null || info.assets == null) return null;
   foreach (var asset in info.assets) if (asset != null && asset.name != null && Regex.IsMatch(asset.name, @"^Closure-Setup-\d+\.\d+\.\d+\.exe$") && IsTrustedUrl(asset.browser_download_url)) return asset;
   return null;
  }
  public static ReleaseAsset FindChecksum(ReleaseInfo info, ReleaseAsset installer) {
   if (info == null || info.assets == null || installer == null) return null;
   foreach (var asset in info.assets) if (asset != null && asset.name == installer.name + ".sha256" && IsTrustedUrl(asset.browser_download_url)) return asset;
   return null;
  }
  public static string ParseSha256(string text) {
   if (string.IsNullOrEmpty(text)) return null;
   var match = Regex.Match(text.Trim(), @"^(?:sha256:)?([0-9a-fA-F]{64})(?:\s|$)");
   return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
  }
  public static string Sha256Of(string path) {
   using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
  }
  public static ReleaseInfo ParseRelease(string json) { try { return JsonUtility.FromJson<ReleaseInfo>(json); } catch { return null; } }
 }

 // 타이틀 화면에서 새 릴리스를 확인하고, 사용자가 수락하면 설치 파일을 받아 무결성을 검증한 뒤 실행한다.
 public sealed class ClosureUpdater : MonoBehaviour {
  ClosureUI ui; ReleaseInfo release; ReleaseAsset installer, checksum; Text label;
  bool busy, dismissed; string readyPath;
  public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer;

  public void Init(ClosureUI target) {
   ui = target; ui.TitleShown += ShowBanner;
   if (Supported) StartCoroutine(Check());
  }
  void OnDestroy() { if (ui != null) ui.TitleShown -= ShowBanner; }

  IEnumerator Check() {
   yield return new WaitForSecondsRealtime(1.5f);
   using (var request = UnityWebRequest.Get("https://api.github.com/repos/" + UpdateRules.Repo + "/releases/latest")) {
    request.SetRequestHeader("User-Agent", "Closure/" + Application.version);
    request.SetRequestHeader("Accept", "application/vnd.github+json");
    request.timeout = 10;
    yield return request.SendWebRequest();
    if (request.result != UnityWebRequest.Result.Success) yield break;
    var info = UpdateRules.ParseRelease(request.downloadHandler.text);
    if (info == null || info.draft || info.prerelease || !UpdateRules.IsNewer(info.tag_name, Application.version)) yield break;
    var asset = UpdateRules.FindInstaller(info);
    if (asset == null) yield break;
    release = info; installer = asset; checksum = UpdateRules.FindChecksum(info, asset);
   }
   ShowBanner();
  }

  void ShowBanner() {
   if (installer == null || dismissed || busy || ui == null || !ui.IsTitle) return;
   if (readyPath != null) label = ui.UpdateBanner("업데이트 " + release.tag_name + " 준비 완료. 지금 설치하면 게임이 다시 시작됩니다.", Launch, "지금 설치", Dismiss);
   else label = ui.UpdateBanner("새 버전 " + release.tag_name + " 이(가) 있습니다. 현재 버전 " + Application.version, () => StartCoroutine(Download()), "지금 업데이트", Dismiss);
  }
  void Dismiss() { dismissed = true; if (ui != null) ui.HideUpdateBanner(); }
  void Say(string message) { if (label) label.text = message; }
  void Fail(string message) { busy = false; Say(message); }

  IEnumerator Download() {
   if (busy) yield break; busy = true; Say("무결성 정보를 확인하는 중…");
   string expected = UpdateRules.ParseSha256(installer.digest);
   if (expected == null && checksum != null) {
    using (var request = UnityWebRequest.Get(checksum.browser_download_url)) {
     request.timeout = 15; yield return request.SendWebRequest();
     if (request.result == UnityWebRequest.Result.Success) expected = UpdateRules.ParseSha256(request.downloadHandler.text);
    }
   }
   if (expected == null) { Fail("무결성 정보를 찾지 못해 업데이트를 중단했습니다."); yield break; }

   string path = Path.Combine(Application.temporaryCachePath, installer.name);
   string cleanupError = null;
   try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) { cleanupError = e.Message; }
   if (cleanupError != null) { Fail("임시 파일을 정리하지 못했습니다: " + cleanupError); yield break; }
   using (var request = new UnityWebRequest(installer.browser_download_url, UnityWebRequest.kHttpVerbGET)) {
    request.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
    request.timeout = 600;
    var operation = request.SendWebRequest();
    while (!operation.isDone) { Say("다운로드 중 " + Mathf.RoundToInt(request.downloadProgress * 100) + "%"); yield return null; }
    if (request.result != UnityWebRequest.Result.Success) { Fail("다운로드에 실패했습니다. 나중에 다시 시도해 주세요."); yield break; }
   }
   Say("무결성 검증 중…"); yield return null;
   string actual = null, verifyError = null;
   try { actual = (installer.size > 0 && new FileInfo(path).Length != installer.size) ? "" : UpdateRules.Sha256Of(path); } catch (Exception e) { verifyError = e.Message; }
   if (verifyError != null) { Fail("검증하지 못했습니다: " + verifyError); yield break; }
   if (actual != expected) { try { File.Delete(path); } catch { } Fail("내려받은 파일이 올바르지 않아 폐기했습니다."); yield break; }

   readyPath = path; busy = false;
   if (ui != null && ui.IsTitle) Launch(); else Say("업데이트 준비 완료. 타이틀 화면에서 설치할 수 있습니다.");
  }

  void Launch() {
   if (readyPath == null || ui == null || !ui.IsTitle) return;
   try {
    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = readyPath, Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS", UseShellExecute = true });
    Application.Quit();
   } catch (Exception e) { Fail("설치 프로그램을 실행하지 못했습니다: " + e.Message); }
  }
 }
}
