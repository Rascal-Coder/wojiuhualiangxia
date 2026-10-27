'use strict';
const fs = require('node:fs'), vm = require('node:vm'), assert = require('node:assert/strict');
const source = fs.readFileSync('Assets/Editor/WeChatFriendsOpenData.js.txt', 'utf8');
const fontSource = fs.readFileSync('Assets/Editor/WeChatFriendsFont.js.txt', 'utf8');
const fontMetrics = require('../Assets/Editor/FriendsFonts/friends-fonts.js');
const KEY = 'inkomino_completed_v1';
function friend(name = '真实好友', completed = 12, openid = name) {
  return {openid, nickname: name, KVDataList: [{key: KEY, value: String(completed)}]};
}
function roster(count) {
  return Array.from({length: count}, (_, index) => friend('好友' + (index + 1), 100 - index));
}
// Native callbacks finish independently. A fake clock controls cache expiry,
// refresh throttling, request timeouts and score retries without real waits.
function setup(options = {}) {
  let now = 100000, message, nextId = 0, draws = 0, shapes = 0, seen = [];
  let storedScore = options.storedScore ?? 12;
  const reads = [], writes = [], logs = [], images = [], artDraws = [], timers = new Map(), frames = new Map();
  const ctx = new Proxy({
    clearRect() { draws++; seen = []; shapes = 0; },
    fillText() {},
    measureText(value) { return {width: Array.from(String(value)).length * 25}; },
    fillRect() { shapes++; }, fill() { shapes++; }, stroke() { shapes++; },
    drawImage(image, ...args) {
      assert(images.includes(image), 'open-data drawImage must receive wx.createImage, never another Canvas');
      shapes++; if (String(image.src).startsWith('open-data/art/')) artDraws.push({image, args});
    },
    createLinearGradient() { return {addColorStop() {}}; },
    createRadialGradient() { return {addColorStop() {}}; }
  }, {get(target, key) { return key in target ? target[key] : () => {}; }});
  const canvas = {width: 960, height: 750, getContext() { return ctx; }};
  const wx = {
    getSharedCanvas: () => canvas, onMessage: fn => { message = fn; },
    createImage() { const image = {}; images.push(image); return image; },
    createCanvas() { throw new Error('ranking text cannot depend on Canvas-to-Canvas drawing'); },
    getFriendCloudStorage(callbacks) {
      reads.push({callbacks, finished: false});
      if (options.throwRead) throw new Error(options.throwRead);
    },
    getUserCloudStorage(callbacks) { callbacks.success({KVDataList: [{key: KEY, value: String(storedScore)}]}); },
    setUserCloudStorage(callbacks) { writes.push({callbacks, value: Number(callbacks.KVDataList[0].value), finished: false}); }
  };
  if (options.missingApi) delete wx.getFriendCloudStorage;
  class ClockDate extends Date {
    constructor(...args) { super(...(args.length ? args : [now])); }
    static now() { return now; }
  }
  const fontModule = {exports: {}};
  vm.runInNewContext(fontSource, {
    module: fontModule, Intl,
    setTimeout(fn, delay = 0) { const id = ++nextId; timers.set(id, {fn, at: now + delay}); return id; },
    clearTimeout(id) { timers.delete(id); },
    require(id) { assert.equal(id, './fonts/friends-fonts.js'); return fontMetrics; },
    console: {warn(...values) { logs.push(values.join(' ')); }}
  });
  vm.runInNewContext(source, {
    wx, Date: ClockDate, performance: {now: () => now},
    require(id) {
      if (id === './art') {
        const artModule = {exports: {}};
        vm.runInNewContext(fs.readFileSync('Assets/Editor/WeChatFriendsArt.js.txt', 'utf8'), {
          module: artModule,
          require(path) { assert.equal(path, './art/friends-art.js'); return require('../Assets/Editor/FriendsArt/friends-art.js'); },
          console: {warn(...values) { logs.push(values.join(' ')); }}
        });
        return artModule.exports;
      }
      assert.equal(id, './font');
      return (...args) => {
        const actual = fontModule.exports(...args);
        return {...actual, paint(...values) {
          // Text now consists of bitmap glyph drawImage calls. Capture one
          // logical line at its real font API, while exercising real metrics,
          // image-only bitmap loading and fallback rendering underneath.
          const face = fontMetrics.roles[values[6] || 'body'];
          const tint = face.tints && face.tints[values[4]];
          if (tint) for (const char of Array.from(String(values[0]))) {
            const code = String(char.codePointAt(0));
            if (face.glyphs[code]) assert(tint.glyphs[code], 'actual colored ranking label lacks tint coverage: ' + values[0] + ' / ' + char);
          }
          seen.push(String(values[0])); return actual.paint(...values);
        }};
      };
    },
    console: {log(...values) { logs.push(values.join(' ')); }, warn(...values) { logs.push(values.join(' ')); }},
    setTimeout(fn, delay = 0) { const id = ++nextId; timers.set(id, {fn, at: now + delay}); return id; },
    clearTimeout(id) { timers.delete(id); },
    requestAnimationFrame(fn) { const id = ++nextId; frames.set(id, fn); return id; }, cancelAnimationFrame(id) { frames.delete(id); }
  });
  function advance(ms) {
    const end = now + ms;
    for (let guard = 0; guard < 100; guard++) {
      const next = [...timers].filter(([, timer]) => timer.at <= end).sort((a, b) => a[1].at - b[1].at)[0];
      if (!next) { now = end; return; }
      now = next[1].at; timers.delete(next[0]); next[1].fn();
    }
    throw new Error('Timer loop did not settle');
  }
  function settleRead(kind, result, index = reads.findIndex(read => !read.finished)) {
    assert(index >= 0 && reads[index], 'a friend request must be pending');
    reads[index].finished = true; reads[index].callbacks[kind](result);
  }
  return {
    canvas, send: payload => message(payload), show() { message({type: 'friends:show'}); },
    hide() { message({type: 'friends:hide'}); }, refresh() { message({type: 'friends:refresh'}); },
    texts: () => seen, logs: () => logs, fetches: () => reads.length, draws: () => draws, shapes: () => shapes, advance,
    artDraws: () => artDraws,
    artLoads: () => images.filter(image => String(image.src).startsWith('open-data/art/')).length,
    finishArt() { for (const image of [...images]) if (String(image.src).startsWith('open-data/art/') && image.onload) image.onload(); },
    failArt() { for (const image of [...images]) if (String(image.src).startsWith('open-data/art/') && image.onerror) image.onerror(); },
    finishFonts() {
      for (const image of [...images]) if (String(image.src).startsWith('open-data/fonts/') && image.onload) {
        image.width = image.height = 2048; image.onload();
      }
    },
    frame() { const batch = [...frames.values()]; frames.clear(); batch.forEach(fn => fn(now)); },
    succeed(data = [friend()], index) { settleRead('success', {data}, index); }, fail(error, index) { settleRead('fail', error, index); },
    finishWrite() {
      const write = writes.find(item => !item.finished); assert(write, 'a score write must be pending');
      write.finished = true; storedScore = write.value; write.callbacks.success({});
    }
  };
}
const cases = [];
function test(name, run) { cases.push({name, run}); }
function hasScore(game, value) {
  return game.texts().includes(value + ' 关') || game.texts().includes(value + '关') ||
    (game.texts().includes(String(value)) && game.texts().some(text => text === '关' || text === '通关'));
}
function hasText(game, pattern) { return game.texts().some(text => pattern.test(text)); }

test('initial loading uses placeholder rows without invented friends', () => {
  const game = setup(); game.show();
  assert(hasText(game, /加载|读取|正在/), 'the first read needs a concise loading status');
  assert(game.shapes() >= 5, 'the first read should paint multiple row silhouettes');
  assert(!game.texts().includes('真实好友')); game.succeed();
  assert(game.texts().includes('真实好友')); assert(hasScore(game, 12));
});
test('three rapid opens and three rapid refreshes coalesce into one request', () => {
  const game = setup();
  for (let i = 0; i < 3; i++) game.show(); for (let i = 0; i < 3; i++) game.refresh();
  assert.equal(game.fetches(), 1); game.succeed();
  assert.equal(game.fetches(), 1, 'in-flight refresh taps must not queue duplicate reads'); assert(game.texts().includes('真实好友'));
});
test('warm successful cache is reused for 30 seconds, then refreshed behind its rows', () => {
  const game = setup(); game.show(); game.succeed(); game.hide(); game.advance(29999); game.show();
  assert.equal(game.fetches(), 1); assert(game.texts().includes('真实好友'));
  game.hide(); game.advance(2); game.show();
  assert.equal(game.fetches(), 2); assert(game.texts().includes('真实好友'), 'expiry must not blank cached rows');
  game.succeed([friend('刷新好友', 13)]); assert(game.texts().includes('刷新好友')); assert(hasScore(game, 13));
});
test('successful empty results also receive the 30-second cache', () => {
  const game = setup(); game.show(); game.succeed([]); assert(hasText(game, /还没有|暂无/));
  game.hide(); game.advance(29999); game.show(); assert.equal(game.fetches(), 1);
  game.hide(); game.advance(2); game.show(); assert.equal(game.fetches(), 2);
});
test('manual refresh bypasses warm cache but keeps names and throttles repeated taps', () => {
  const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh();
  assert.equal(game.fetches(), 2); assert(game.texts().includes('真实好友')); game.succeed([friend('更新好友', 14)]);
  for (let i = 0; i < 3; i++) game.refresh();
  assert.equal(game.fetches(), 2, 'refresh completion must not remove the rapid-tap cooldown');
  game.advance(1501); game.refresh(); assert.equal(game.fetches(), 3);
});
test('hidden reads populate cache without painting or reopening the screen', () => {
  const game = setup(); game.show(); game.hide(); const draws = game.draws();
  game.succeed(); game.frame(); assert.equal(game.draws(), draws);
  game.send({type: 'WXShow'}); assert.equal(game.draws(), draws, 'native show must not reopen an explicitly hidden ranking');
  game.show(); assert.equal(game.fetches(), 1); assert(game.texts().includes('真实好友'));
});
test('font decoding while hidden cannot repaint or reopen the ranking', () => {
  const game = setup(); game.send({type: 'friends:prepare'}); game.show(); game.succeed(); game.hide();
  const hidden = game.draws(); game.finishFonts(); game.frame(); assert.equal(game.draws(), hidden);
  game.show(); assert(game.texts().includes('真实好友')); assert.equal(game.fetches(), 1);
});
test('art preloading shares one atlas and does not trigger friend data reads', () => {
  const game = setup(); game.send({type: 'friends:prepare'}); game.send({type: 'friends:prepare'});
  assert.equal(game.artLoads(), 1); assert.equal(game.fetches(), 0); assert.equal(game.artDraws().length, 0);
  game.show(); game.succeed(roster(8)); game.finishArt(); assert.equal(game.artLoads(), 1);
  assert(game.artDraws().length >= 3, 'top-three badges must be drawn from the real shared atlas');
  const firstPage = game.artDraws().length; game.send({type: 'friends:page', delta: 1});
  assert.equal(game.artDraws().length, firstPage, 'ranks after three use game-font digits without repeated badges');
  game.advance(1501); game.refresh(); assert.equal(game.fetches(), 2); assert.equal(game.artLoads(), 1);
});
test('hidden art decoding cannot repaint or reopen a cached ranking', () => {
  const game = setup(); game.show(); game.succeed(); game.hide();
  const hidden = game.draws(), reads = game.fetches(); game.finishArt(); game.frame();
  assert.equal(game.draws(), hidden); assert.equal(game.fetches(), reads);
  game.show(); assert(game.texts().includes('真实好友')); assert.equal(game.fetches(), reads);
});
test('art failure preserves readable ranks and normal pagination without retry loops', () => {
  const game = setup(); game.show(); game.succeed(roster(7)); game.failArt();
  for (const rank of ['1', '2', '3']) assert(game.texts().includes(rank), 'rank ' + rank + ' remains in the game font');
  assert.equal(game.artDraws().length, 0); assert.equal(game.artLoads(), 1);
  game.send({type: 'friends:page', delta: 1}); assert(game.texts().includes('好友6'));
  game.send({type: 'friends:page', delta: -1}); assert(game.texts().includes('好友1'));
  assert.equal(game.fetches(), 1); assert.equal(game.artLoads(), 1);
});
test('reopening while a read is pending does not start another request', () => {
  const game = setup(); game.show(); game.hide(); game.show(); assert.equal(game.fetches(), 1);
  game.succeed(); assert(game.texts().includes('真实好友'));
});
test('SDK canvas resize, detach and frame refresh preserve cached data', () => {
  const game = setup(); game.send(JSON.stringify({type: 'friends:show'})); game.succeed();
  game.send({type: 'WXRender', width: 450, height: 600}); assert.equal(game.canvas.width, 450); assert.equal(game.canvas.height, 600);
  const drawn = game.draws(); game.frame(); assert(game.draws() > drawn, 'visible frames keep the shared texture current');
  game.send({type: 'WXDestroy'}); const hidden = game.draws(); game.frame(); assert.equal(game.draws(), hidden);
  game.send({type: 'WXRender', width: 960, height: 750}); assert(game.texts().includes('真实好友')); assert.equal(game.fetches(), 1);
});
test('foreground resume reuses warm cache and refreshes expired data without blanking', () => {
  const game = setup(); game.show(); game.succeed();
  for (let i = 0; i < 3; i++) game.send({type: 'friends:resume'});
  assert.equal(game.fetches(), 1); assert(game.texts().includes('真实好友'));
  game.advance(30001); game.send({type: 'friends:resume'});
  assert.equal(game.fetches(), 2); assert(game.texts().includes('真实好友'));
  game.send({type: 'friends:resume'}); assert.equal(game.fetches(), 2, 'focus events must share a pending read');
  game.succeed(); game.hide(); const draws = game.draws();
  game.advance(30001); game.send({type: 'friends:resume'});
  assert.equal(game.fetches(), 2); assert.equal(game.draws(), draws, 'resume must not reopen a hidden ranking');
});
test('native surface events coalesce one frame repaint and static rows then settle', () => {
  const game = setup(); game.show(); game.succeed(); game.frame();
  const settled = game.draws();
  for (let i = 0; i < 60; i++) game.frame();
  game.advance(1000); assert.equal(game.draws(), settled, 'static rows must not redraw every frame or timer tick');
  for (let i = 0; i < 3; i++) game.send({type: 'WXRender', width: 960, height: 750});
  const synchronous = game.draws(); assert(synchronous > settled, 'native surface changes must paint synchronously');
  game.frame(); assert.equal(game.draws(), synchronous + 1, 'native event bursts need only one next-frame repaint');
  const painted = game.draws();
  for (let i = 0; i < 60; i++) game.frame();
  game.advance(1000); assert.equal(game.draws(), painted);
});
test('loading animates on a bounded timer and stops on success or hide', () => {
  const game = setup(); game.show(); game.frame(); const start = game.draws();
  game.advance(179); assert.equal(game.draws(), start);
  game.advance(1); assert.equal(game.draws(), start + 1);
  game.advance(180); assert.equal(game.draws(), start + 2);
  game.succeed(); const ready = game.draws(); game.advance(1000); assert.equal(game.draws(), ready);
  game.advance(1501); game.refresh(); const refreshing = game.draws();
  game.advance(180); assert.equal(game.draws(), refreshing + 1); assert(game.texts().includes('真实好友'));
  game.hide(); const hidden = game.draws(); game.advance(1000); game.frame(); assert.equal(game.draws(), hidden);
});
test('refresh preserves the selected page and clamps it when results shrink', () => {
  const game = setup(); game.show(); game.succeed(roster(13)); game.send({type: 'friends:page', delta: 1});
  assert(game.texts().includes('好友6')); assert(!game.texts().includes('好友1'));
  game.advance(1501); game.refresh(); assert(game.texts().includes('好友6'));
  game.succeed(roster(11)); assert(game.texts().includes('好友6'), 'refresh must not reset page two');
  game.advance(1501); game.refresh(); game.succeed(roster(3)); assert(game.texts().includes('好友1')); assert(!game.texts().includes('好友6'));
});
test('network failure retains cached rows with a concise refresh notice', () => {
  const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh(); game.fail({errMsg: 'network failure', errno: 20001});
  assert(game.texts().includes('真实好友')); assert(hasText(game, /更新未完成.*点击刷新/));
  assert(game.logs().some(line => line.includes('[INK_FRIENDS] fetch failed:') && line.includes('WX_20001')));
});
test('permission failure purges previous rows and never points to the removed button', () => {
  const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh(); game.fail({errMsg: 'user deny', errCode: 20002});
  assert(!game.texts().includes('真实好友')); assert(hasText(game, /权限|授权/));
  assert(!game.texts().some(text => text.includes('排行权限设置')));
  game.hide(); game.show(); assert(!game.texts().includes('真实好友'), 'revoked friend data must not return from cache');
});
test('numeric WX_1026 is classified as privacy/configuration and purges old rows', () => {
  const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh(); game.fail({errMsg: 'getFriendCloudStorage:fail', errno: 1026});
  assert(!game.texts().includes('真实好友'));
  assert(hasText(game, /隐私|配置|功能准备/), '1026 must explain a configuration/privacy issue');
  assert(!hasText(game, /检查网络/)); assert(hasText(game, /WX_1026/));
});
test('score committed during pending list read schedules exactly one follow-up', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.finishWrite();
  assert.equal(game.fetches(), 1, 'the initial read stays in flight'); game.succeed([friend('真实好友', 12)]); assert.equal(game.fetches(), 2);
  game.succeed([friend('真实好友', 13)]); assert(hasScore(game, 13)); assert.equal(game.fetches(), 2);
});
test('multiple committed scores during a pending read share one follow-up', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.send({type: 'score:update', score: 15});
  game.finishWrite(); game.finishWrite(); assert.equal(game.fetches(), 1); game.succeed([friend('真实好友', 12)]); assert.equal(game.fetches(), 2);
  game.succeed([friend('真实好友', 15)]); assert(hasScore(game, 15)); assert.equal(game.fetches(), 2);
});
test('score committed during a failed read receives one bounded follow-up', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.finishWrite();
  game.fail({errMsg: 'network failure', errno: 20001}); assert.equal(game.fetches(), 2);
  game.fail({errMsg: 'network failure', errno: 20001});
  assert.equal(game.fetches(), 2, 'the follow-up must not become an automatic retry loop');
});
test('score committed during a timed-out read gets a fresh read and ignores the late result', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.finishWrite();
  game.advance(12000); assert.equal(game.fetches(), 2);
  game.succeed([friend('过期响应', 12)], 0); assert(!game.texts().includes('过期响应'));
  game.succeed([friend('真实好友', 13)], 1); assert(hasScore(game, 13)); assert.equal(game.fetches(), 2);
});
test('dirty privacy and permission failures purge data without automatic follow-up', () => {
  for (const error of [{errMsg: 'getFriendCloudStorage:fail', errno: 1026}, {errMsg: 'user deny', errCode: 20002}]) {
    const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh();
    game.send({type: 'score:update', score: 13}); game.finishWrite(); game.fail(error);
    assert.equal(game.fetches(), 2, 'privacy blocks must not cause automatic reads');
    assert(!game.texts().includes('真实好友')); game.advance(12000); assert.equal(game.fetches(), 2);
  }
});
test('dirty failed read completed while hidden waits for reopening to retry', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.finishWrite(); game.hide();
  const draws = game.draws(); game.fail({errMsg: 'network failure', errno: 20001});
  assert.equal(game.fetches(), 1); assert.equal(game.draws(), draws);
  game.show(); assert.equal(game.fetches(), 2); game.succeed([friend('真实好友', 13)]); assert(hasScore(game, 13));
});
test('score completion refreshes a visible settled list without waiting for TTL', () => {
  const game = setup(); game.show(); game.succeed(); game.send({type: 'score:update', score: 13}); game.finishWrite();
  assert.equal(game.fetches(), 2); assert(game.texts().includes('真实好友')); game.succeed([friend('真实好友', 13)]); assert(hasScore(game, 13));
});
test('score completion while hidden marks cache dirty without fetching until reopened', () => {
  const game = setup(); game.show(); game.succeed(); game.send({type: 'score:update', score: 13}); game.hide();
  const draws = game.draws(); game.finishWrite(); assert.equal(game.fetches(), 1); assert.equal(game.draws(), draws);
  game.show(); assert.equal(game.fetches(), 2); game.succeed([friend('真实好友', 13)]); assert(hasScore(game, 13));
});
test('dirty read finishing while hidden waits for reopening before its follow-up', () => {
  const game = setup(); game.show(); game.send({type: 'score:update', score: 13}); game.finishWrite(); game.hide();
  const draws = game.draws(); game.succeed(); assert.equal(game.fetches(), 1); assert.equal(game.draws(), draws);
  game.show(); assert.equal(game.fetches(), 2); game.succeed([friend('真实好友', 13)]); assert(hasScore(game, 13));
});
test('12-second timeout ignores a late result and preserves cached rows', () => {
  const game = setup(); game.show(); game.succeed(); game.advance(1501); game.refresh(); game.advance(11999);
  assert(!game.logs().some(line => line.includes('code=TIMEOUT'))); game.advance(1);
  assert(game.logs().some(line => line.includes('code=TIMEOUT'))); assert(game.texts().includes('真实好友')); assert(hasText(game, /更新未完成.*点击刷新/));
  game.succeed([friend('过期响应', 99)]); assert(!game.texts().includes('过期响应'));
});
test('first-load timeout and synchronous exception still expose safe diagnostic codes', () => {
  for (const [options, code] of [[{}, 'TIMEOUT'], [{throwRead: 'native call failed'}, 'CALL_ERROR'], [{missingApi: true}, 'API_UNAVAILABLE']]) {
    const game = setup(options); game.show(); if (code === 'TIMEOUT') game.advance(12000);
    assert(hasText(game, new RegExp(code)), code + ' should be visible');
    assert(game.logs().some(line => line.includes('[INK_FRIENDS] fetch failed:') && line.includes(code)));
  }
});
let failed = 0;
for (const {name, run} of cases) {
  try { run(); console.log('PASS ' + name); }
  catch (error) { failed++; console.error('FAIL ' + name + '\n  ' + error.stack); }
}
if (failed) process.exitCode = 1;
else console.log('PASS ' + cases.length + ' friend ranking lifecycle/render checks; rapid-open/refresh requests 6 -> 1');
