using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Closure {
 public sealed class ClosureRuntime : MonoBehaviour {
  public SurveyEngine Engine {get;private set;} public SurveyState S=>Engine.S;
  public ClosureUI UI {get;private set;} public Camera View {get;private set;}
  GameObject world,player;CharacterController controller;Light torch;RenderTexture lowResolution;
  float length=41.2f,width=4.4f,gap,yaw,pitch,walkBudget,stepBudget,autosave;bool playing;
  Texture2D concrete,floor;Material wallMat,lowerMat,floorMat,metalMat,worldTextMaterial;Font font;AudioSource ambient,beeper;AudioClip beep,foot;
  public string Target="full",Reading="—.——— m",Message="";public string SavePath=>Path.Combine(Application.persistentDataPath,"closure-survey.json");
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(FindAnyObjectByType<ClosureRuntime>()==null)new GameObject("CLOSURE").AddComponent<ClosureRuntime>();}
  void Awake(){Application.targetFrameRate=60;QualitySettings.vSyncCount=1;
   var database=Resources.Load<SurveyDatabase>("SurveyDatabase");
   Engine=new SurveyEngine(database?database.survey:JsonUtility.FromJson<SurveyData>(Resources.Load<TextAsset>("SurveyData").text));
   font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Arial"},24);
   concrete=Noise(new Color(.44f,.48f,.39f));floor=Noise(new Color(.35f,.38f,.30f));
   wallMat=Mat(new Color(.66f,.69f,.57f),concrete);lowerMat=Mat(new Color(.35f,.44f,.33f),concrete);floorMat=Mat(new Color(.44f,.46f,.36f),floor);metalMat=Mat(new Color(.24f,.30f,.22f));
   player=new GameObject("Surveyor");controller=player.AddComponent<CharacterController>();controller.height=1.7f;controller.center=new Vector3(0,.85f,0);controller.radius=.24f;controller.stepOffset=.25f;
   var cam=new GameObject("World Camera");cam.transform.SetParent(player.transform,false);cam.transform.localPosition=new Vector3(0,1.62f,0);View=cam.AddComponent<Camera>();View.fieldOfView=67;View.nearClipPlane=.05f;View.farClipPlane=90;View.clearFlags=CameraClearFlags.SolidColor;View.backgroundColor=new Color(.04f,.07f,.045f);cam.AddComponent<AudioListener>();
   lowResolution=new RenderTexture(640,360,24){filterMode=FilterMode.Point};View.targetTexture=lowResolution;
   var flash=new GameObject("Flashlight");flash.transform.SetParent(cam.transform,false);torch=flash.AddComponent<Light>();torch.type=LightType.Spot;torch.spotAngle=70;torch.range=22;torch.intensity=9;torch.color=new Color(.85f,.9f,.69f);
   worldTextMaterial=new Material(Shader.Find("Closure/World Text"));worldTextMaterial.mainTexture=font.material.mainTexture;Font.textureRebuilt+=RefreshFont;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.24f,.29f,.21f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.065f,.1f,.065f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.035f;
   var sun=new GameObject("Distant Work Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.3f;sun.transform.rotation=Quaternion.Euler(40,-30,0);sun.color=new Color(.7f,.8f,.59f);
   SetupAudio();UI=gameObject.AddComponent<ClosureUI>();UI.Init(this,lowResolution,font);BuildWorld(1);UI.Title();
  }
  Texture2D Noise(Color basis){var t=new Texture2D(64,64,TextureFormat.RGB24,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};var colors=new Color[4096];var random=new System.Random(51);for(int i=0;i<colors.Length;i++){float a=(float)random.NextDouble()*.16f-.08f;colors[i]=basis+new Color(a,a,a,0);if(i%64==63||i/64==63)colors[i]*=.8f;}t.SetPixels(colors);t.Apply();return t;}
  Material Mat(Color color,Texture texture=null){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;if(texture)m.mainTexture=texture;m.SetFloat("_Smoothness",.08f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.007f,.009f,.005f));return m;}
  GameObject Box(string name,Vector3 p,Vector3 scale,Material material,bool collide=true){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(world.transform,false);o.transform.localPosition=p;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;if(!collide)Destroy(o.GetComponent<Collider>());return o;}
  void Label(string text,Vector3 p,float size=.18f,Quaternion? rotation=null){var o=new GameObject(text);o.transform.SetParent(world.transform,false);o.transform.position=p;o.transform.rotation=(rotation??Quaternion.identity)*Quaternion.Euler(0,180,0);var t=o.AddComponent<TextMesh>();t.text=text;t.font=font;t.fontSize=64;t.characterSize=size/6.4f;t.alignment=TextAlignment.Center;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.75f,.81f,.65f);o.GetComponent<MeshRenderer>().sharedMaterial=worldTextMaterial;}
  public void BuildWorld(int zone){
   if(world)Destroy(world);world=new GameObject("Observatory / Zone "+zone);var z=zone>0?Engine.Data.zones[zone-1]:null;length=z?.length??18;width=zone==7?9:zone==3?6:4.4f;gap=zone==1?-25.2125f:-length*.66f;floorMat.mainTextureScale=new Vector2(width/2,length/2);
   Box("Concrete floor",new Vector3(0,-.12f,-length/2+2),new Vector3(width+.5f,.22f,length+8),floorMat);
   Box("Ceiling",new Vector3(0,3.7f,-length/2+2),new Vector3(width+.5f,.22f,length+8),wallMat);
   for(int side=-1;side<=1;side+=2){
    if(side<0){Box("Upper wall",new Vector3(-width/2,2.5f,-length/2+2),new Vector3(.25f,2.25f,length+5),wallMat);Box("Painted lower wall",new Vector3(-width/2,.675f,-length/2+2),new Vector3(.27f,1.35f,length+5),lowerMat);}
    else{float a=5-gap-1.6f,b=length+gap-1.6f;foreach(var section in new[]{new Vector2(a,(5+gap+1.6f)/2),new Vector2(b,(gap-1.6f-length)/2)}){Box("Upper wall",new Vector3(width/2,2.5f,section.y),new Vector3(.25f,2.25f,section.x),wallMat);Box("Lower wall",new Vector3(width/2,.675f,section.y),new Vector3(.27f,1.35f,section.x),lowerMat);}}
    Box("Floor safety line",new Vector3(side*(width/2-.5f),.003f,-length/2+1),new Vector3(.05f,.006f,length+3),Mat(new Color(.61f,.56f,.29f)),false);
    for(int pipe=0;pipe<3;pipe++)Box("Overhead conduit",new Vector3(side*(width/2-.3f-pipe*.2f),3.35f,-length/2+1),new Vector3(.09f,.09f,length+5),metalMat,false);
   }
   Box("Entrance bulkhead",new Vector3(0,1.8f,5),new Vector3(width+.5f,3.7f,.25f),wallMat);Box("End wall",new Vector3(0,1.8f,-length-.1f),new Vector3(width+.5f,3.7f,.25f),wallMat);Box("Steel door",new Vector3(0,1.35f,-length+.05f),new Vector3(1.6f,2.7f,.1f),Mat(new Color(.31f,.22f,.17f)));
   Label(zone==8?"최종 성과도 / K-01":"다음 구역 →",new Vector3(0,2.95f,-length+.13f));
   for(int i=1;i<=8;i++){float segStart=(i-1)*3.075f+(i>6?16.6f:0),segEnd=i*3.075f+(i>=6?16.6f:0);float p=zone==1?-(segStart+segEnd)/2:-(i-.5f)*length/8;Box("Beam "+i,new Vector3(0,3.4f,p),new Vector3(width+.2f,.18f,.25f),metalMat);var em=Mat(new Color(.73f,.82f,.61f));em.EnableKeyword("_EMISSION");em.SetColor("_EmissionColor",new Color(.85f,.95f,.68f)*2);Box("Fluorescent "+i,new Vector3(0,3.15f,p),new Vector3(1.7f,.045f,.25f),em,false);var light=new GameObject("Fluorescent light "+i);light.transform.SetParent(world.transform);light.transform.position=new Vector3(0,2.9f,p);var l=light.AddComponent<Light>();l.type=LightType.Point;l.intensity=3.6f;l.range=9;l.color=new Color(.76f,.85f,.63f);Label(i.ToString("00"),new Vector3(-width/2+.17f,2.6f,p),.15f,Quaternion.Euler(0,90,0));}
   Box("Archive floor",new Vector3(width/2+2.4f,-.12f,gap),new Vector3(5,.22f,3.4f),floorMat);Box("Archive back",new Vector3(width/2+4.9f,1.8f,gap),new Vector3(.2f,3.6f,3.5f),wallMat);foreach(int side in new[]{-1,1})Box("Archive wall",new Vector3(width/2+2.4f,1.8f,gap+side*1.7f),new Vector3(5,3.6f,.2f),wallMat);Box("Archive roof",new Vector3(width/2+2.4f,3.6f,gap),new Vector3(5,.2f,3.5f),wallMat);Box("Archive table",new Vector3(width/2+2.4f,.9f,gap-.6f),new Vector3(1.6f,.1f,.8f),metalMat);Box("1991 notebook",new Vector3(width/2+2.4f,.97f,gap-.5f),new Vector3(.55f,.025f,.38f),Mat(new Color(.7f,.67f,.49f)),false);Label("1991 / 현장 야장",new Vector3(width/2+2.4f,1.6f,gap-1.55f));
   Label((z?.point??"K-01")+"\n1991 · 기존점",new Vector3(-width/2+.17f,1.55f,-length+2.2f),.16f,Quaternion.Euler(0,90,0));
   Box("Green charging case",new Vector3(-width/2+.25f,.7f,-2.8f),new Vector3(.3f,.8f,.6f),lowerMat);Label("충전함 / AC220V",new Vector3(-width/2+.43f,1.35f,-2.8f),.13f,Quaternion.Euler(0,90,0));Label("5.000 m / 표준척",new Vector3(-width/2+.17f,1.6f,.2f),.15f,Quaternion.Euler(0,90,0));
   if(zone==2){for(int i=0;i<14;i++)Box("Descending step",new Vector3(width/2+2.5f,-.2f-i*.2f,gap-1.4f+i*.28f),new Vector3(3,.2f,.32f),wallMat);Label("02 / 수준표 −3.400",new Vector3(width/2+.2f,2.3f,gap+.7f),.17f);}
   if(zone==3)Label("북동 / 후보 B\nA 38° · B 62°",new Vector3(0,2.5f,-length+.2f));
   if(zone==4){Label("K-07\n2026 · 새 스티커",new Vector3(width/2-.17f,1.8f,-8),.16f,Quaternion.Euler(0,-90,0));Label("K-07\n1991 · 금속 각인",new Vector3(-width/2+.17f,1.8f,-14),.16f,Quaternion.Euler(0,90,0));}
   if(zone==5&&S.drift>=30)Label("09",new Vector3(width/2-.17f,2.5f,gap),.3f,Quaternion.Euler(0,-90,0));
   if(zone==6){Label("LOOP A / +2.200",new Vector3(-width/2+.17f,2,-9),.15f,Quaternion.Euler(0,90,0));Label("LOOP B / −2.200",new Vector3(width/2-.17f,2,-18),.15f,Quaternion.Euler(0,-90,0));}
   if(zone==7){for(int i=0;i<20;i++){var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="Cavity rock";rock.transform.SetParent(world.transform);rock.transform.position=new Vector3((i%2==0?-1:1)*(width/2-.3f),1,-i*length/20);rock.transform.localScale=new Vector3(2.5f,3,2);rock.GetComponent<Renderer>().sharedMaterial=lowerMat;}}
   var props=Resources.Load<GameObject>("Observatory");if(props){foreach(var prop in new[]{new{n="TotalStation",p=new Vector3(-1.2f,0,-1.2f)},new{n="FieldGenerator",p=new Vector3(-width/2+.65f,0,-6)},new{n="ArchiveCabinet",p=new Vector3(width/2+3.8f,0,gap+.8f)}}){var source=props.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==prop.n);if(source){var instance=Instantiate(source.gameObject,world.transform);instance.name=prop.n;instance.transform.localPosition=prop.p;}}}
   controller.enabled=false;player.transform.position=new Vector3(S.x,.02f,S.z);controller.enabled=true;yaw=S.yaw;pitch=S.pitch;player.transform.rotation=Quaternion.Euler(0,yaw,0);View.transform.localRotation=Quaternion.Euler(pitch,0,0);
   Target="full";Reading="—.——— m";UI?.Refresh();
  }
  void SetupAudio(){ambient=gameObject.AddComponent<AudioSource>();ambient.loop=true;ambient.volume=.13f;var clip=AudioClip.Create("Ventilation 50Hz",44100*4,1,44100,false);var values=new float[44100*4];var random=new System.Random(8);float brown=0;for(int i=0;i<values.Length;i++){brown=(brown+(float)(random.NextDouble()*2-1)*.02f)/1.02f;values[i]=Mathf.Sin(i*2*Mathf.PI*50/44100)*.12f+brown;}clip.SetData(values,0);ambient.clip=clip;beeper=gameObject.AddComponent<AudioSource>();beep=Tone(1180,.16f);foot=Tone(75,.1f);}
  AudioClip Tone(float frequency,float duration){int count=(int)(44100*duration);var c=AudioClip.Create("Survey tone",count,1,44100,false);var v=new float[count];for(int i=0;i<count;i++)v[i]=Mathf.Sin(i*2*Mathf.PI*frequency/44100)*Mathf.Exp(-i/(float)count*7)*.25f;c.SetData(v,0);return c;}
  public void StartSurvey(bool resume){if(resume&&File.Exists(SavePath)){try{var state=JsonUtility.FromJson<SurveyState>(File.ReadAllText(SavePath));if(state.version!=1||state.zone<0||state.zone>8)throw new Exception("Invalid save");Engine=new SurveyEngine(Engine.Data,state);}catch{Engine=new SurveyEngine(Engine.Data);}}else Engine=new SurveyEngine(Engine.Data);playing=true;ambient.Play();BuildWorld(S.zone);UI.Refresh();UI.Radio("P로 기준점을 잡고 측정하세요. 숫자는 TAB 야장에 남기시고요. 기존 점은 복도 끝에 있습니다.");Changed();}
  public void Measure(){Measurement m;var error=Engine.Measure(Target,out m);if(error!=null){UI.Toast(error);Changed();return;}Reading=$"{m.value:0.000} {m.unit}";beeper.pitch=1-S.drift*.001f;beeper.PlayOneShot(beep);Aim();UI.Toast($"{m.reference} → {m.value:0.000} {m.unit} / 원본 기록 보존");if(S.zone==1&&m.target=="full"&&m.reference.StartsWith("P"))UI.Radio("41미터요? 도면에는 24.6인데요. 끝벽 K-12에서 반대로 재 보시고, 의심 가는 기록은 야장에 표시해 주세요.");Changed();}
  public void Aim(){Vector3 p=new Vector3(0,1.5f,S.reference=="K"?3:-length+.2f);if(Target=="standard")p=new Vector3(-width/2,1.4f,0);else if(Target.StartsWith("s")&&Target!="standard"){int n=int.Parse(Target.Substring(1));p.z=-(S.zone==1?n*3.075f+(n>=6?16.6f:0):n*length/8);};var direction=p-View.transform.position;var rot=Quaternion.LookRotation(direction).eulerAngles;yaw=rot.y;pitch=rot.x>180?rot.x-360:rot.x;}
  string NearbyAction(){
   var p=player.transform.position;var here=new Vector2(p.x,p.z);
   if(p.z < -length+1.7f && Mathf.Abs(p.x)<.95f)return "exit";
   string best="";float distance=3.2f;
   if(S.zone>0){float d=Vector2.Distance(here,new Vector2(-width/2+.5f,-length+2.2f));if(d<distance){best="K";distance=d;}}
   float charger=Vector2.Distance(here,new Vector2(-width/2+.5f,-2.8f));if(charger<distance){best="charge";distance=charger;}
   if(S.zone>0&&(S.zone!=1||S.solved[1])){float d=Vector2.Distance(here,new Vector2(width/2+2.4f,gap-.5f));if(d<distance){best="note";distance=d;}}
   return best;
  }
  public string InteractionHint(){switch(NearbyAction()){case "exit":return S.zone==8?"[ E ] 최종 성과도 열기":"[ E ] 다음 구역으로 이동";case "K":return "[ E ] "+Engine.Current.point+" 기준 채택";case "charge":return "[ E ] 장비 충전 · 25분";case "note":return "[ E ] 1991년 야장 열람";default:return "";}}
  public void Use(){
   switch(NearbyAction()){
    case "K":UI.Toast(Engine.Adopt());Aim();break;
    case "charge":UI.Toast(Engine.Recharge());break;
    case "note":Engine.ReadNote();UI.OpenNotebook("archive");if(S.zone==7)UI.Radio("예전 도면하고 너무 다르면 맞춰서 내면 됩니다. — 회사 대표");break;
    case "exit":Advance();break;
    default:UI.Toast("기준점, 충전함, 기록실 가까이에서 E로 조사하세요.");break;
   }Changed();
  }
  public void Advance(){if(S.zone==8){UI.OpenNotebook("sign");return;}string error=Engine.Advance();if(error!=null){UI.Toast(error);return;}UI.Close();BuildWorld(S.zone);UI.Radio((Engine.Current?.name??"지상")+"입니다. 확인한 값과 추측한 값은 구분해 주세요.");Changed();}
  public void Revisit(int zone){string error=Engine.Revisit(zone);if(error!=null){UI.Toast(error);return;}UI.Close();BuildWorld(S.zone);Changed();}
  public void Changed(){Shader.SetGlobalFloat("_ClosureDrift",S.drift/100);if(S.deadline&&string.IsNullOrEmpty(S.ending)){S.zone=8;S.maxZone=8;BuildWorld(8);UI.OpenNotebook("sign");UI.Toast("05:00 마감. 확보한 기록으로 서명하세요.");}int w=S.drift>=60?480:640,h=S.drift>=60?270:360;if(lowResolution.width!=w){lowResolution.Release();lowResolution.width=w;lowResolution.height=h;lowResolution.Create();}Save();UI.RefreshHud();}
  public void Save(){if(!playing)return;var p=player.transform.position;S.x=p.x;S.z=p.z;S.yaw=yaw;S.pitch=pitch;try{File.WriteAllText(SavePath,JsonUtility.ToJson(S,true));}catch(Exception e){Debug.LogWarning(e.Message);}}
  public void ReturnTitle(){Save();playing=false;ambient.Stop();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;UI.Title();}
  public void ToggleAudio(){ambient.mute=!ambient.mute;beeper.mute=ambient.mute;UI.Toast(ambient.mute?"환경음 꺼짐":"환경음 켜짐");}
  void Update(){if(!playing||UI==null)return;var kb=Keyboard.current;if(kb==null)return;if(kb.escapeKey.wasPressedThisFrame){if(UI.ModalOpen)UI.Close();else UI.Pause();return;}if(!string.IsNullOrEmpty(S.ending))return;if(kb.tabKey.wasPressedThisFrame){if(UI.ModalOpen)UI.Close();else UI.OpenNotebook("field");return;}if(UI.ModalOpen)return;
   if(kb.pKey.wasPressedThisFrame){UI.Toast(Engine.Install());Changed();}if(kb.qKey.wasPressedThisFrame){UI.Toast(Engine.Toggle());Aim();Changed();}if(kb.spaceKey.wasPressedThisFrame)Measure();if(kb.eKey.wasPressedThisFrame)Use();if(kb.rKey.wasPressedThisFrame){UI.Radio(Engine.Hint());Changed();}if(kb.fKey.wasPressedThisFrame)torch.enabled=!torch.enabled;
   float x=(kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),z=(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0);yaw+=((kb.rightArrowKey.isPressed?1:0)-(kb.leftArrowKey.isPressed?1:0))*Time.deltaTime*90;
   if(Cursor.lockState==CursorLockMode.Locked&&Mouse.current!=null){var delta=Mouse.current.delta.ReadValue();yaw+=delta.x*.12f;pitch=Mathf.Clamp(pitch-delta.y*.12f,-65,65);}
   player.transform.rotation=Quaternion.Euler(0,yaw,0);View.transform.localRotation=Quaternion.Euler(pitch,0,0);var direction=player.transform.TransformDirection(new Vector3(x,0,z).normalized);controller.SimpleMove(direction*(kb.leftShiftKey.isPressed?4:2.5f));float distance=direction.magnitude*Time.deltaTime*2.5f;walkBudget+=distance;stepBudget+=distance;if(stepBudget>1.4f){stepBudget=0;beeper.PlayOneShot(foot,.3f);}if(walkBudget>7){walkBudget=0;Engine.Spend(2);Changed();}autosave+=Time.deltaTime;if(autosave>5){autosave=0;Save();}UI.RefreshHud();
   if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&!EventSystem.current.IsPointerOverGameObject()){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
  }
  void RefreshFont(Font changed){if(changed==font&&worldTextMaterial)worldTextMaterial.mainTexture=font.material.mainTexture;}void OnApplicationPause(bool pause){if(pause)Save();}void OnApplicationQuit(){Save();}void OnDestroy(){Font.textureRebuilt-=RefreshFont;if(lowResolution)lowResolution.Release();}
 }
}
