'use strict';
const crypto = require('node:crypto');

class ApiError extends Error {
  constructor(status, code, message, upstreamCode) { super(message); this.status = status; this.code = code; this.upstreamCode = upstreamCode; }
}
const fail = (status, code, message) => { throw new ApiError(status, code, message); };
const integer = (value, min, max) => Number.isSafeInteger(value) && value >= min && value <= max;
// Unix seconds through 2100 are enough for this profile schema. A caller's
// clock may be wrong, but must never pin a record arbitrarily into the future.
const MAX_SAVED_AT = 4102444800;
function stable(value) {
  if (Array.isArray(value)) return '[' + value.map(stable).join(',') + ']';
  if (value && typeof value === 'object') return '{' + Object.keys(value).sort().map(k => JSON.stringify(k) + ':' + stable(value[k])).join(',') + '}';
  return JSON.stringify(value);
}
function validateProfile(p) {
  if (!p || Array.isArray(p) || typeof p !== 'object' || p.schema !== 1) fail(400, 'INVALID_PROFILE', '存档格式不受支持');
  const allowed = new Set(('schema level coins hearts hint freeze lens wins streak avatar sound music haptics drawIntroSeen firstContinueUsed reducedMenuMotion regenAt dailyAt lastRewardAdAt lastInterstitialAt boosterIntroSeen barrierIntroSeen shopDay shopPurchases adUsage completed resumeLevel resumeTime resumePieces bestStreak climbPendingFailure climb').split(' '));
  if (Object.keys(p).some(k => !allowed.has(k))) fail(400, 'INVALID_PROFILE', '存档含有未知字段');
  if (!integer(p.level, 1, 500) || !integer(p.coins, 0, 1000000000) || !integer(p.hearts, 0, 5)
      || !['hint','freeze','lens'].every(k => integer(p[k],0,1000000)) || !integer(p.wins,0,500)
      || !Array.isArray(p.completed) || p.completed.length !== 500 || p.completed.some(v => typeof v !== 'boolean')
      || Buffer.byteLength(JSON.stringify(p)) > 180000) fail(400, 'INVALID_PROFILE', '存档内容无效');
  return p;
}
function createService(env = process.env, fetcher = globalThis.fetch, now = () => Math.floor(Date.now()/1000), log = message => console.warn(message)) {
  const appId = env.WX_APPID || 'wx3a628e8b48216b63';
  function configured() {
    if (appId !== 'wx3a628e8b48216b63' || !env.WX_APPSECRET || !env.TCB_API_KEY
        || !/^[a-z0-9-]+$/.test(env.TCB_ENV_ID || '') || (env.SESSION_SIGNING_KEY || '').length < 32)
      fail(503, 'NOT_CONFIGURED', '云存档服务尚未配置完成');
  }
  function sign(data) { return crypto.createHmac('sha256', env.SESSION_SIGNING_KEY).update(data).digest('base64url'); }
  function issue(openid) {
    const payload = Buffer.from(JSON.stringify({ appId, openid, exp: now()+3600, nonce: crypto.randomUUID() })).toString('base64url');
    return payload + '.' + sign(payload);
  }
  function identify(token) {
    if (typeof token !== 'string' || token.length > 4096) fail(401,'SESSION_EXPIRED','请重新登录');
    const [data, signature, extra] = token.split('.');
    if (!data || !signature || extra) fail(401,'SESSION_EXPIRED','请重新登录');
    const expected = Buffer.from(sign(data)); const supplied = Buffer.from(signature);
    if (expected.length !== supplied.length || !crypto.timingSafeEqual(expected,supplied)) fail(401,'SESSION_EXPIRED','请重新登录');
    let value; try { value = JSON.parse(Buffer.from(data,'base64url').toString()); } catch { fail(401,'SESSION_EXPIRED','请重新登录'); }
    if (value.appId !== appId || !value.openid || !integer(value.exp,now()+1,now()+3600)) fail(401,'SESSION_EXPIRED','请重新登录');
    return value.openid;
  }
  async function request(url, options) {
    let result;
    try { result = await fetcher(url,{...options,signal:AbortSignal.timeout(7000)}); }
    catch { fail(503,'UPSTREAM_UNAVAILABLE','云存档暂时无法连接，请稍后重试'); }
    let data; try { data = await result.json(); } catch { fail(503,'UPSTREAM_UNAVAILABLE','云存档返回异常'); }
    return { status:result.status, ok:result.ok, data };
  }
  async function db(path, method='GET', body) {
    const r = await request('https://'+env.TCB_ENV_ID+'.api.tcloudbasegateway.com/v1/rdb/rest/'+path,{
      method, headers:{Authorization:'Bearer '+env.TCB_API_KEY,'Content-Type':'application/json',Prefer:'return=representation'},
      ...(body === undefined ? {} : {body:JSON.stringify(body)})
    });
    if (!r.ok && r.status !== 409) fail(503,'DATABASE_UNAVAILABLE','存档服务暂不可用，请检查服务端配置');
    return r;
  }
  const filter = openid => 'app_id=eq.'+encodeURIComponent(appId)+'&openid=eq.'+encodeURIComponent(openid);
  const record = row => {
    if (!row) return {exists:false,profile:null,revision:0,lastOperationId:'',updatedAt:'',savedAt:0};
    // Keep the clock in JSON storage so existing tables need no migration. It
    // is protocol metadata, never a PlayerProfile field exposed to clients.
    const {__savedAt, ...profile} = row.profile || {};
    return {exists:true,profile,revision:Number(row.revision),lastOperationId:row.last_operation_id || '',updatedAt:row.updated_at || '',savedAt:integer(__savedAt,0,MAX_SAVED_AT)?__savedAt:0};
  };
  async function load(openid) {
    const r = await db('game_saves?'+filter(openid)+'&select=profile,revision,last_operation_id,updated_at&limit=1');
    if (!r.ok || !Array.isArray(r.data)) fail(503,'DATABASE_UNAVAILABLE','无法读取云存档');
    return record(r.data[0]);
  }
  async function run(body) {
    configured();
    if (!body || Array.isArray(body) || typeof body !== 'object') fail(400,'INVALID_REQUEST','请求格式错误');
    if (body.action === 'login') {
      if (typeof body.code !== 'string' || body.code.length < 3 || body.code.length > 256) fail(400,'INVALID_CODE','登录凭证无效');
      const url = new URL('https://api.weixin.qq.com/sns/jscode2session');
      url.search = new URLSearchParams({appid:appId,secret:env.WX_APPSECRET,js_code:body.code,grant_type:'authorization_code'}).toString();
      const r = await request(url.toString(),{});
      if (!r.ok || !r.data || r.data.errcode || typeof r.data.openid !== 'string' || !/^[a-zA-Z0-9_-]{1,128}$/.test(r.data.openid)) {
        const raw = r.data && r.data.errcode;
        const upstreamCode = typeof raw === 'number' && Number.isSafeInteger(raw) && raw !== 0 ? raw : undefined;
        // Only a numeric platform error is recorded. Never log the login URL,
        // code, AppSecret, session_key, openid or the upstream errmsg payload.
        log('[INKOMINO_CLOUD_LOGIN] stage=code2session status='+r.status+' upstreamCode='+(upstreamCode ?? 'unknown'));
        throw new ApiError(401,'LOGIN_FAILED','微信登录校验未通过'+(upstreamCode === undefined ? '' : '（错误码 '+upstreamCode+'）'),upstreamCode);
      }
      const openid=r.data.openid;
      return {ok:true,token:issue(openid),identity:crypto.createHash('sha256').update(appId+':'+openid).digest('hex'),...(await load(openid))};
    }
    const openid=identify(body.token);
    if (body.action === 'load') return {ok:true,...(await load(openid))};
    if (body.action !== 'save') fail(400,'INVALID_ACTION','不支持的操作');
    const profile=validateProfile(body.profile);
    if (!integer(body.revision,0,1000000000) || !/^[0-9a-f-]{36}$/.test(body.operationId || '')) fail(400,'INVALID_REQUEST','存档版本无效');
    if (body.savedAt !== undefined && !integer(body.savedAt,0,MAX_SAVED_AT)) fail(400,'INVALID_REQUEST','存档时间无效');
    const receivedAt=now(),savedAt=body.savedAt===undefined?receivedAt:Math.min(body.savedAt,receivedAt);
    const values={profile:{...profile,__savedAt:savedAt},last_operation_id:body.operationId,revision:body.revision+1,updated_at:new Date(receivedAt*1000).toISOString()};
    const r = body.revision===0
      ? await db('game_saves','POST',{...values,app_id:appId,openid})
      : await db('game_saves?'+filter(openid)+'&revision=eq.'+body.revision,'PATCH',values);
    if (r.ok && Array.isArray(r.data) && r.data.length===1) return {ok:true,...record(r.data[0])};
    const current=await load(openid);
    // A response may be lost after a committed write. Replaying that same write
    // acknowledges it without incrementing the version or granting rewards again.
    if (current.exists && current.lastOperationId===body.operationId && stable(current.profile)===stable(profile)) return {ok:true,...current};
    return {ok:false,code:'REVISION_CONFLICT',message:'云端记录已更新，正在自动同步最新存档',...current};
  }
  async function handle(event) {
    try {
      const method=event.httpMethod || event.requestContext?.http?.method;
      if (method !== 'POST') fail(405,'METHOD_NOT_ALLOWED','请使用 POST 请求');
      let raw=event.body;
      if (event.isBase64Encoded && typeof raw==='string') raw=Buffer.from(raw,'base64').toString();
      if (typeof raw==='string') {
        if (Buffer.byteLength(raw)>200000) fail(413,'REQUEST_TOO_LARGE','存档数据过大');
        try { raw=JSON.parse(raw); } catch { fail(400,'INVALID_JSON','请求格式错误'); }
      }
      if (raw && Buffer.byteLength(JSON.stringify(raw))>200000) fail(413,'REQUEST_TOO_LARGE','存档数据过大');
      const value=await run(raw);
      return response(value.ok?200:409,{...value,serverTime:now()});
    } catch(error) {
      // Do not return upstream payloads, login URLs, headers or environment variables.
      return response(error instanceof ApiError?error.status:500,{ok:false,code:error instanceof ApiError?error.code:'INTERNAL_ERROR',message:error instanceof ApiError?error.message:'云存档暂不可用',...(error instanceof ApiError && Number.isSafeInteger(error.upstreamCode) ? {upstreamCode:error.upstreamCode} : {})});
    }
  }
  return {handle};
}
function response(statusCode,value) {
  return {statusCode,headers:{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store'},isBase64Encoded:false,body:JSON.stringify(value)};
}
let service;
exports.main=event => (service ||= createService()).handle(event);
exports.createService=createService;
