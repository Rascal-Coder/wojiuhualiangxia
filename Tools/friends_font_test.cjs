'use strict';
const fs = require('node:fs'), vm = require('node:vm'), path = require('node:path'), os = require('node:os'), assert = require('node:assert/strict');
const source = fs.readFileSync('Assets/Editor/WeChatFriendsFont.js.txt', 'utf8');
const metrics = require('../Assets/Editor/FriendsFonts/friends-fonts.js');
const INK = '#493362', MUTED = '#8a739f';
function setup(options = {}) {
  const images = [], native = [], output = [], warnings = [], timers = new Map();
  let invalidations = 0, now = 0, nextTimer = 0;
  const ctx = {
    font: '10px sans-serif', textAlign: 'left', textBaseline: 'alphabetic',
    measureText(value) {
      const size = Number(/([\d.]+)px/.exec(this.font)[1]);
      const width = Array.from(String(value)).length * size * .6;
      return {width, actualBoundingBoxLeft: 0, actualBoundingBoxRight: width,
        actualBoundingBoxAscent: size * .8, actualBoundingBoxDescent: size * .2};
    },
    fillText(value, x, y) { native.push({value: String(value), x, y, color: this.fillStyle, font: this.font, align: this.textAlign, baseline: this.textBaseline}); },
    drawImage(image, ...args) {
      assert(images.includes(image), 'sharedCanvas.drawImage must only consume a wx.createImage object');
      output.push({image, args});
    },
    fillRect() { throw new Error('font rendering must not depend on runtime mask compositing'); }
  };
  Object.defineProperty(ctx, 'globalCompositeOperation', {set() { throw new Error('font rendering cannot depend on source-in support'); }});
  const wx = {
    createCanvas() { throw new Error('font rendering must not create scratch canvases'); },
    createImage() {
      if (options.createImageThrows) throw new Error('native image creation failed');
      const image = {width: 0, height: 0}; images.push(image);
      if (options.srcThrows) Object.defineProperty(image, 'src', {set() { throw new Error('native image src failed'); }});
      return image;
    }
  };
  const module = {exports: {}};
  vm.runInNewContext(source, {
    module, Intl: options.noSegmenter ? {} : Intl,
    require(id) { assert.equal(id, './fonts/friends-fonts.js'); return metrics; },
    setTimeout(fn, delay = 0) { const id = ++nextTimer; timers.set(id, {at: now + delay, fn}); return id; },
    clearTimeout(id) { timers.delete(id); },
    console: {warn(value) { warnings.push(value); }}
  });
  const font = module.exports(wx, ctx, () => { invalidations++; });
  return {
    font, ctx, images, native, output, warnings,
    invalidations: () => invalidations, timers: () => timers.size,
    finish() { for (const image of images) if (!image.settled) { image.settled = true; image.width = image.height = 2048; if (image.onload) image.onload(); } },
    fail() { for (const image of images) if (!image.settled) { image.settled = true; if (image.onerror) image.onerror(); } },
    resetDraws() { native.length = output.length = 0; },
    advance(ms) {
      const target = now + ms;
      for (let guard = 0; guard < 100; guard++) {
        const next = [...timers].filter(([, timer]) => timer.at <= target).sort((a, b) => a[1].at - b[1].at)[0];
        if (!next) { now = target; return; }
        now = next[1].at; timers.delete(next[0]); next[1].fn();
      }
      throw new Error('font timeout loop did not settle');
    }
  };
}
const cases = [];
function test(name, run) { cases.push({name, run}); }
function draw(game, value = '好友12', align = 'left', role = 'body', color = INK) {
  game.font.paint(value, 100, 80, 48, color, align, role);
}
test('metrics are available before decoding and preserve variable advances', () => {
  const game = setup();
  assert.equal(game.font.measure('中 A1', 24), 59, 'Noto Sans SC 600 advances at 24px must equal half the 48px metrics');
  assert.equal(game.images.length, 0, 'measuring and ellipsis fitting must not decode images');
  assert(game.font.measure('111', 48) < game.font.measure('WWW', 48));
});
test('pending atlas pages never flash system text, then draw exact glyph images directly', () => {
  const game = setup(); draw(game);
  assert(game.images.length > 0); assert.equal(game.native.length, 0); assert.equal(game.output.length, 0);
  assert(game.images.every(image => /^open-data\/fonts\/friends-(body|display)-.*\.png$/.test(image.src)));
  const requested = game.images.length; game.finish(); assert.equal(game.invalidations(), requested);
  draw(game); assert.equal(game.native.length, 0); assert.equal(game.output.length, 4);
  assert(game.output.every(call => call.args.length === 8 && call.args[2] > 0 && call.args[3] > 0), 'each image draw must crop an actual atlas glyph');
  draw(game); assert.equal(game.images.length, requested, 'successful local atlas pages must be cached');
});
test('priority prepare preloads public label pages without reading friend data', () => {
  const game = setup(); game.font.prepare();
  assert(game.images.length > 0); const count = game.images.length;
  game.font.prepare(); assert.equal(game.images.length, count);
  assert.equal(game.output.length, 0); assert.equal(game.native.length, 0);
});
test('fixed-copy colors use precolored glyph images and unusual nickname characters reuse the base face', () => {
  const game = setup(), base = metrics.roles.body;
  assert(base.tints && base.tints[MUTED], 'a muted fixed-copy face must be generated');
  draw(game, '好友12', 'left', 'body', MUTED); game.finish(); draw(game, '好友12', 'left', 'body', MUTED);
  const mutedFiles = new Set(base.tints[MUTED].pages.map(page => 'open-data/fonts/' + page.file));
  assert(game.output.every(call => mutedFiles.has(call.image.src)), 'muted labels must use their actual precolored atlas');
  const commonOnly = Object.keys(base.glyphs).find(code => !base.tints[MUTED].glyphs[code] && base.glyphs[code][3] > 0);
  assert(commonOnly, 'the tint face must remain a small subset instead of duplicating every nickname glyph');
  const extra = String.fromCodePoint(Number(commonOnly)), fallback = setup();
  draw(fallback, extra, 'left', 'body', MUTED); fallback.finish(); draw(fallback, extra, 'left', 'body', MUTED);
  const baseFiles = new Set(base.pages.map(page => 'open-data/fonts/' + page.file));
  assert(fallback.output.length > 0); assert(fallback.output.every(call => baseFiles.has(call.image.src)));
  assert.equal(fallback.native.length, 0, 'known nickname characters keep the game font even without a tint copy');
});
test('left center and right alignment use the same exact glyph advances', () => {
  const game = setup(); draw(game); game.finish(); game.resetDraws();
  const width = game.font.measure('好友12', 48);
  draw(game, '好友12', 'left'); draw(game, '好友12', 'center'); draw(game, '好友12', 'right');
  const left = game.output[0].args[4], center = game.output[4].args[4], right = game.output[8].args[4];
  assert.equal(left - center, width / 2); assert.equal(left - right, width);
});
test('only unsupported nickname clusters use native fallback', () => {
  const game = setup(); draw(game, '好🧪友'); game.finish(); game.resetDraws(); draw(game, '好🧪友');
  assert.deepEqual(game.native.map(call => call.value), ['🧪']);
  assert.equal(game.output.length, 2); assert.equal(game.native[0].x, 148, 'emoji begins after the first 48px game glyph');
  assert.equal(game.native[0].baseline, 'alphabetic');
});
test('display and body use their respective game font atlases', () => {
  const game = setup(); draw(game, '好友', 'left', 'display'); game.finish(); draw(game, '好友', 'left', 'display');
  assert(game.output.every(call => call.image.src.includes('friends-display-'))); assert.equal(game.native.length, 0);
});
test('atlas failure retains readable text and reports only the public asset name', () => {
  const game = setup(); draw(game); game.fail(); draw(game);
  assert.deepEqual(game.native.map(call => call.value), ['好友12']);
  assert(game.warnings.every(line => line.startsWith('[INK_FRIENDS] font image unavailable: friends-')));
  assert.equal(game.output.length, 0);
});
test('image decoding timeout is bounded and falls back to a readable complete label', () => {
  const game = setup(); draw(game); game.advance(4499); draw(game);
  assert.equal(game.native.length, 0); assert.equal(game.output.length, 0);
  game.advance(1); draw(game);
  assert.deepEqual(game.native.map(call => call.value), ['好友12']);
  assert.equal(game.timers(), 0); assert(game.invalidations() > 0);
  const count = game.images.length; game.advance(30000); draw(game); assert.equal(game.images.length, count, 'timeout must not cause an image retry loop');
});
test('successful decoding cancels font timers and does not cause late redraws', () => {
  const game = setup(); draw(game); assert(game.timers() > 0); game.finish(); assert.equal(game.timers(), 0);
  const invalidated = game.invalidations(); game.advance(30000); assert.equal(game.invalidations(), invalidated);
});
test('late image callbacks after timeout cannot resurrect a failed page or restart loading', () => {
  const game = setup(); draw(game); game.advance(4500);
  const invalidated = game.invalidations(), count = game.images.length; game.finish(); draw(game);
  assert.equal(game.invalidations(), invalidated); assert.equal(game.images.length, count);
  assert.deepEqual(game.native.map(call => call.value), ['好友12']); assert.equal(game.output.length, 0);
});
test('a zero-size image decode cannot leave the ranking label invisible', () => {
  const game = setup(); draw(game); for (const image of game.images) image.onload(); draw(game);
  assert.deepEqual(game.native.map(call => call.value), ['好友12']); assert.equal(game.output.length, 0);
  assert.equal(game.timers(), 0);
});
test('native image creation and src errors keep labels readable', () => {
  for (const options of [{createImageThrows: true}, {srcThrows: true}]) {
    const game = setup(options); draw(game); game.finish(); draw(game);
    assert(game.native.some(call => call.value === '好友12'), JSON.stringify(options) + ': a native complete-label fallback is required');
    assert.equal(game.timers(), 0);
  }
});
test('ellipsis fitting respects measured widths without splitting graphemes', () => {
  const game = setup(), family = '👨‍👩‍👧‍👦', mark = 'e\u0301';
  for (const cluster of [family, mark]) {
    const original = '好友' + cluster + '继续';
    const width = game.font.measure('好友…', 48) + game.font.measure(cluster, 48) / 2;
    const clipped = game.font.truncate(original, width, 48);
    assert.equal(clipped, '好友…'); assert(game.font.measure(clipped, 48) <= width);
  }
  assert.equal(game.font.truncate('好友', 200, 48), '好友');
});
test('clients without Intl.Segmenter preserve joined emoji, flags and combining marks', () => {
  const game = setup({noSegmenter: true});
  for (const cluster of ['👨‍👩‍👧‍👦', 'e\u0301', '🇨🇳', '👍🏽', '❤️', '🏴\u{e0067}\u{e0062}\u{e0065}\u{e006e}\u{e0067}\u{e007f}']) {
    const width = game.font.measure('好友…', 48) + game.font.measure(cluster, 48) / 2;
    const clipped = game.font.truncate('好友' + cluster + '继续', width, 48);
    assert.equal(clipped, '好友…', 'the fallback must remove the whole cluster ' + cluster);
  }
});
test('native fallback measuring uses a stable left alphabetic baseline', () => {
  const game = setup(); game.ctx.textAlign = 'center'; game.ctx.textBaseline = 'middle';
  game.font.measure('🧪', 48);
  assert.equal(game.ctx.textAlign, 'left'); assert.equal(game.ctx.textBaseline, 'alphabetic');
});
test('clear keeps only public atlas images without creating private nickname surfaces', () => {
  const game = setup(); draw(game); game.finish(); draw(game); const count = game.images.length;
  game.font.clear(); game.resetDraws(); draw(game);
  assert.equal(game.images.length, count); assert.equal(game.output.length, 4); assert.equal(game.native.length, 0);
});
test('real PNG glyphs paint visible exact-color pixels on an Image-only shared canvas', async () => {
  let canvas;
  try { canvas = require('@napi-rs/canvas'); }
  catch (_) { canvas = require(path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas')); }
  const labels = [[INK, '好友12关'], [MUTED, '名次好友已通关12'], ['#6645aa', '12 关'], ['#ef628b', '更新未完成，点击刷新']];
  for (const [color, label] of labels) {
    const screen = canvas.createCanvas(500, 140), ctx = screen.getContext('2d'), drawImage = ctx.drawImage.bind(ctx);
    const images = new Set(), pending = [], timers = new Map(); let nextId = 0;
    ctx.drawImage = (image, ...args) => { assert(images.has(image), 'a non-Image source would be blank on restricted open-data clients'); return drawImage(image.actual, ...args); };
    const wx = {
      createCanvas() { throw new Error('Image-only platform cannot render a scratch canvas'); },
      createImage() {
        const image = {width: 0, height: 0}; images.add(image);
        Object.defineProperty(image, 'src', {set(url) {
          pending.push(canvas.loadImage(fs.readFileSync(path.join('Assets/Editor/FriendsFonts', path.basename(url))))
            .then(actual => { image.actual = actual; image.width = actual.width; image.height = actual.height; image.onload(); }));
        }}); return image;
      }
    };
    const module = {exports: {}};
    vm.runInNewContext(source, {module, Intl, console,
      require() { return metrics; },
      setTimeout(fn) { const id = ++nextId; timers.set(id, fn); return id; },
      clearTimeout(id) { timers.delete(id); }});
    const font = module.exports(wx, ctx, () => {});
    font.paint(label, 250, 70, 38, color); await Promise.all(pending); font.paint(label, 250, 70, 38, color);
    const pixels = ctx.getImageData(0, 0, 500, 140).data, expected = [1, 3, 5].map(offset => parseInt(color.slice(offset, offset + 2), 16));
    let visible = 0, solidColor = 0;
    for (let index = 0; index < pixels.length; index += 4) {
      if (pixels[index + 3] > 0) visible++;
      if (pixels[index + 3] === 255 && expected.every((value, channel) => pixels[index + channel] === value)) solidColor++;
    }
    assert(visible > 250, color + ': ranking labels must produce real visible pixels');
    assert(solidColor > 100, color + ': font textures must contain their intended precolored ink');
    assert.equal(timers.size, 0);
  }
});
async function main() {
  let failed = 0;
  for (const {name, run} of cases) {
    try { await run(); console.log('PASS ' + name); }
    catch (error) { failed++; console.error('FAIL ' + name + '\n  ' + error.stack); }
  }
  if (failed) process.exitCode = 1;
  else console.log('PASS ' + cases.length + ' Image-only bitmap font loading, metrics, color, timeout and fallback checks');
}
main().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
