'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const {createService} = require('../function');
const env = {WX_APPID:'wx3a628e8b48216b63',WX_APPSECRET:'test-only-secret',TCB_ENV_ID:'test-env',TCB_API_KEY:'test-only-api-key',SESSION_SIGNING_KEY:'test-only-signing-key-at-least-32-chars'};
const profile = coins => ({schema:1,level:3,coins,hearts:5,hint:3,freeze:3,lens:3,wins:2,completed:Array.from({length:500},(_,i)=>i<2)});
const op = n => '00000000-0000-4000-8000-'+String(n).padStart(12,'0');
function fixture() {
  const rows=new Map();const calls=[];let clock=10000,lost=false;
  const result=(status,data)=>({status,ok:status>=200&&status<300,json:async()=>data});
  const fetcher=async(url,options)=>{
    const u=new URL(url);calls.push({url:u,options});
    if(u.hostname==='api.weixin.qq.com')return result(200,u.searchParams.get('js_code')==='bad-code'?{errcode:40029}:{openid:'player-'+u.searchParams.get('js_code'),session_key:'must-not-leak'});
    assert.equal(options.headers.Authorization,'Bearer '+env.TCB_API_KEY);
    const id=u.searchParams.get('openid')?.slice(3);const key=id||JSON.parse(options.body).openid;
    if(options.method==='GET')return result(200,rows.has(key)?[structuredClone(rows.get(key))]:[]);
    const value=JSON.parse(options.body);
    if(options.method==='POST'&&rows.has(key))return result(409,{code:'23505'});
    if(options.method==='PATCH'&&(!rows.has(key)||rows.get(key).revision!==Number(u.searchParams.get('revision').slice(3))))return result(200,[]);
    const row={...(rows.get(key)||{}),...value};rows.set(key,row);
    if(lost){lost=false;throw Error('response lost after commit');}
    return result(200,[structuredClone(row)]);
  };
  const service=createService(env,fetcher,()=>clock);
  const call=async body=>{const http=await service.handle({httpMethod:'POST',body:JSON.stringify(body)});return {status:http.statusCode,...JSON.parse(http.body)};};
  return {rows,calls,service,call,advance:seconds=>clock+=seconds,lose:()=>lost=true};
}
test('login uses verified WeChat identity and does not leak secrets',async()=>{
  const f=fixture();const r=await f.call({action:'login',code:'alice',openid:'victim'});
  assert.equal(r.ok,true);assert.equal(r.exists,false);assert.equal(r.revision,0);assert.equal(r.identity.length,64);
  for(const secret of ['must-not-leak',env.WX_APPSECRET,env.TCB_API_KEY])assert(!JSON.stringify(r).includes(secret));
});
test('create/load/save are isolated between two WeChat accounts',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'}),b=await f.call({action:'login',code:'bob'});
  const save=await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(115),openid:'player-bob'});
  assert.equal(save.revision,1);assert.equal(f.rows.get('player-alice').profile.coins,115);assert(!f.rows.has('player-bob'));
  assert.equal((await f.call({action:'load',token:b.token})).exists,false);
  assert.equal((await f.call({action:'load',token:a.token})).profile.coins,115);
});
test('two devices: stale revision cannot overwrite the newer balance',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(100)});
  const b=await f.call({action:'login',code:'alice'});assert.equal(b.revision,1);
  await f.call({action:'save',token:a.token,revision:1,operationId:op(2),profile:profile(50)});
  const conflict=await f.call({action:'save',token:b.token,revision:1,operationId:op(3),profile:profile(150)});
  assert.equal(conflict.status,409);assert.equal(conflict.code,'REVISION_CONFLICT');assert.equal(conflict.profile.coins,50);assert.equal(f.rows.get('player-alice').revision,2);
  const chosen=await f.call({action:'save',token:b.token,revision:conflict.revision,operationId:op(4),profile:profile(150)});
  assert.equal(chosen.revision,3);assert.equal(chosen.profile.coins,150);
});
test('retry after a lost committed response does not write or reward twice',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  const data={action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(120)};
  f.lose();assert.equal((await f.call(data)).status,503);
  const replay=await f.call(data);assert.equal(replay.ok,true);assert.equal(replay.revision,1);assert.equal(replay.profile.coins,120);
  assert.equal((await f.call({...data,profile:profile(1000)})).status,409);
});
test('same stale revision admits exactly one of two simultaneous device writes',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(100)});
  const results=await Promise.all([2,3].map(i=>f.call({action:'save',token:a.token,revision:1,operationId:op(i),profile:profile(i*100)})));
  assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);assert.equal(f.rows.get('player-alice').revision,2);
});
test('offline progress keeps its actual save time separately from the upload time',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  assert.equal(a.serverTime,10000);assert.equal(a.savedAt,0);
  f.advance(600);
  const saved=await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(125),savedAt:9500});
  assert.equal(saved.savedAt,9500);assert.equal(saved.serverTime,10600);
  assert.equal(saved.updatedAt,new Date(10600*1000).toISOString());
  assert.equal(f.rows.get('player-alice').profile.__savedAt,9500);
  for(const reply of [saved,await f.call({action:'load',token:a.token}),await f.call({action:'login',code:'alice'})]){
    assert.equal(reply.savedAt,9500);assert.equal(reply.serverTime,10600);
    assert(!Object.hasOwn(reply.profile,'__savedAt'));assert(!JSON.stringify(reply).includes('__savedAt'));
  }
});
test('legacy rows expose an unknown actual save time and legacy saves use receipt time',async()=>{
  const f=fixture();const oldProfile=profile(100);
  f.rows.set('player-alice',{profile:oldProfile,revision:3,last_operation_id:op(3),updated_at:new Date(9000*1000).toISOString()});
  const a=await f.call({action:'login',code:'alice'});
  assert.equal(a.savedAt,0);assert.equal(a.updatedAt,new Date(9000*1000).toISOString());assert.equal(a.serverTime,10000);
  assert.deepEqual(a.profile,oldProfile);assert(!Object.hasOwn(oldProfile,'__savedAt'),'reading a legacy row must not rewrite its profile');
  f.advance(20);
  const saved=await f.call({action:'save',token:a.token,revision:3,operationId:op(4),profile:profile(120)});
  assert.equal(saved.savedAt,10020);assert.equal(saved.serverTime,10020);assert.equal(saved.revision,4);
});
test('future save clocks are clamped and an explicit unknown zero clock is accepted',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  const saved=await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(130),savedAt:4102444800});
  assert.equal(saved.savedAt,10000);assert.equal(f.rows.get('player-alice').profile.__savedAt,10000);
  const unknown=await f.call({action:'save',token:a.token,revision:1,operationId:op(2),profile:profile(140),savedAt:0});
  assert.equal(unknown.savedAt,0);assert.equal(unknown.serverTime,10000);
});
test('invalid save clocks and injected internal profile metadata cannot modify storage',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  for(const savedAt of [-1,1.5,'10000',null,{},[],true,4102444801,Number.MAX_SAFE_INTEGER+1]){
    const r=await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(100),savedAt});
    assert.equal(r.status,400);assert.equal(r.code,'INVALID_REQUEST');
  }
  assert.equal((await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:{...profile(100),__savedAt:9500},savedAt:9500})).status,400);
  assert.equal(f.rows.size,0);assert(f.calls.every(c=>c.options.method!=='POST'&&c.options.method!=='PATCH'),'invalid clocks must be rejected before database writes');
});
test('storing and exposing timestamp metadata does not mutate caller-owned or database profiles',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});const original=profile(150),before=structuredClone(original);
  const http=await f.service.handle({httpMethod:'POST',body:{action:'save',token:a.token,revision:0,operationId:op(1),profile:original,savedAt:9300}});
  assert.equal(http.statusCode,200);assert.deepEqual(original,before);assert(!Object.hasOwn(original,'__savedAt'));
  const stored=f.rows.get('player-alice'),snapshot=structuredClone(stored);
  await f.call({action:'load',token:a.token});await f.call({action:'login',code:'alice'});
  assert.deepEqual(stored,snapshot);assert.equal(stored.profile.__savedAt,9300);
});
test('replaying a committed operation does not refresh its save clock, upload time or revision',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  const data={action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(160),savedAt:9400};
  f.lose();assert.equal((await f.call(data)).status,503);const first=structuredClone(f.rows.get('player-alice'));
  f.advance(120);
  const replay=await f.call({...data,savedAt:10120});
  assert.equal(replay.ok,true);assert.equal(replay.revision,1);assert.equal(replay.savedAt,9400);assert.equal(replay.serverTime,10120);
  assert.equal(replay.updatedAt,first.updated_at);assert.deepEqual(f.rows.get('player-alice'),first);
  const conflict=await f.call({...data,profile:profile(999),savedAt:10120});
  assert.equal(conflict.status,409);assert.equal(conflict.savedAt,9400);assert.equal(conflict.serverTime,10120);assert.deepEqual(f.rows.get('player-alice'),first);
});
test('a newer client timestamp cannot bypass compare-and-swap revision protection',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:profile(100),savedAt:9000});
  const b=await f.call({action:'login',code:'alice'});
  await f.call({action:'save',token:a.token,revision:1,operationId:op(2),profile:profile(50),savedAt:9500});
  f.advance(100);
  const conflict=await f.call({action:'save',token:b.token,revision:1,operationId:op(3),profile:profile(500),savedAt:10100});
  assert.equal(conflict.status,409);assert.equal(conflict.code,'REVISION_CONFLICT');assert.equal(conflict.profile.coins,50);
  assert.equal(conflict.savedAt,9500);assert.equal(conflict.serverTime,10100);assert.equal(f.rows.get('player-alice').revision,2);
  assert(!conflict.message.includes('选择'));assert(!Object.hasOwn(conflict.profile,'__savedAt'));
});
test('forged and expired sessions are rejected before database access',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});const n=f.calls.length;
  assert.equal((await f.call({action:'load',token:a.token+'x'})).status,401);assert.equal(f.calls.length,n);
  f.advance(3601);assert.equal((await f.call({action:'load',token:a.token})).status,401);
});
test('invalid profiles, missing session and unknown action cannot modify storage',async()=>{
  const f=fixture();const a=await f.call({action:'login',code:'alice'});
  for(const p of [{...profile(1),coins:-1},{...profile(1),completed:[]},{...profile(1),schema:2},{...profile(1),unknown:true}]){
    assert.equal((await f.call({action:'save',token:a.token,revision:0,operationId:op(1),profile:p})).status,400);
  }
  assert.equal((await f.call({action:'load'})).status,401);
  assert.equal((await f.call({action:'erase',token:a.token})).status,400);assert.equal(f.rows.size,0);
});
test('malformed / oversized / wrong-method requests and missing config fail safely',async()=>{
  const f=fixture();
  assert.equal((await f.service.handle({httpMethod:'GET'})).statusCode,405);
  assert.equal((await f.service.handle({httpMethod:'POST',body:'{'})).statusCode,400);
  assert.equal((await f.service.handle({httpMethod:'POST',body:'x'.repeat(200001)})).statusCode,413);
  assert.equal((await createService({}).handle({httpMethod:'POST',body:'{}'})).statusCode,503);
  assert.equal((await f.call({action:'login',code:'bad-code'})).status,401);
});

test('login rejection exposes only the numeric WeChat error, never credentials or raw upstream data',async()=>{
  for(const code of [40013,40125,40029,40163,45011,-1]){
    const logs=[];let calls=0;
    const service=createService(env,async()=>{calls++;return {status:200,ok:true,json:async()=>({errcode:code,errmsg:env.WX_APPSECRET+' must-not-leak',session_key:'must-not-leak',openid:'must-not-leak'})};},()=>10000,message=>logs.push(message));
    const http=await service.handle({httpMethod:'POST',body:JSON.stringify({action:'login',code:'private-login-code'})});
    const reply=JSON.parse(http.body);
    assert.equal(http.statusCode,401);assert.equal(reply.code,'LOGIN_FAILED');assert.equal(reply.upstreamCode,code);assert.equal(calls,1,'rejected login must not access the database');
    assert(logs[0].includes('stage=code2session')&&logs[0].includes('upstreamCode='+code));
    const output=JSON.stringify({reply,logs});
    for(const secret of [env.WX_APPSECRET,env.TCB_API_KEY,'private-login-code','must-not-leak'])assert(!output.includes(secret));
  }
});

test('malformed login responses fail without reflecting untrusted upstream fields',async()=>{
  for(const upstream of [null,{}, {errcode:'secret-value',errmsg:'must-not-leak'}, {errcode:0}]){
    const logs=[];
    const service=createService(env,async()=>({status:200,ok:true,json:async()=>upstream}),()=>10000,message=>logs.push(message));
    const http=await service.handle({httpMethod:'POST',body:JSON.stringify({action:'login',code:'test-code'})});
    assert.equal(http.statusCode,401);assert(!JSON.stringify({body:http.body,logs}).includes('secret-value'));assert(!http.body.includes('must-not-leak'));
  }
});
