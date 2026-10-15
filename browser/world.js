import * as T from 'three';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {ZONES} from './data.js';
const V=T.Vector3;
export class World{
 constructor(canvas){
  this.renderer=new T.WebGLRenderer({canvas,antialias:false,powerPreference:'high-performance'});this.renderer.setPixelRatio(1);this.renderer.outputColorSpace=T.SRGBColorSpace;
  this.scene=new T.Scene();this.scene.background=new T.Color('#0c140f');this.scene.fog=new T.FogExp2('#121b14',.037);
  this.camera=new T.PerspectiveCamera(67,innerWidth/innerHeight,.05,90);this.camera.rotation.order='YXZ';this.scene.add(this.camera);
  this.target=new T.WebGLRenderTarget(640,360,{minFilter:T.NearestFilter,magFilter:T.NearestFilter});
  this.post=new T.Scene();this.postCam=new T.OrthographicCamera(-1,1,1,-1,0,1);
  this.postMat=new T.ShaderMaterial({uniforms:{image:{value:this.target.texture},time:{value:0},drift:{value:0},resolution:{value:new T.Vector2(640,360)}},vertexShader:'varying vec2 vUv;void main(){vUv=uv;gl_Position=vec4(position,1.);}',fragmentShader:`precision highp float;uniform sampler2D image;uniform float time;uniform float drift;uniform vec2 resolution;varying vec2 vUv;float hash(vec2 p){return fract(sin(dot(p,vec2(12.9898,78.233)))*43758.5453);}void main(){vec2 uv=vUv;uv.x+=sin(uv.y*140.+time*3.)*.00045*drift;vec3 c=texture2D(image,uv).rgb;c=pow(c,vec3(.4545));float n=hash(floor(vUv*resolution)+floor(time*18.));c+=(n-.5)*(.022+drift*.04);float d=mod(floor(vUv.x*resolution.x)+mod(floor(vUv.y*resolution.y),2.)*2.,4.);c=floor(c*32.+d*.17)/32.;float vig=smoothstep(.8,.22,length((vUv-.5)*vec2(1.,.9)));c*=.72+.28*vig;c*=.985+.015*sin(vUv.y*resolution.y*3.14159);gl_FragColor=vec4(c,1.);}`});
  this.post.add(new T.Mesh(new T.PlaneGeometry(2,2),this.postMat));
  this.scene.add(new T.HemisphereLight('#85967d','#11160e',1.05));
  this.torch=new T.SpotLight('#dce4b0',11,22,Math.PI*.24,.85,1.1);this.camera.add(this.torch);this.torch.position.set(.12,-.1,.05);this.torch.target.position.set(0,-.05,-8);this.camera.add(this.torch.target);
  this.group=new T.Group();this.scene.add(this.group);this.yaw=0;this.pitch=0;this.step=0;this.keys={};this.interactions=[];this.lights=[];this.extra=[];this.asset=null;
  const b64=__ASSET__;const bytes=Uint8Array.from(atob(b64),c=>c.charCodeAt(0));new GLTFLoader().parse(bytes.buffer,'',g=>{this.asset=g.scene;if(this.zone!==undefined)this.addAssets();},e=>console.warn('Asset',e));
  this.build(1);this.camera.position.set(1.05,1.62,3);this.yaw=-.055;this.pitch=.025;this.resize();addEventListener('resize',()=>this.resize());
 }
 resize(){const ratio=innerWidth/innerHeight;this.camera.aspect=ratio;this.camera.updateProjectionMatrix();this.renderer.setSize(innerWidth,innerHeight,false);this.target.setSize(Math.round(360*ratio),360);this.postMat.uniforms.resolution.value.set(this.target.width,this.target.height);}
 mat(color,texture=null){return new T.MeshLambertMaterial({color,map:texture});}
 concrete(color,floor=false){const c=document.createElement('canvas');c.width=c.height=128;const ctx=c.getContext('2d');ctx.fillStyle=color;ctx.fillRect(0,0,128,128);let seed=51;const random=()=>{seed=(seed*1664525+1013904223)>>>0;return seed/4294967296};for(let i=0;i<5600;i++){const a=random()*.1;ctx.fillStyle=random()>.5?`rgba(0,0,0,${a})`:`rgba(230,234,211,${a})`;const x=random()*128,y=random()*128;ctx.fillRect(x,y,random()*3+1,random()*2+1);}ctx.strokeStyle='#111a1130';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(0,127);ctx.lineTo(128,127);if(floor){ctx.moveTo(127,0);ctx.lineTo(127,128);}ctx.stroke();if(!floor){for(let j=0;j<5;j++){ctx.fillStyle='#151d1215';ctx.fillRect(random()*128,0,random()*8,random()*90);}}const t=new T.CanvasTexture(c);t.magFilter=T.NearestFilter;t.minFilter=T.NearestFilter;t.wrapS=t.wrapT=T.RepeatWrapping;t.colorSpace=T.SRGBColorSpace;return t;}
 box(w,h,d,x,y,z,mat){const m=new T.Mesh(new T.BoxGeometry(w,h,d),mat);m.position.set(x,y,z);this.group.add(m);return m;}
 pipe(radius,length,x,y,z,color,axis='z'){const m=new T.Mesh(new T.CylinderGeometry(radius,radius,length,8),this.mat(color));m.position.set(x,y,z);if(axis==='z')m.rotation.x=Math.PI/2;if(axis==='x')m.rotation.z=Math.PI/2;this.group.add(m);return m;}
 label(text,x,y,z,w=1,h=.48,color='#c3cdb4',bg='#26382c',ry=0){const c=document.createElement('canvas');c.width=512;c.height=Math.round(512*h/w);const ctx=c.getContext('2d');ctx.fillStyle=bg;ctx.fillRect(0,0,c.width,c.height);ctx.strokeStyle=color;ctx.globalAlpha=.45;ctx.strokeRect(7,7,c.width-14,c.height-14);ctx.globalAlpha=1;ctx.fillStyle=color;const lines=text.split('\n');const fs=Math.min(c.height/(lines.length*1.65),c.width/(Math.max(...lines.map(s=>s.length))*.72+1));ctx.font=`${fs}px 'Malgun Gothic',Consolas,sans-serif`;ctx.textAlign='center';ctx.textBaseline='middle';lines.forEach((l,i)=>ctx.fillText(l,c.width/2,c.height/2+(i-(lines.length-1)/2)*fs*1.5));const tex=new T.CanvasTexture(c);tex.colorSpace=T.SRGBColorSpace;tex.magFilter=T.NearestFilter;const m=new T.Mesh(new T.PlaneGeometry(w,h),new T.MeshBasicMaterial({map:tex,side:T.DoubleSide}));m.position.set(x,y,z);m.rotation.y=ry;this.group.add(m);return m;}
 marker(x,y,z,color='#b7c0a0'){this.box(.35,.35,.035,x,y,z,this.mat('#333f30'));this.label('＋',x,y,z+.022,.32,.32,color,'#29362a');}
 build(zone,saved=null){
  this.zone=zone;this.group.traverse(o=>{if(o.geometry)o.geometry.dispose();});this.group.clear();this.interactions=[];this.lights=[];this.extra=[];this.foldDoor=null;
  const z=ZONES[zone-1];this.length=z?.length||18;this.width=zone===7?9:zone===3?6:4.4;const L=this.length,W=this.width;
  const concrete=this.mat('#959a86',this.concrete('#a4a692'));const lower=this.mat('#697c65',this.concrete('#6c7b63'));const floor=this.mat('#777b69',this.concrete('#858874',true));floor.map.repeat.set(W/2,L/2);concrete.map.repeat.set(1,2);lower.map.repeat.set(1,1);const dark=this.mat('#2d382b');const steel=this.mat('#56614e');
  this.box(W+.5,.18,L+8,0,-.12,-L/2+2,floor);this.box(W+.5,.22,L+8,0,3.7,-L/2+2,concrete);
  const gap=zone===1?-25.2125:-L*.66;this.gap=gap;const a=5-gap-1.6,b=L+gap-1.6;
  for(const side of [-1,1]){
   if(side<0){this.box(.25,2.25,L+5,side*W/2,2.5,-L/2+2,concrete);this.box(.27,1.35,L+5,side*W/2,.675,-L/2+2,lower);}
   else{for(const [len,center] of [[a,(5+gap+1.6)/2],[b,(gap-1.6-L)/2]]){this.box(.25,2.25,len,W/2,2.5,center,concrete);this.box(.27,1.35,len,W/2,.675,center,lower);}}
   this.box(.05,.055,L+5,side*(W/2-.16),1.38,-L/2+2,steel);this.box(.06,.12,L+5,side*(W/2-.17),.06,-L/2+2,dark);
   this.box(.05,.008,L+3,side*(W/2-.52),.005,-L/2+1,this.mat('#bcaf68'));
  }
  this.box(W+.5,3.7,.25,0,1.8,-L-.1,concrete);this.box(1.6,2.7,.12,0,1.35,-L+.06,this.mat(zone===7?'#28332b':'#5d4234'));this.box(.07,.07,.16,.55,1.25,-L+.2,this.mat('#c2b797'));
  this.label(zone===0?'지 질 관 측 소\n지 하 출 입 구':zone===8?'성과도 제출 / K-01':'다음 구역 →',0,2.95,-L+.04,1.7,.38,'#b6c6a5','#283c2d');
  this.label('서진산 지질관측소\nSEOJINSAN / 1991',-W/2+.15,2,-2,1.3,.7,'#b9c4a7','#314133',Math.PI/2);
  this.label(zone===0?'안전모 착용\n관계자 외 출입 금지':`${z.floor} / ${z.name}\n시설 폐쇄 · 1991`,0,3.1,3.8,2,.5,'#b8c29f','#23332a',Math.PI);
  for(let i=1;i<=8;i++){
   const segmentStart=(i-1)*3.075+(i>6?16.6:0),segmentEnd=i*3.075+(i>=6?16.6:0);
   const p=zone===1?-(segmentStart+segmentEnd)/2:-(i-.5)*L/8;
   this.box(W+.2,.2,.3,0,3.38,p,steel);this.box(1.9,.12,.5,0,3.19,p,dark);this.box(1.62,.035,.2,0,3.11,p,new T.MeshBasicMaterial({color:'#ccd6b6'}));
   this.label(String(i).padStart(2,'0'),-W/2+.151,2.7,p,.55,.36,'#c1c9a8','#3b4d37',Math.PI/2);
   this.box(.13,3.3,.25,-W/2+.14,1.65,p,steel);this.box(.13,3.3,.25,W/2-.14,1.65,p,steel);
   const light=new T.PointLight(i===6?'#bacb97':'#c6d5aa',15,11,1.65);light.position.set(0,2.85,p);this.group.add(light);this.lights.push(light);
   this.marker(0,1.5,-(zone===1?segmentEnd:i*L/8)+.03,i===6?'#c6ba7a':'#abc8a8');
  }
  for(let i=0;i<3;i++){this.pipe(.07+i*.015,L+5,W/2-.35-i*.26,3.34,-L/2+1,i===0?'#82624a':'#404f3c');for(let j=0;j<7;j++)this.pipe(.115,.07,W/2-.35-i*.26,3.34,-j*L/7,i===0?'#926b49':'#58614c');}
  // An alcove whose position is fixed, even while its reported coordinates disagree.
  this.box(5,.15,3.3,W/2+2.4,-.1,gap,floor);this.box(5,3.6,.2,W/2+2.4,1.8,gap-1.7,concrete);this.box(5,3.6,.2,W/2+2.4,1.8,gap+1.7,lower);this.box(.2,3.6,3.5,W/2+4.9,1.8,gap,concrete);this.box(5,.2,3.5,W/2+2.4,3.6,gap,concrete);
  this.label(zone===1?'기록실 / 06':zone===3?'A 38° / B 62°\n관측기록실':'보조 작업실',W/2+.3,2.8,gap+1.58,2,.45,'#b0bda0','#293c2e');
  this.box(1.6,.1,.8,W/2+2.4,.88,gap-.75,steel);for(const x of [-.65,.65])for(const dz of [-.3,.3])this.box(.06,.88,.06,W/2+2.4+x,.44,gap-.75+dz,dark);
  const paper=this.box(.55,.012,.38,W/2+2.4,.95,gap-.6,this.mat('#bcb99a'));paper.rotation.y=.2;this.label('1991\n현장 야장',W/2+2.4,1.5,gap-1.55,.8,.55,'#acb397','#27372b');
  const lamp=new T.PointLight('#d5bc85',12,7,1.7);lamp.position.set(W/2+2.4,2.6,gap);this.group.add(lamp);this.lights.push(lamp);
  this.interactions.push({type:'note',name:'1991년 야장 열람',x:W/2+2.4,z:gap-.5});
  if(zone===1||zone===5)this.foldDoor=this.box(.3,3.4,3.2,W/2,1.7,gap,lower);
  // Old control point, charging case, and the exit are physical interaction targets.
  this.label(`${z?.point||'K-01'}\n1991 · 기준점`, -W/2+.151,1.55,-L+2.2,.83,.66,'#c9d1b1','#3e4b38',Math.PI/2);
  this.marker(-W/2+.2,.7,-L+2.3);this.interactions.push({type:'K',name:`${z?.point||'K-01'} 기준 채택`,x:-W/2+.5,z:-L+2.2});
  this.box(.28,.8,.6,-W/2+.28,.75,-2.8,this.mat('#47583d'));this.label('충전함\nAC 220V',-W/2+.435,1.3,-2.8,.65,.5,'#c7d4b3','#2b4a2e',Math.PI/2);this.interactions.push({type:'charge',name:'장비 충전 · 25분',x:-W/2+.55,z:-2.8});
  this.interactions.push({type:'exit',name:zone===0?'지하 진입':zone===8?'최종 성과도 열기':'다음 구역으로 이동',x:0,z:-L+1.6});
  this.label('5.000 m\n검정 표준척',-W/2+.151,1.7,.2,1.4,.6,'#d3c78c','#3f4931',Math.PI/2);for(let i=0;i<12;i++)this.box(.02,.08,.12,-W/2+.16,.85,1-i*.42,this.mat(i%2?'#b7b9a0':'#424d39'));
  if(zone===2){for(let i=0;i<14;i++)this.box(3,.2,.32,W/2+2.5,-i*.2-.2,gap+i*.28-1.4,concrete);this.label('02\n水準標 −3.400',W/2+.16,2.1,gap+.9,1.2,.7,'#ca9b78','#3a3a2b');for(const x of [-.5,.5])this.pipe(.035,3,x,1.5,gap,'#716d4e','x');}
  if(zone===3){this.label('北東 / B',0,2.7,-L+.2,1.6,.4);this.label('38°',-W/2+.18,2.5,-8,.7,.5,'#d4c18d','#354531',Math.PI/2);this.label('62°',W/2-.18,2.5,-14,.7,.5,'#d4c18d','#354531',-Math.PI/2);}
  if(zone===4){this.label('K-07\n2026 · 基準点',W/2-.151,1.7,-8,1,.65,'#eeeece','#798b62',-Math.PI/2);this.label('K-07\n1991 · 原点',-W/2+.151,1.7,-14,1,.65,'#b6b89e','#4a5140',Math.PI/2);}
  if(zone===5){for(let i=0;i<5;i++)this.pipe(.15,L-4,-W/2+.4+i*.28,2.2-i*.22,-L/2,'#5d6551');const extra=this.label('09',W/2-.151,2.6,gap,.75,.55,'#dbba80','#4c4930',-Math.PI/2);this.extra.push(extra);}
  if(zone===6){this.label('LOOP A / +2.200',-W/2+.15,2,-9,1.5,.5,'#bbbf9c','#37452f',Math.PI/2);this.label('LOOP B / −2.200',W/2-.15,2,-18,1.5,.5,'#bbbf9c','#37452f',-Math.PI/2);}
  if(zone===7){for(let i=0;i<24;i++){const side=i%2?-1:1;const rock=new T.Mesh(new T.IcosahedronGeometry(1.3+(i%4)*.2,0),this.mat('#414e3b'));rock.position.set(side*(W/2-.3),.7+(i%3)*.5,-i*L/24);rock.scale.set(1,1.5,1);this.group.add(rock);}this.label('K-01\n최초 기준점',0,2.4,-L+.2,1.7,.7,'#c6c8a4','#3c4432');}
  if(zone===0){this.label('안전 제일\n지하 구조물 사전 측량',0,2.7,-9,2,.7,'#d3cc94','#3b492f');}
  this.camera.position.set(saved?.x||0,1.63,saved?.z??2.2);this.yaw=saved?.yaw||0;this.pitch=saved?.pitch||0;this.addAssets();
 }
 addAssets(){if(!this.asset)return;const L=this.length,W=this.width;const add=(name,x,z,rot=0,scale=1)=>{const source=this.asset.getObjectByName(name);if(!source)return;const o=source.clone(true);o.position.set(x,0,z);o.rotation.y=rot;o.scale.setScalar(scale);this.group.add(o);};add('TotalStation',-1.2,1,-.2);add('FieldGenerator',-W/2+.7,-6,.3,.8);add('ArchiveCabinet',W/2+3.8,this.gap+.9,Math.PI/2);if(this.zone===4){for(let i=0;i<5;i++)add('ArchiveCabinet',i%2?-W/2+.5:W/2-.5,-4-i*3,i%2?Math.PI/2:-Math.PI/2,.85);}}
 position(){return {x:this.camera.position.x,z:this.camera.position.z,yaw:this.yaw,pitch:this.pitch};}
 canWalk(x,z){const r=.25;if(z>4||z<-this.length+.4)return false;const main=Math.abs(x)<this.width/2-r;const alcove=x>0&&x<this.width/2+4.6&&Math.abs(z-this.gap)<1.35&&!(this.foldDoor?.visible&&x>this.width/2-r);return main||alcove;}
 mouse(dx,dy){this.yaw-=dx*.0024;this.pitch=Math.max(-.9,Math.min(.9,this.pitch-dy*.0021));}
 aim(target,ref='P'){let point=new V(0,1.5,-this.length+.2);if(target==='standard')point.set(-this.width/2,1.4,-.5);else if(/^s[0-9]/.test(target)){const n=+target.slice(1);point.z=-(this.zone===1?n*3.075+(n>=6?16.6:0):n*this.length/8);}else if(ref==='K'&&target==='full')point.z=3;else if(target==='old')point.set(-this.width/2,1.7,-14);else if(target==='new')point.set(this.width/2,1.7,-8);const d=point.clone().sub(this.camera.position).normalize();this.yaw=Math.atan2(-d.x,-d.z);this.pitch=Math.asin(d.y);this.laserTo=point;}
 nearest(s){let best=null,dist=3.5;for(const item of this.interactions){if(item.type==='K'&&s.zone===0)continue;if(item.type==='note'&&s.zone===1&&!s.located['U-1'])continue;const d=Math.hypot(this.camera.position.x-item.x,this.camera.position.z-item.z);if(d<dist){best=item;dist=d;}}return best;}
 flashLaser(target,ref){this.aim(target,ref);if(this.laser){this.group.remove(this.laser);this.laser.geometry.dispose();}const from=this.camera.position.clone();from.y-=.12;const to=this.laserTo;const geometry=new T.BufferGeometry().setFromPoints([from,to]);this.laser=new T.Line(geometry,new T.LineBasicMaterial({color:'#ff5e3b',transparent:true,opacity:.8}));this.group.add(this.laser);this.laserUntil=performance.now()+420;}
 tick(dt,time,s,playing){
  let walked=0;if(playing){const keys=this.keys;const speed=(keys.ShiftLeft?4:2.5)*dt;let f=(keys.KeyW||keys.ArrowUp?1:0)-(keys.KeyS||keys.ArrowDown?1:0),r=(keys.KeyD?1:0)-(keys.KeyA?1:0);if(f&&r){f*=.707;r*=.707;}this.yaw+=((keys.ArrowLeft?1:0)-(keys.ArrowRight?1:0))*dt*1.5;const dx=(-Math.sin(this.yaw)*f+Math.cos(this.yaw)*r)*speed,dz=(-Math.cos(this.yaw)*f-Math.sin(this.yaw)*r)*speed;const p=this.camera.position;if(this.canWalk(p.x+dx,p.z)){p.x+=dx;walked+=Math.abs(dx);}if(this.canWalk(p.x,p.z+dz)){p.z+=dz;walked+=Math.abs(dz);}this.step+=walked;this.camera.position.y=1.63+Math.sin(this.step*4)*Math.min(.025,walked*.9);}
  this.camera.rotation.set(this.pitch,this.yaw,0,'YXZ');this.postMat.uniforms.time.value=time;this.postMat.uniforms.drift.value=s.drift/100;
  for(let i=0;i<this.lights.length;i++){const flicker=(Math.sin(time*(i===5?29:13)+i*7)> .985&&s.drift>20)?.18:1;this.lights[i].intensity=(i===8?12:15)*flicker;}
  this.extra.forEach(m=>m.visible=s.drift>=30);if(this.foldDoor)this.foldDoor.visible=this.zone===1?!s.located['U-1']:s.drift<30;const renderHeight=s.drift>=60?270:360;if(this.target.height!==renderHeight){this.target.setSize(Math.round(renderHeight*innerWidth/innerHeight),renderHeight);this.postMat.uniforms.resolution.value.set(this.target.width,this.target.height);}if(this.laser&&performance.now()>this.laserUntil){this.group.remove(this.laser);this.laser=null;}
  this.renderer.setRenderTarget(this.target);this.renderer.render(this.scene,this.camera);this.renderer.setRenderTarget(null);this.renderer.render(this.post,this.postCam);return walked;
 }
}
