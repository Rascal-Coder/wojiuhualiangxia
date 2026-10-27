/* Render the exact open-data renderer with synthetic fixtures. No network or real friend data. */
'use strict';
const fs = require('fs');
const path = require('path');
const os = require('os');
const vm = require('vm');
const assert = require('assert/strict');

function canvasModule() {
  try { return require('@napi-rs/canvas'); }
  catch (_) {
    return require(path.join(os.homedir(), '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas'));
  }
}
const { createCanvas, GlobalFonts, loadImage } = canvasModule();
const project = path.resolve(__dirname, '..');
const sourcePath = path.join(project, 'Assets/Editor/WeChatFriendsOpenData.js.txt');
const fontModulePath = path.join(project, 'Assets/Editor/WeChatFriendsFont.js.txt');
const fontAssetDirectory = path.join(project, 'Assets/Editor/FriendsFonts');
const artModulePath = path.join(project, 'Assets/Editor/WeChatFriendsArt.js.txt');
const artAssetDirectory = path.join(project, 'Assets/Editor/FriendsArt');
const outputPath = path.resolve(process.argv[2] || path.join(project, '../好友排行_样式预览.png'));
const source = fs.readFileSync(sourcePath, 'utf8');
// Panel captions are outside the game viewport. Production text inside the
// viewport uses its own bitmap-font module and actual texture assets below.
const windowsFonts = path.join(process.env.WINDIR || 'C:/Windows', 'Fonts');
const fontPath = fs.existsSync(path.join(windowsFonts, 'msyh.ttc'))
  ? path.join(windowsFonts, 'msyh.ttc') : path.join(project, 'Assets/Editor/FontSource/NotoSansSC-variable.ttf');
GlobalFonts.registerFromPath(fontPath);
GlobalFonts.registerFromPath(fontPath, 'Preview UI');
if (fs.existsSync(path.join(windowsFonts, 'msyhbd.ttc'))) {
  GlobalFonts.registerFromPath(path.join(windowsFonts, 'msyhbd.ttc'));
  GlobalFonts.registerFromPath(path.join(windowsFonts, 'msyhbd.ttc'), 'Preview UI');
}
if (fs.existsSync(path.join(windowsFonts, 'seguiemj.ttf'))) GlobalFonts.registerFromPath(path.join(windowsFonts, 'seguiemj.ttf'), 'Segoe UI Emoji');
GlobalFonts.registerFromPath(path.join(project, 'Assets/Editor/FontSource/ZCOOLKuaiLe-Regular.ttf'), 'Preview Display');

const fixtures = [
  ['小桃今天也要贴满格', 500], ['薄荷汽水', 256], ['小熊猫🐼一起划两下', 138],
  ['超级长名字的小伙伴也要快乐贴满一整本手账', 76], ['🌸 小花同学 ✨', 31],
  ['晚风和橘子', 22], ['阿柠', 8], ['快乐新同学', 2]
].map(([nickname, completed], i) => ({
  openid: 'synthetic-preview-' + i, nickname,
  avatarUrl: 'https://preview.invalid/avatar-' + i + '.png',
  KVDataList: [{ key: 'inkomino_completed_v1', value: String(completed) }]
}));

async function previewAssets() {
  const atlas = await loadImage(fs.readFileSync(path.join(project, 'Assets/Resources/MenuArt/sticker_avatars_atlas.bytes')));
  const index = JSON.parse(fs.readFileSync(path.join(project, 'Assets/Resources/MenuArt/sticker_avatars_atlas_index.json'), 'utf8'));
  const avatars = index.regions.map(region => {
    const cropped = createCanvas(region.width, region.height);
    // Unity's atlas rectangles start at the bottom left; Canvas starts at the top left.
    cropped.getContext('2d').drawImage(atlas, region.x, index.height - region.y - region.height,
      region.width, region.height, 0, 0, region.width, region.height);
    return cropped;
  });
  const fonts = new Map();
  for (const filename of fs.readdirSync(fontAssetDirectory).filter(filename => /\.png$/i.test(filename))) {
    fonts.set(filename, await loadImage(fs.readFileSync(path.join(fontAssetDirectory, filename))));
  }
  assert(fonts.size > 0, 'The production bitmap-font textures must exist before rendering.');
  const art = new Map();
  for (const filename of fs.readdirSync(artAssetDirectory).filter(filename => /\.png$/i.test(filename))) {
    art.set(filename, await loadImage(fs.readFileSync(path.join(artAssetDirectory, filename))));
  }
  assert(art.has('friends-art.png'), 'The production ranking art atlas must exist before rendering.');
  return { avatars, fonts, art };
}

function renderer(width, height, assets) {
  const canvas = createCanvas(width, height), ctx = canvas.getContext('2d');
  let now = 100000, nextId = 1, receive, textBounds = [], imageBounds = [], semanticLabels = [];
  const wxImages = new WeakSet();
  const timers = new Map(), frames = new Map(), imageLoads = [], reads = [], warnings = [];
  const nativeFillText = ctx.fillText.bind(ctx), nativeClear = ctx.clearRect.bind(ctx), nativeDrawImage = ctx.drawImage.bind(ctx);
  ctx.clearRect = (...args) => { textBounds = []; imageBounds = []; semanticLabels = []; nativeClear(...args); };
  ctx.fillText = (value, x, y, ...rest) => {
    const metrics = ctx.measureText(String(value));
    const align = ctx.textAlign, anchor = align === 'center' ? metrics.width / 2 : (align === 'right' || align === 'end') ? metrics.width : 0;
    const matrix = ctx.getTransform();
    const left = matrix.a * (x - anchor) + matrix.c * y + matrix.e;
    const right = matrix.a * (x - anchor + metrics.width) + matrix.c * y + matrix.e;
    textBounds.push({ text: String(value), left, right, y: matrix.b * x + matrix.d * y + matrix.f });
    nativeFillText(value, x, y, ...rest);
  };
  ctx.drawImage = (image, ...args) => {
    // WeChat mobile accepts SDK Image objects here. Do not let a native desktop
    // Canvas hide a production regression involving unsupported scratch canvases.
    assert(image && wxImages.has(image), 'Shared-canvas drawImage source must originate from wx.createImage().');
    const actualImage = image && image.previewCanvas || image;
    const rect = args.length === 2 ? [args[0], args[1], actualImage.width, actualImage.height] : args.slice(-4);
    const matrix = ctx.getTransform();
    imageBounds.push({ kind: image && image.previewKind || 'bitmap', source: args.length === 8 ? args.slice(0, 4) : null,
      left: matrix.a * rect[0] + matrix.c * rect[1] + matrix.e,
      top: matrix.b * rect[0] + matrix.d * rect[1] + matrix.f,
      right: matrix.a * (rect[0] + rect[2]) + matrix.c * (rect[1] + rect[3]) + matrix.e,
      bottom: matrix.b * (rect[0] + rect[2]) + matrix.d * (rect[1] + rect[3]) + matrix.f });
    return nativeDrawImage(actualImage, ...args);
  };
  class PreviewDate extends Date { constructor(...args) { super(...(args.length ? args : [now])); } static now() { return now; } }
  const sandbox = {
    wx: {
      getSharedCanvas: () => canvas,
      onMessage: callback => { receive = callback; },
      getFriendCloudStorage: options => reads.push(options),
      getUserCloudStorage: options => options.success({ KVDataList: [] }),
      setUserCloudStorage: options => options.success({}),
      createImage() {
        const image = { onload: null, onerror: null };
        wxImages.add(image);
        Object.defineProperty(image, 'src', { set(url) {
          const match = /^https:\/\/preview\.invalid\/avatar-(\d+)\.png$/.exec(String(url));
          const fontImage = assets.fonts.get(path.basename(String(url)));
          const artImage = assets.art.get(path.basename(String(url)));
          if (match) {
            image.previewCanvas = assets.avatars[Number(match[1]) % assets.avatars.length];
            image.previewKind = 'avatar';
            image.width = image.previewCanvas.width; image.height = image.previewCanvas.height;
            imageLoads.push(() => image.onload && image.onload());
          }
          else if (fontImage) {
            image.previewCanvas = fontImage; image.previewKind = 'font'; image.width = fontImage.width; image.height = fontImage.height;
            imageLoads.push(() => image.onload && image.onload());
          }
          else if (artImage) {
            image.previewCanvas = artImage; image.previewKind = 'art'; image.width = artImage.width; image.height = artImage.height;
            imageLoads.push(() => image.onload && image.onload());
          }
          else imageLoads.push(() => image.onerror && image.onerror());
        } });
        return image;
      }
    },
    Date: PreviewDate, performance: { now: () => now },
    setTimeout(callback, delay = 0) { const id = nextId++; timers.set(id, { at: now + delay, delay, callback }); return id; },
    clearTimeout: id => timers.delete(id),
    requestAnimationFrame(callback) { const id = nextId++; frames.set(id, callback); return id; },
    cancelAnimationFrame: id => frames.delete(id),
    console: { log() {}, warn(value) { warnings.push(String(value)); }, error() {} }, Intl
  };
  vm.createContext(sandbox);
  const moduleCache = new Map();
  sandbox.require = function previewRequire(moduleId) {
    if (moduleCache.has(moduleId)) return moduleCache.get(moduleId);
    let filename;
    if (moduleId === './font') filename = fontModulePath;
    else if (moduleId === './fonts/friends-fonts.js') filename = path.join(fontAssetDirectory, 'friends-fonts.js');
    else if (moduleId === './art') filename = artModulePath;
    else if (moduleId === './art/friends-art.js') filename = path.join(artAssetDirectory, 'friends-art.js');
    else throw new Error('Unexpected production preview module: ' + moduleId);
    const module = { exports: {} };
    const wrapper = vm.runInContext('(function(module, exports, require) {\n' + fs.readFileSync(filename, 'utf8') + '\n})', sandbox, { filename });
    wrapper(module, module.exports, sandbox.require); moduleCache.set(moduleId, module.exports); return module.exports;
  };
  vm.runInContext(source, sandbox, { filename: sourcePath });
  // Observe semantic text calls without substituting fonts or layout. Combine
  // each bitmap line and any native fallback runs for geometry validation.
  sandbox.__previewTextStart = (value, x, y) => {
    semanticLabels.push(String(value));
    return { text: String(value), x, y, firstImage: imageBounds.length, firstText: textBounds.length };
  };
  sandbox.__previewTextEnd = record => {
    const boxes = imageBounds.slice(record.firstImage), nativeRuns = textBounds.splice(record.firstText);
    if (!boxes.length && !nativeRuns.length) return;
    const matrix = ctx.getTransform();
    textBounds.push({ text: record.text,
      left: Math.min(...boxes.map(item => item.left), ...nativeRuns.map(item => item.left)),
      right: Math.max(...boxes.map(item => item.right), ...nativeRuns.map(item => item.right)),
      top: boxes.length ? Math.min(...boxes.map(item => item.top)) : record.y,
      bottom: boxes.length ? Math.max(...boxes.map(item => item.bottom)) : record.y,
      x: matrix.a * record.x + matrix.c * record.y + matrix.e,
      y: matrix.b * record.x + matrix.d * record.y + matrix.f });
  };
  vm.runInContext('if (typeof text === "function") { const originalText = text; text = function(...args) { const observed = __previewTextStart(...args); try { return originalText.apply(this, args); } finally { __previewTextEnd(observed); } }; }', sandbox);
  function advance(milliseconds = 16) {
    const target = now + milliseconds;
    for (let guard = 0; guard < 200; guard++) {
      const pending = [...timers.entries()].filter(([, task]) => task.at <= target).sort((a, b) => a[1].at - b[1].at)[0];
      if (!pending) break;
      now = pending[1].at; timers.delete(pending[0]); pending[1].callback();
    }
    now = target;
    while (imageLoads.length) imageLoads.shift()();
    const pendingFrames = [...frames.values()]; frames.clear();
    pendingFrames.forEach(callback => callback(now));
  }
  function send(type, data = {}) { receive(JSON.stringify({ type, ...data })); }
  function respond(data) { assert(reads.length, 'The production renderer must request friend data.'); reads.shift().success({ data }); advance(); }
  function validate(label) {
    assert(!warnings.some(value => /font image unavailable/i.test(value)), label + ': bitmap-font load failed: ' + warnings.filter(value => /font image unavailable/i.test(value)).join(' / '));
    assert(![...timers.values()].some(task => task.delay === 4500), label + ': successfully decoded font pages must clear their load timeout.');
    assert(textBounds.length > 0 || imageBounds.length > 0, label + ': visible state content expected');
    assert(imageBounds.some(item => item.kind === 'font'), label + ': production font images must visibly draw; artwork alone is insufficient.');
    const remainingText = textBounds.map(item => item.text);
    for (const value of semanticLabels.filter(value => value.trim())) {
      const index = remainingText.indexOf(value);
      assert(index >= 0, label + ': requested text did not produce visible glyphs: ' + value);
      remainingText.splice(index, 1);
    }
    const overflow = textBounds.filter(item => item.left < -1 || item.right > canvas.width + 1 || item.y < -1 || item.y > canvas.height + 1);
    assert.equal(overflow.length, 0, label + ': text exceeds the shared canvas: ' + JSON.stringify(overflow));
    const imageOverflow = imageBounds.filter(item => item.left < -1 || item.right > canvas.width + 1 || item.top < -1 || item.bottom > canvas.height + 1);
    assert.equal(imageOverflow.length, 0, label + ': bitmap text/image exceeds the shared canvas: ' + JSON.stringify(imageOverflow));
    for (const text of textBounds.filter(item => item.text.trim() && item.bottom > item.top)) {
      const left = Math.max(0, Math.floor(text.left)), top = Math.max(0, Math.floor(text.top));
      const right = Math.min(canvas.width, Math.ceil(text.right)), bottom = Math.min(canvas.height, Math.ceil(text.bottom));
      const pixels = ctx.getImageData(left, top, Math.max(1, right - left), Math.max(1, bottom - top)).data;
      let inkPixels = 0;
      // All production text inks are dark enough to separate from the cream and
      // pastel cards. This catches blank/white glyphs even when drawImage succeeds.
      for (let i = 0; i < pixels.length; i += 4) {
        if (pixels[i + 3] > 64 && pixels[i] < 245 && pixels[i + 1] < 195 && pixels[i + 2] < 245) inkPixels++;
      }
      assert(inkPixels >= Math.max(1, Math.floor(pixels.length / 4 * .004)), label + ': no visible ink for text ' + text.text);
    }
    const artMetadata = moduleCache.get('./art/friends-art.js');
    for (const image of imageBounds.filter(item => item.kind === 'art' && item.source)) {
      const [sx, sy, sw, sh] = image.source, destinationRatio = (image.right - image.left) / (image.bottom - image.top);
      assert(Math.abs(destinationRatio / (sw / sh) - 1) <= .01, label + ': ranking art is stretched.');
      const frameEntry = artMetadata && Object.entries(artMetadata.frames).find(([, frame]) => frame.x === sx && frame.y === sy && frame.w === sw && frame.h === sh);
      if (!frameEntry) continue;
      const [key, frame] = frameEntry;
      if (/^rank[123]$/.test(key) && frame.digit) {
        const rankText = textBounds.find(item => item.text === key.slice(4));
        assert(rankText, label + ': rank digit missing for ' + key);
        const expectedX = image.left + (image.right - image.left) * frame.digit[0];
        const expectedY = image.top + (image.bottom - image.top) * frame.digit[1];
        assert(Math.abs(rankText.x - expectedX) < 1 && Math.abs(rankText.y - expectedY) < 1, label + ': rank digit is not at its medal center.');
      }
      if (key === 'empty') {
        const bodyLines = textBounds.filter(item => item.y > image.top);
        assert(bodyLines.every(item => item.top >= image.bottom - 1), label + ': empty illustration overlaps its text.');
      }
    }
    for (let i = 0; i < textBounds.length; i++) {
      for (let j = i + 1; j < textBounds.length; j++) {
        const a = textBounds[i], b = textBounds[j];
        if (!a.text || !b.text || Math.abs(a.y - b.y) > 1) continue;
        const intersection = Math.min(a.right, b.right) - Math.max(a.left, b.left);
        assert(intersection <= 1, label + ': aligned text overlaps: ' + a.text + ' / ' + b.text);
      }
    }
  }
  return { canvas, send, respond, advance, validate,
    fail(error) { assert(reads.length); reads.shift().fail(error); advance(); },
    labels: () => semanticLabels,
    dispose() { send('friends:hide'); }
  };
}

function statePreview(kind, width, height, assets) {
  const view = renderer(width, height, assets);
  view.send('friends:show');
  if (kind === 'loading') view.advance(450);
  else if (kind === 'empty') view.respond([]);
  else if (kind === 'permission') view.fail({ errno: 1026, errMsg: 'getFriendCloudStorage:fail please go to mp to announce your privacy usage' });
  else {
    view.respond(fixtures);
    if (kind === 'refresh') { view.advance(1600); view.send('friends:refresh'); view.advance(450); }
    if (kind === 'page2') { view.send('friends:page', { delta: 1 }); view.advance(); }
  }
  view.validate(kind + ' ' + width + '×' + height);
  if (kind === 'refresh') {
    assert(view.labels().some(value => /正在更新/.test(value)), 'Refresh preview must show an active refresh.');
    assert(view.labels().some(value => /小桃/.test(value)), 'Refresh must retain existing friend rows.');
  }
  return view;
}

function roundRect(ctx, x, y, width, height, radius) {
  ctx.beginPath(); ctx.roundRect(x, y, width, height, radius); ctx.closePath();
}

async function main() {
  const assets = await previewAssets();
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'liangxia-friends-preview-'));
  let checks = 0;
  try {
    for (const [width, height] of [[928, 758], [280, 300], [450, 360]]) {
      for (const kind of ['loading', 'ready', 'refresh', 'empty', 'permission', 'page2']) {
        const view = statePreview(kind, width, height, assets);
        fs.writeFileSync(path.join(tempRoot, kind + '-' + width + '.png'), view.canvas.toBuffer('image/png'));
        view.dispose(); checks++;
      }
    }
    const scale = 2, viewportWidth = 360, viewportHeight = Math.round(viewportWidth * 758 / 928);
    const margin = 28, gutter = 26, heading = 78, caption = 34, bottom = 44;
    const width = margin * 2 + viewportWidth * 2 + gutter;
    const height = heading + (caption + viewportHeight) * 2 + gutter + bottom;
    const montage = createCanvas(width * scale, height * scale), ctx = montage.getContext('2d');
    ctx.scale(scale, scale); ctx.fillStyle = '#f7f1ff'; ctx.fillRect(0, 0, width, height);
    ctx.font = '400 23px "Preview Display"'; ctx.fillStyle = '#48305f'; ctx.fillText('好友排行 · 列表与加载状态', margin, 35);
    ctx.font = '13px "Preview UI"'; ctx.fillStyle = '#8a769d'; ctx.fillText('同一套手账贴纸风格，加载和刷新分别呈现', margin, 58);
    const scenes = [['loading', '初次加载'], ['ready', '好友列表'], ['refresh', '刷新列表'], ['empty', '暂无好友']];
    scenes.forEach(([kind, label], i) => {
      const x = margin + (i % 2) * (viewportWidth + gutter), y = heading + Math.floor(i / 2) * (caption + viewportHeight + gutter);
      ctx.font = '400 15px "Preview Display"'; ctx.fillStyle = '#5c4178'; ctx.fillText(label, x + 2, y + 20);
      const view = statePreview(kind, viewportWidth * scale, viewportHeight * scale, assets);
      ctx.save(); roundRect(ctx, x, y + caption, viewportWidth, viewportHeight, 15); ctx.clip();
      ctx.drawImage(view.canvas, x, y + caption, viewportWidth, viewportHeight); ctx.restore();
      ctx.strokeStyle = '#dfd0ef'; ctx.lineWidth = 1; roundRect(ctx, x, y + caption, viewportWidth, viewportHeight, 15); ctx.stroke();
      view.dispose();
    });
    ctx.font = '11px "Preview UI"'; ctx.fillStyle = '#8a769d';
    ctx.fillText('样式预览使用虚构昵称、分数及本地头像；游戏中只展示微信实际返回的好友数据。', margin, height - 17);
    fs.mkdirSync(path.dirname(outputPath), { recursive: true }); fs.writeFileSync(outputPath, montage.toBuffer('image/png'));
    console.log('PASS: ' + checks + ' production-renderer viewport/state checks. Preview: ' + outputPath);
  } finally {
    // All temporary viewport images live under the newly created task-only OS temp folder.
    fs.rmSync(tempRoot, { recursive: true, force: true });
  }
}
main().catch(error => { console.error(error.stack || error); process.exitCode = 1; });
