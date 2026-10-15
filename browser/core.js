import {ZONES,ROUTE} from './data.js';
export function fresh(){return {version:1,zone:0,maxZone:0,minutes:520,battery:7,drift:0,points:4,installed:false,reference:'P',records:[],judgments:{},located:{},notes:[],help:{},flags:{},route:[],closed:false,ending:null,position:null};}
export function spend(s,minutes,drift=0){s.minutes=Math.min(1740,s.minutes+minutes);s.drift=Math.max(0,Math.min(100,s.drift+drift));if(s.minutes>=1740&&!s.ending)s.flags.deadline=true;}
export function install(s){if(s.installed){s.installed=false;s.points++;s.reference=s.flags['K'+s.zone]?'K':null;spend(s,5,-3);return '기준점을 회수했습니다.';}if(s.points<=0)return '기준점이 없습니다. 이전 점을 회수하세요.';s.installed=true;s.points--;s.reference='P';spend(s,8,5);return '새 기준점 P를 설치했습니다. [SPACE] 측정';}
export function adopt(s){s.reference='K';s.flags['K'+s.zone]=true;spend(s,2);return `${ZONES[s.zone-1]?.point||'K-01'}을 기준으로 채택했습니다.`;}
export function observe(s,target='full'){
 if(s.ending||s.flags.deadline)return {error:'마감 시각입니다. 성과도에 서명하세요.'};
 if(!s.battery)return {error:'배터리가 없습니다. 녹색 충전함에서 E로 충전하세요.'};
 if(s.reference!=='K'&&!s.installed)return {error:'P로 기준점을 먼저 설치하세요.'};
 const z=ZONES[s.zone-1];let value=z?(s.reference==='K'?z.k:z.p):12.41,unit='m';
 if(target==='standard')value=5;
 if(s.zone===1&&/^s/.test(target)&&target!=='standard'){const n=+target.slice(1);value=3.075*n+(s.reference==='P'&&n>=6?16.6:0);}
 if(target==='up')value=6.4;if(target==='down')value=-9.8;
 if(target==='angleA'){value=38;unit='°';}if(target==='angleB'){value=62;unit='°';}
 if(target==='old')value=8.4;if(target==='new')value=13.1;
 if(target==='lights'){value=s.drift>=30?9:8;unit='箇';}
 if(target==='loopA')value=s.reference==='K'?12.4:14.6;
 if(target==='loopB')value=s.reference==='K'?17.2:15;
 if(target==='origin')value=0;
 const currentRef=s.reference==='K'?z.point:`P-${s.zone||'G'}`;
 const repeat=s.records.some(r=>r.zone===s.zone&&r.target===target&&r.ref===currentRef);
 if(s.drift>=60&&s.reference==='P'&&repeat&&target==='full'&&s.zone>1)value+=.04;
 s.battery--;spend(s,repeat?2:3,s.reference==='K'?0:(repeat?1:2));
 const r={id:s.records.length+1,zone:s.zone,target,value:+value.toFixed(3),unit,ref:s.reference==='K'?z.point:`P-${s.zone||'G'}`,time:s.minutes,suspect:false};s.records.push(r);return {record:r};
}
export function evidence(s){const rec=s.records.filter(r=>r.zone===s.zone);const has=t=>rec.some(r=>r.target===t);switch(s.zone){case 1:return has('s5')&&has('s6')&&rec.some(r=>r.ref.startsWith('K'))&&rec.some(r=>r.ref.startsWith('P'));case 2:return has('up')&&has('down');case 3:return has('angleA')&&has('angleB');case 4:return has('old')&&has('new');case 5:return rec.some(r=>r.target==='lights'&&r.value===9);case 6:return has('loopA')&&has('loopB');case 7:return s.notes.includes(7);default:return false;}}
export function locate(s,answer){const z=ZONES[s.zone-1];if(!evidence(s))return {ok:false,message:'아직 판정 근거가 부족합니다. 해당 구간을 비교 측정하세요.'};spend(s,4);if(answer!==z.answer){spend(s,0,3);s.flags['wrong'+s.zone]=true;return {ok:false,message:'표시한 위치와 측량 기록이 맞지 않습니다. 원본 값을 다시 대조하세요.'};}if(z.u)s.located[z.u]=s.zone;s.flags['solved'+s.zone]=true;return {ok:true,message:z.u?`${z.u}의 위치를 야장에 표시했습니다. 현장의 기록을 확인하세요.`:'1991년의 미서명 기록을 확인했습니다.'};}
export function judge(s,type){const z=ZONES[s.zone-1];const records=s.records.filter(r=>r.zone===s.zone&&r.target==='full');if(!records.length)return {error:'전체 구간을 먼저 측정하세요.'};if(type==='K'&&!records.some(r=>r.ref.startsWith('K')))return {error:'K 기준의 전체 구간 기록이 필요합니다.'};if(type==='P'&&!records.some(r=>r.ref.startsWith('P')))return {error:'P 기준의 전체 구간 기록이 필요합니다.'};s.judgments[s.zone]={type,status:type==='hold'?'보류':type==='K'?'확정':'보류',value:type==='K'?z.k:z.p,error:type==='K'?0:Math.abs(z.p-z.k),help:s.help[s.zone]||0};spend(s,6,type==='P'?8:0);return {ok:true};}
export function next(s){if(s.zone===0){if(!s.records.some(r=>r.zone===0))return {error:'지상 구간을 먼저 측정하세요.'};}else if(!s.judgments[s.zone])return {error:'야장에서 구간 판정을 먼저 남겨 주세요.'};if(s.installed){s.points++;s.installed=false;spend(s,5,-3);}s.zone++;s.maxZone=Math.max(s.maxZone||0,s.zone);s.reference=null;s.position=null;s.route=[];if(s.zone===1)s.minutes=872;else{spend(s,20);s.minutes=Math.max(s.minutes,[0,872,990,1080,1170,1260,1350,1470,1650][s.zone]);}return {ok:true};}
export function revisit(s,zone){if(!Number.isInteger(zone)||zone<1||zone>(s.maxZone||s.zone)||s.flags.deadline)return {error:'현재 재방문할 수 없는 구역입니다.'};if(s.installed){s.points++;s.installed=false;spend(s,5,-3);}spend(s,12);s.zone=zone;s.reference=null;s.position=null;return {ok:true};}
export function recharge(s){if(s.battery===7)return '충전되어 있습니다.';s.battery=7;spend(s,25);return '배터리 충전 완료. 현장 시간 25분 경과.';}
export function hint(s){const z=ZONES[s.zone-1]||ZONES[0];const n=s.help[s.zone]||0;s.help[s.zone]=n+1;spend(s,10);return z.hint[Math.min(2,n)];}
export function readNote(s){if(!s.notes.includes(s.zone))s.notes.push(s.zone);return ZONES[s.zone-1]?.note||'숫자가 이상하면 표준척부터 재 봐. 5m짜리.';}
export function traverse(s,id){if(s.zone!==8)return {error:'K-01의 최종 성과도에서 폐합하세요.'};if(Object.keys(s.located).length<6||!s.notes.includes(7))return {error:'여섯 미확인 공간의 위치와 마지막 야장을 확인해야 합니다.'};if(s.flags.deadline)return {error:'마감 시각입니다. 확보한 기록으로 서명하세요.'};if(!s.battery)return {error:'충전함에서 장비를 충전하세요.'};if(s.closed)return {error:'이미 폐합했습니다.'};if(s.route.length>=7)s.route=[];s.battery--;s.route.push(id);spend(s,3);if(s.route.length===7){s.closed=s.route.every((v,i)=>v===ROUTE[i]);return {ok:true,done:true,errorValue:s.closed?0:Math.max(.23,s.route.filter((v,i)=>v!==ROUTE[i]).length*.73)};}return {ok:true,done:false};}
export function sign(s,type,name){if(!name?.trim())return {error:'서명자 이름을 입력하세요.'};if(!['A','B','C','D','E'].includes(type))return {error:'유효한 서명 방식을 선택하세요.'};if(type==='D'&&(!s.closed||Object.keys(s.located).length!==6))return {error:'모든 미확인 공간을 포함한 폐합이 필요합니다.'};s.signature=name.trim().slice(0,32);s.ending=type;return {ok:true};}
export function validateSave(raw){if(!raw||raw.version!==1||!Number.isInteger(raw.zone)||raw.zone<0||raw.zone>8||!Array.isArray(raw.records)||!Array.isArray(raw.notes)||!raw.judgments||!raw.located||!Number.isFinite(raw.minutes)||raw.minutes<520||raw.minutes>1740||!Number.isInteger(raw.battery)||raw.battery<0||raw.battery>7||!Number.isFinite(raw.drift)||raw.drift<0||raw.drift>100||!Number.isInteger(raw.points)||raw.points<0||raw.points>4)return null;return {...fresh(),...raw};}
