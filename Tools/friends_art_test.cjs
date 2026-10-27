'use strict';
const fs = require('node:fs'), vm = require('node:vm'), assert = require('node:assert/strict');
const source = fs.readFileSync('Assets/Editor/WeChatFriendsArt.js.txt', 'utf8');
const atlas = require('../Assets/Editor/FriendsArt/friends-art.js');
function setup(options = {}) {
  const images = [], draws = [], warnings = []; let invalidations = 0, creates = 0;
  const ctx = {drawImage(...args) { draws.push(args); }};
  const wx = {createImage() {
    creates++;
    if (options.throwCreate) throw new Error('image allocation failed');
    const image = {}; images.push(image); return image;
  }};
  const module = {exports: {}};
  vm.runInNewContext(source, {
    module,
    require(path) { assert.equal(path, './art/friends-art.js'); return atlas; },
    console: {warn(value) { warnings.push(String(value)); }}
  });
  return {
    art: module.exports(wx, ctx, () => { invalidations++; }), images, draws, warnings,
    creates: () => creates, invalidations: () => invalidations,
    finish() { assert.equal(images.length, 1); images[0].onload(); },
    fail() { assert.equal(images.length, 1); images[0].onerror(); }
  };
}
const cases = [];
function test(name, run) { cases.push({name, run}); }
function near(a, b) { assert(Math.abs(a - b) < 1e-7, a + ' should equal ' + b); }
test('the four game stickers reference valid regions in the real PNG atlas', () => {
  assert.equal(atlas.file, 'friends-art.png');
  const data = fs.readFileSync('Assets/Editor/FriendsArt/' + atlas.file);
  assert.equal(data.subarray(1, 4).toString(), 'PNG');
  const width = data.readUInt32BE(16), height = data.readUInt32BE(20);
  for (const key of ['rank1', 'rank2', 'rank3', 'empty']) {
    const frame = atlas.frames[key]; assert(frame, key + ' must have generated artwork');
    assert(frame.x >= 0 && frame.y >= 0 && frame.w > 0 && frame.h > 0);
    assert(frame.x + frame.w <= width && frame.y + frame.h <= height, key + ' must remain inside the atlas');
    if (frame.digit) assert(frame.digit.length === 2 && frame.digit.every(value => value >= 0 && value <= 1));
  }
});
test('prepare and all sticker paints share one local image load', () => {
  const game = setup(); game.art.prepare(); game.art.prepare();
  for (const key of Object.keys(atlas.frames)) game.art.paint(key, 100, 80, 88, 120);
  assert.equal(game.creates(), 1); assert.equal(game.images[0].src, 'open-data/art/friends-art.png');
  assert.equal(game.draws.length, 0, 'pending artwork must not draw synthetic substitutes');
});
test('number anchors remain stable before and after artwork decoding', () => {
  const game = setup();
  const before = game.art.paint('rank1', 66, 180, 88, 120); assert(before); assert.equal(game.draws.length, 0);
  game.finish(); assert.equal(game.invalidations(), 1);
  const after = game.art.paint('rank1', 66, 180, 88, 120);
  near(before.x, after.x); near(before.y, after.y); assert.equal(game.draws.length, 1);
});
test('artwork uses the source crop and fits its destination without stretching', () => {
  const game = setup(); game.art.prepare(); game.finish();
  for (const [key, maxWidth, maxHeight] of [['rank1', 88, 120], ['rank2', 120, 45], ['rank3', 35, 80], ['empty', 340, 310]]) {
    const frame = atlas.frames[key], anchor = game.art.paint(key, 200, 170, maxWidth, maxHeight);
    const [image, sx, sy, sw, sh, dx, dy, dw, dh] = game.draws.at(-1);
    assert.equal(image, game.images[0]); assert.deepEqual([sx, sy, sw, sh], [frame.x, frame.y, frame.w, frame.h]);
    assert(dw <= maxWidth + 1e-7 && dh <= maxHeight + 1e-7); near(dw / sw, dh / sh);
    near(dx + dw / 2, 200); near(dy + dh / 2, 170);
    const digit = frame.digit || [.5, .5]; near(anchor.x, dx + digit[0] * dw); near(anchor.y, dy + digit[1] * dh);
  }
});
test('unknown stickers and invalid sizes neither load nor paint', () => {
  const game = setup();
  assert.equal(game.art.paint('missing', 0, 0, 100, 100), null);
  for (const [width, height] of [[0, 100], [100, 0], [-1, 100], [100, NaN]]) {
    assert.equal(game.art.paint('rank1', 0, 0, width, height), null);
  }
  assert.equal(game.creates(), 0); assert.equal(game.draws.length, 0);
});
test('decode failure leaves stable game-font anchors without replacement drawings or retry loops', () => {
  const game = setup(); const before = game.art.paint('rank2', 60, 90, 88, 120);
  game.fail(); const after = game.art.paint('rank2', 60, 90, 88, 120); game.art.prepare();
  near(before.x, after.x); near(before.y, after.y);
  assert.equal(game.draws.length, 0); assert.equal(game.creates(), 1); assert.equal(game.invalidations(), 1);
  assert.equal(game.warnings.length, 1); assert(!game.warnings[0].includes('真实好友'));
});
test('native image allocation failure remains nonfatal and is attempted only once', () => {
  const game = setup({throwCreate: true});
  assert.doesNotThrow(() => game.art.prepare());
  assert(game.art.paint('rank3', 60, 90, 88, 120)); game.art.prepare();
  assert.equal(game.creates(), 1); assert.equal(game.draws.length, 0); assert.equal(game.warnings.length, 1);
});
let failed = 0;
for (const {name, run} of cases) {
  try { run(); console.log('PASS ' + name); }
  catch (error) { failed++; console.error('FAIL ' + name + '\n  ' + error.stack); }
}
if (failed) process.exitCode = 1;
else console.log('PASS ' + cases.length + ' generated sticker atlas loading, crop, anchors and fallback checks');
