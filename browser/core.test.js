import test from 'node:test';
import assert from 'node:assert/strict';
import * as C from './core.js';
import {ZONES,ROUTE,ENDINGS} from './data.js';
function ground(){const s=C.fresh();C.install(s);C.observe(s);C.next(s);return s;}
function measure(s,t){const r=C.observe(s,t);assert.ok(!r.error,r.error);return r.record;}
function solveZone(s){const z=ZONES[s.zone-1];C.recharge(s);C.install(s);measure(s,'full');if(s.zone===1){measure(s,'s4');measure(s,'s6');measure(s,'s5');}if(s.zone===2){measure(s,'up');measure(s,'down');}if(s.zone===3){measure(s,'angleA');measure(s,'angleB');}if(s.zone===4){measure(s,'old');measure(s,'new');}if(s.zone===5)measure(s,'lights');if(s.zone===6){measure(s,'loopA');measure(s,'loopB');}C.adopt(s);measure(s,'full');if(s.zone===7)C.readNote(s);assert.ok(C.locate(s,z.answer).ok);C.readNote(s);assert.ok(C.judge(s,'K').ok);assert.ok(C.next(s).ok);}
test('측정은 기준점과 배터리를 요구한다',()=>{const s=C.fresh();assert.match(C.observe(s).error,/설치/);C.install(s);s.battery=0;assert.match(C.observe(s).error,/배터리/);});
test('지상 완료 후 14:32 지하 진입, 기준점 회수',()=>{const s=ground();assert.equal(s.zone,1);assert.equal(s.minutes,872);assert.equal(s.points,4);assert.equal(s.installed,false);});
test('같은 복도 P 41.200 / K 24.600',()=>{const s=ground();C.install(s);const p=measure(s,'full');C.adopt(s);const k=measure(s,'full');assert.equal(p.value,41.2);assert.equal(k.value,24.6);assert.equal(p.suspect,false);});
test('표준척은 모든 기준에서 5.000',()=>{const s=ground();C.install(s);assert.equal(measure(s,'standard').value,5);C.adopt(s);assert.equal(measure(s,'standard').value,5);});
test('이분 탐색으로 구간 6의 16.6m를 찾는다',()=>{const s=ground();C.install(s);measure(s,'full');assert.equal(measure(s,'s4').value,12.3);assert.equal(measure(s,'s6').value,35.05);assert.equal(measure(s,'s5').value,15.375);C.adopt(s);measure(s,'full');assert.ok(C.locate(s,'6').ok);assert.equal(s.located['U-1'],1);});
test('추측만으로 U 공간을 찾을 수 없다',()=>{const s=ground();C.install(s);measure(s,'full');assert.equal(C.locate(s,'6').ok,false);assert.equal(Object.keys(s.located).length,0);});
test('K 측정은 불안정도를 올리지 않는다',()=>{const s=ground();C.adopt(s);const d=s.drift;measure(s,'full');assert.equal(s.drift,d);});
test('재측정 원본은 변하지 않고 새 기록이 추가된다',()=>{const s=ground();s.zone=2;C.install(s);const a=measure(s,'full');s.drift=70;const b=measure(s,'full');assert.equal(a.value,9.8);assert.equal(b.value,9.84);assert.notEqual(a.id,b.id);});
test('없는 K 측정으로 확정할 수 없다',()=>{const s=ground();C.install(s);measure(s,'full');assert.ok(C.judge(s,'K').error);assert.ok(C.judge(s,'P').ok);assert.equal(s.judgments[1].error,16.6);});
test('충전·힌트는 시간을 소모하고 도움 이력이 남는다',()=>{const s=ground();let m=s.minutes;C.recharge(s);assert.equal(s.minutes,m+25);m=s.minutes;C.hint(s);assert.equal(s.minutes,m+10);assert.equal(s.help[1],1);});
test('구역 재방문은 기록과 위치를 보존한다',()=>{const s=ground();solveZone(s);const n=s.records.length;assert.ok(C.revisit(s,1).ok);assert.equal(s.zone,1);assert.equal(s.records.length,n);assert.equal(s.located['U-1'],1);assert.ok(C.revisit(s,8).error);});
test('8개 구역, 여섯 U 공간, 전체 루프의 진 엔딩까지 도달 가능',()=>{const s=ground();while(s.zone<8)solveZone(s);assert.equal(s.zone,8);assert.equal(Object.keys(s.located).length,6);assert.equal(s.minutes,1650);C.recharge(s);for(const id of ROUTE)assert.ok(C.traverse(s,id).ok);assert.equal(s.closed,true);assert.ok(C.sign(s,'D','시험 측량자').ok);assert.equal(s.ending,'D');assert.ok(s.minutes<1740);});
test('순서가 틀리면 폐합하지 않는다',()=>{const s=ground();while(s.zone<8)solveZone(s);C.recharge(s);for(const id of [...ROUTE].reverse())C.traverse(s,id);assert.equal(s.closed,false);assert.ok(C.sign(s,'D','시험').error);});
test('U 위치를 빠뜨린 채 진 엔딩 서명 불가',()=>{const s=C.fresh();s.zone=8;s.closed=true;assert.ok(C.sign(s,'D','시험').error);});
test('A/C/E 결말은 어떤 상태에서도 서명할 수 있다',()=>{for(const id of ['A','C','E']){const s=C.fresh();assert.ok(C.sign(s,id,'시험 측량자').ok);assert.equal(s.ending,id);assert.ok(ENDINGS[id].text);}});
test('B 전체 서명은 모든 구역을 판정해야 열린다',()=>{const s=C.fresh();assert.match(C.sign(s,'B','시험').error,/판정하지 않은/);const t=ground();while(t.zone<8)solveZone(t);assert.equal(C.summary(t).unjudged,0);assert.equal(C.summary(t).confirmed,7);assert.ok(C.sign(t,'B','시험').ok);assert.equal(t.ending,'B');});
test('불안정도 80 이상이면 전체 서명이 막힌다',()=>{const s=ground();while(s.zone<8)solveZone(s);s.drift=80;assert.match(C.signOptions(s).B,/미확정/);assert.ok(C.sign(s,'B','시험').error);assert.ok(C.sign(s,'A','시험').ok);});
test('서명 요약은 확정·보류·오판·도움을 센다',()=>{const s=ground();s.judgments[1]={status:'확정'};s.judgments[2]={status:'보류'};s.flags.wrong3=true;s.help[4]=2;const m=C.summary(s);assert.deepEqual([m.confirmed,m.held,m.unjudged,m.wrong,m.helped],[1,1,5,1,1]);});
test('마감 이후에도 서명 선택이 남는다',()=>{const s=ground();s.minutes=1739;C.spend(s,3);assert.equal(s.flags.deadline,true);assert.equal(s.minutes,1740);assert.ok(C.observe(s).error);assert.ok(C.sign(s,'A','시험').ok);});
test('빈 서명과 손상된 저장 데이터 거부',()=>{assert.ok(C.sign(C.fresh(),'A',' ').error);assert.equal(C.validateSave({zone:900}),null);assert.equal(C.validateSave({...C.fresh(),zone:10}),null);assert.deepEqual(C.validateSave(JSON.parse(JSON.stringify(C.fresh()))),C.fresh());});
