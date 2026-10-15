#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Closure.Editor {
 [InitializeOnLoad] public static class ClosureProjectSetup {
  static ClosureProjectSetup(){EditorApplication.delayCall+=Configure;}
  [MenuItem("CLOSURE/프로젝트 설정 및 메인 씬 열기")]
  public static void Configure(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)return;
   Directory.CreateDirectory("Assets/Settings");Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Resources");Directory.CreateDirectory("Assets/Art/Materials");AssetDatabase.Refresh();
   const string pipelinePath="Assets/Settings/ClosureURP.asset";var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
   if(!pipeline){var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Settings/ClosureRenderer.asset");pipeline=ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();AssetDatabase.CreateAsset(pipeline,pipelinePath);var serialized=new SerializedObject(pipeline);var renderers=serialized.FindProperty("m_RendererDataList");renderers.arraySize=1;renderers.GetArrayElementAtIndex(0).objectReferenceValue=renderer;serialized.FindProperty("m_DefaultRendererIndex").intValue=0;serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;serialized.ApplyModifiedPropertiesWithoutUndo();pipeline.msaaSampleCount=1;pipeline.renderScale=1;pipeline.supportsHDR=false;EditorUtility.SetDirty(pipeline);}
   GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;PlayerSettings.companyName="Closure Prototype";PlayerSettings.productName="폐합 CLOSURE";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.runInBackground=true;
   var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");if(settings.Length>0){var serialized=new SerializedObject(settings[0]);var input=serialized.FindProperty("activeInputHandler");if(input!=null&&input.intValue!=1){input.intValue=1;serialized.ApplyModifiedPropertiesWithoutUndo();}}
   var database=AssetDatabase.LoadAssetAtPath<SurveyDatabase>("Assets/Resources/SurveyDatabase.asset");if(!database){database=ScriptableObject.CreateInstance<SurveyDatabase>();AssetDatabase.CreateAsset(database,"Assets/Resources/SurveyDatabase.asset");}database.survey=JsonUtility.FromJson<SurveyData>(File.ReadAllText("Assets/Resources/SurveyData.json"));EditorUtility.SetDirty(database);
   var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Observatory.fbx");if(model&&!File.Exists("Assets/Resources/Observatory.prefab")){var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);ConvertMaterials(instance);PrefabUtility.SaveAsPrefabAsset(instance,"Assets/Resources/Observatory.prefab");Object.DestroyImmediate(instance);}
   if(!File.Exists("Assets/Scenes/Main.unity")){var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("CLOSURE Runtime").AddComponent<ClosureRuntime>();EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};}
   AssetDatabase.SaveAssets();
  }
  static void ConvertMaterials(GameObject instance){foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true)){var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++){var source=materials[i];if(!source)continue;string path="Assets/Art/Materials/"+source.name.Replace(' ','_')+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.gray;material.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(material,path);}materials[i]=material;}renderer.sharedMaterials=materials;}}
  [MenuItem("CLOSURE/Windows 실행 파일 빌드")]
  public static void BuildWindows(){Configure();Directory.CreateDirectory("Builds/Windows");BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/Main.unity"},"Builds/Windows/Closure.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);}
 }
}
#endif
