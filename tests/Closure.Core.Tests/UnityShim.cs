// SurveyEngine.cs가 쓰는 UnityEngine.Mathf의 최소 대체 구현 (테스트 전용)
namespace UnityEngine {
 public static class Mathf {
  public static float Clamp(float value, float min, float max) { return System.Math.Max(min, System.Math.Min(max, value)); }
  public static float Abs(float value) { return System.Math.Abs(value); }
 }
}
