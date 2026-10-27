"""Build/check offline open-data bitmap fonts from the game's OFL typefaces.

Requires Pillow and fontTools; optional zopfli makes smaller lossless PNGs.
Run from any directory; --check verifies without modifying outputs, --repack
losslessly recompresses existing pages. --refresh-ui-reference validates a
refreshed Unity UI font against the existing atlases before updating only its
provenance reference; it never regenerates or moves glyphs. Files stay under
Assets/Editor.
"""
from __future__ import annotations

import argparse
import functools
import hashlib
import io
import json
import math
import shutil
import struct
import zlib
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

PROJECT = Path(__file__).resolve().parents[1]
DEST = PROJECT / "Assets/Editor/FriendsFonts"
SOURCES = PROJECT / "Assets/Editor/FontSource"
RENDERER = PROJECT / "Assets/Editor/WeChatFriendsOpenData.js.txt"
BODY_SUBSET = PROJECT / "Assets/Resources/Fonts/LiangxiaUISans-Medium.ttf"
SIZE, SIDE, PADDING = 48, 1024, 2
FONT_CONFIG = {
    "body": ("NotoSansSC-variable.ttf", "NotoSansSC-OFL.txt", 600),
    "display": ("ZCOOLKuaiLe-Regular.ttf", "ZCOOLKuaiLe-OFL.txt", 400),
}
INK = "#493362"
BODY_TINTS = {"#8a739f": "body-muted", "#6645aa": "body-accent", "#ef628b": "body-coral"}
MUTED_COPY = ("名次 好友 已通关 正在更新… 正在读取好友进度… 共 位好友 页 "
              "进度同步稍后重试 错误码： 点击刷新查看好友排行 "
              "允许好友互动后，点击下方刷新 隐私功能配置尚未就绪 "
              "请检查网络后，点击下方刷新 更新未完成，点击刷新")
RASTER_PROVENANCE = ("priority pages use shipped UI subset where covered; optional pages use full Noto600; "
                     "priority glyphs outside the subset use full Noto600; display uses ZCOOL KuaiLe")


def tint_coverage() -> dict[str, set[int]]:
    return {"#8a739f": set(range(32, 127)) | {ord(c) for c in MUTED_COPY},
            "#6645aa": {ord(c) for c in "0123456789 关"},
            "#ef628b": {ord(c) for c in "更新未完成，点击刷新"}}


def rgb(color: str) -> tuple[int, int, int]:
    assert len(color) == 7 and color[0] == "#", "expected a six-digit RGB color"
    return tuple(int(color[index:index + 2], 16) for index in (1, 3, 5))


def faces(roles: dict):
    for role, face in roles.items():
        yield role, face
        for color, tint in face.get("tints", {}).items():
            yield role + "/" + color, tint


def rgba_zopfli(data: bytes) -> bytes:
    """Recompress IDAT without allowing PNG color-type/palette conversion."""
    try:
        import zopfli.zlib
    except ImportError:
        return data
    chunks, payload, offset = [], bytearray(), 8
    while offset < len(data):
        length = struct.unpack(">I", data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        content = data[offset + 8:offset + 8 + length]
        chunks.append((kind, content))
        if kind == b"IDAT":
            payload.extend(content)
        offset += length + 12
    compressed = zopfli.zlib.compress(zlib.decompress(payload), numiterations=3)
    if len(compressed) >= len(payload):
        return data
    result, written = bytearray(data[:8]), False
    for kind, content in chunks:
        if kind == b"IDAT":
            if written:
                continue
            content, written = compressed, True
        result.extend(struct.pack(">I", len(content)) + kind + content +
                      struct.pack(">I", zlib.crc32(kind + content) & 0xffffffff))
    return bytes(result)


def benchmark() -> None:
    """Measure direct-image font storage without changing generated assets."""
    metadata = read_metadata()
    source_pages = [(role, page) for role, face in metadata["roles"].items() for page in face["pages"]]
    totals = {"indexed": 0, "rgba": 0, "rgba-zopfli": 0}
    for role, page in source_pages:
        with Image.open(DEST / page["file"]) as existing:
            alpha = existing.convert("RGBA").getchannel("A")
        rgba = Image.new("RGBA", alpha.size, (0x49, 0x33, 0x62, 0))
        rgba.putalpha(alpha)
        indexed = alpha.convert("P")
        indexed.putpalette([0x49, 0x33, 0x62] * 256)
        indexed.info["transparency"] = bytes(range(256))
        buffer = io.BytesIO()
        indexed.save(buffer, format="PNG", optimize=True, compress_level=9)
        indexed_data = buffer.getvalue()
        buffer = io.BytesIO()
        rgba.save(buffer, format="PNG", optimize=True, compress_level=9)
        rgba_data = buffer.getvalue()
        optimized = rgba_zopfli(rgba_data)
        assert optimized[25] == 6, "RGBA benchmark was converted to a palette"
        totals["indexed"] += len(indexed_data)
        totals["rgba"] += len(rgba_data)
        totals["rgba-zopfli"] += len(optimized)
        print(page["file"] + ": " + json.dumps({"indexed": len(indexed_data), "rgba": len(rgba_data),
                                                 "rgba-zopfli": len(optimized)}), flush=True)
    print("ALL PAGES " + json.dumps(totals), flush=True)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def encode_page(rgba: Image.Image, encoding: str = "indexed") -> bytes:
    color = rgba.getpixel((0, 0))[:3]
    assert all(rgba.getchannel(channel).getextrema() == (value, value)
               for channel, value in zip("RGB", color)), "font page RGB must be a single pre-rendered color"
    buffer = io.BytesIO()
    if encoding == "rgba":
        rgba.save(buffer, format="PNG", optimize=True, compress_level=9)
        data = rgba_zopfli(buffer.getvalue())
        assert data[25] == 6, "priority font PNG must remain true RGBA"
        with Image.open(io.BytesIO(data)) as decoded:
            assert decoded.convert("RGBA").tobytes() == rgba.tobytes(), "PNG encoding changed glyph pixels"
        return data
    assert encoding == "indexed"
    indexed = rgba.getchannel("A").convert("P")
    indexed.putpalette(list(color) * 256)
    indexed.info["transparency"] = bytes(range(256))
    indexed.save(buffer, format="PNG", optimize=True, compress_level=9)
    data = buffer.getvalue()
    try:
        import zopfli.png
    except ImportError:
        pass
    else:
        optimized = zopfli.png.optimize(data, lossy_transparent=False, lossy_8bit=False,
                                       filter_strategies="0", num_iterations=3, num_iterations_large=3)
        if len(optimized) < len(data):
            data = optimized
    # Palette encoding is storage-only: each decoded RGBA pixel is unchanged,
    # including RGB behind fully transparent pixels and every antialias level.
    with Image.open(io.BytesIO(data)) as decoded:
        assert decoded.convert("RGBA").tobytes() == rgba.tobytes(), "PNG encoding changed glyph pixels"
    return data


def save_metadata(metadata: dict) -> None:
    (DEST / "friends-fonts.js").write_text("// Generated offline from the game's OFL fonts. See included licenses.\nmodule.exports = " +
                                         json.dumps(metadata, ensure_ascii=True, separators=(",", ":")) + ";\n", encoding="utf-8")


def read_metadata() -> dict:
    text = (DEST / "friends-fonts.js").read_text(encoding="utf-8")
    return json.loads(text.split("module.exports = ", 1)[1].rstrip().removesuffix(";"))


def coverage() -> tuple[set[int], set[int], set[int]]:
    text = RENDERER.read_text(encoding="utf-8-sig")
    fixed = set(range(32, 127)) | {ord(c) for c in text if ord(c) >= 127 and c.isprintable()}
    with TTFont(BODY_SUBSET) as font:
        priority = fixed | set(font.getBestCmap())
    common = set()
    for first in range(0xB0, 0xD8):
        for second in range(0xA1, 0xFF):
            try:
                char = bytes([first, second]).decode("gb2312")
            except UnicodeDecodeError:
                continue
            if len(char) == 1:
                common.add(ord(char))
    assert len(common) == 3755, "GB2312 level-one coverage changed"
    return fixed, priority, common


@functools.lru_cache(maxsize=2)
def raster_font(role: str) -> tuple[ImageFont.FreeTypeFont, set[int]]:
    filename, _, weight = FONT_CONFIG[role]
    font = TTFont(SOURCES / filename)
    available = set(font.getBestCmap())
    if "fvar" in font:
        # Resolve the same weight as Tools/subset_fonts.py before rasterization.
        # The complete static font remains in memory, never in the game package.
        instantiateVariableFont(font, {"wght": weight}, inplace=True)
    buffer = io.BytesIO()
    font.save(buffer)
    font.close()
    buffer.seek(0)
    return ImageFont.truetype(buffer, SIZE), available


def glyph_bitmap(font: ImageFont.FreeTypeFont, codepoint: int) -> dict:
    char = chr(codepoint)
    advance = font.getlength(char)
    if advance.is_integer():
        advance = int(advance)
    left, top, right, bottom = font.getbbox(char, anchor="ls")
    if right <= left or bottom <= top:
        return {"codepoint": codepoint, "image": None, "advance": advance}
    image = Image.new("L", (right - left, bottom - top))
    ImageDraw.Draw(image).text((-left, -top), char, font=font, fill=255, anchor="ls")
    bounds = image.getbbox()
    if not bounds:
        return {"codepoint": codepoint, "image": None, "advance": advance}
    return {"codepoint": codepoint, "image": image.crop(bounds), "xOffset": left + bounds[0],
            "yOffset": top + bounds[1], "advance": advance}


def contains(a: tuple, b: tuple) -> bool:
    return a[0] <= b[0] and a[1] <= b[1] and a[0] + a[2] >= b[0] + b[2] and a[1] + a[3] >= b[1] + b[3]


class Page:
    def __init__(self):
        self.image = Image.new("L", (SIDE, SIDE))
        self.free = [(0, 0, SIDE, SIDE)]
        self.width = self.height = 1

    def place(self, image: Image.Image) -> tuple[int, int] | None:
        width, height = image.width + 2 * PADDING, image.height + 2 * PADDING
        candidates = [(min(w - width, h - height), max(w - width, h - height), y, x)
                      for x, y, w, h in self.free if w >= width and h >= height]
        if not candidates:
            return None
        _, _, top, left = min(candidates)
        right, bottom = left + width, top + height
        free = []
        for x, y, w, h in self.free:
            if right <= x or left >= x + w or bottom <= y or top >= y + h:
                free.append((x, y, w, h))
                continue
            if left > x:
                free.append((x, y, left - x, h))
            if right < x + w:
                free.append((right, y, x + w - right, h))
            if top > y:
                free.append((x, y, w, top - y))
            if bottom < y + h:
                free.append((x, bottom, w, y + h - bottom))
        free = list(dict.fromkeys(free))
        self.free = [rect for i, rect in enumerate(free)
                     if not any(i != j and contains(other, rect) for j, other in enumerate(free))]
        self.image.paste(image, (left + PADDING, top + PADDING))
        self.width, self.height = max(self.width, right), max(self.height, bottom)
        return left + PADDING, top + PADDING


def build_role(role: str, groups: list[set[int]], color: str = INK, filename_tag: str | None = None,
               write_pages: bool = True) -> dict:
    font, available = raster_font(role)
    # Reuse shipped glyph outlines/hinting verbatim for all familiar UI text.
    # Instancer versions can round some full-source contours differently; only
    # additional name glyphs need the newly instantiated full Noto face.
    matching_font, matching_codes = None, set()
    if role == "body":
        matching_font = ImageFont.truetype(BODY_SUBSET, SIZE)
        with TTFont(BODY_SUBSET) as existing:
            matching_codes = set(existing.getBestCmap())
    required = set().union(*groups)
    missing = required - available
    if missing:
        raise ValueError(role + " source misses required codepoints: " + repr("".join(map(chr, sorted(missing)))))
    ascent, descent = font.getmetrics()
    pages, metrics, priority_pages = [], {}, 0
    for group_index, codes in enumerate(groups):
        # Each later group starts fresh pages, so normal UI does not load the
        # optional name-only atlases. Tight glyphs are packed by height/width.
        first_page = len(pages)
        glyphs = [glyph_bitmap(matching_font if code in matching_codes else font, code) for code in sorted(codes)]
        glyphs.sort(key=lambda g: (-(g["image"].height if g["image"] else 0),
                                   -(g["image"].width if g["image"] else 0), g["codepoint"]))
        for glyph in glyphs:
            image = glyph["image"]
            key = str(glyph["codepoint"])
            if image is None:
                metrics[key] = [0, 0, 0, 0, 0, 0, 0, glyph["advance"]]
                continue
            position, page_index = None, first_page
            for page_index in range(first_page, len(pages)):
                position = pages[page_index].place(image)
                if position is not None:
                    break
            if position is None:
                page_index = len(pages)
                pages.append(Page())
                position = pages[-1].place(image)
                assert position is not None, "glyph exceeds atlas size"
            metrics[key] = [page_index, *position, image.width, image.height,
                            glyph["xOffset"], glyph["yOffset"], glyph["advance"]]
        if group_index == 0:
            priority_pages = len(pages)
    page_info = []
    for index, page in enumerate(pages):
        filename = "friends-" + (filename_tag or role) + "-" + str(index) + ".png"
        # Pre-render the UI color into RGB. The open-data renderer uses only
        # drawImage(Image): no offscreen canvas or composite tinting is needed.
        alpha = page.image.crop((0, 0, page.width, page.height))
        rgba = Image.new("RGBA", alpha.size, (*rgb(color), 0))
        rgba.putalpha(alpha)
        path = DEST / filename
        encoding = "rgba" if index < priority_pages else "indexed"
        encoded = encode_page(rgba, encoding)
        if write_pages:
            path.write_bytes(encoded)
        page_info.append({"file": filename, "width": rgba.width, "height": rgba.height,
                          "encoding": encoding, "bytes": len(encoded), "sha256": hashlib.sha256(encoded).hexdigest(),
                          "alphaSha256": hashlib.sha256(alpha.tobytes()).hexdigest(),
                          "rgbaSha256": hashlib.sha256(rgba.tobytes()).hexdigest()})
    return {"color": color, "ascent": ascent, "descent": descent, "priorityPages": priority_pages,
            "pages": page_info, "glyphs": dict(sorted(metrics.items(), key=lambda item: int(item[0])))}


def build() -> dict:
    fixed, priority, common = coverage()
    DEST.mkdir(parents=True, exist_ok=True)
    roles = {"body": build_role("body", [priority, common - priority]),
             "display": build_role("display", [fixed])}
    # Only the fixed UI copy needs additional colors. Nicknames retain complete
    # Noto coverage in ink without multiplying the large optional name atlases.
    roles["body"]["tints"] = {color: build_role("body", [tint_coverage()[color]], color, tag)
                              for color, tag in BODY_TINTS.items()}
    sources = []
    for role, (filename, license_name, weight) in FONT_CONFIG.items():
        source = SOURCES / filename
        license_target = "OFL-" + role + ".txt"
        shutil.copyfile(SOURCES / license_name, DEST / license_target)
        sources.append({"role": role, "path": source.relative_to(PROJECT).as_posix(),
                        "sha256": sha256(source), "weight": weight, "license": license_target,
                        "licenseSha256": sha256(DEST / license_target)})
    metadata = {"version": 2, "size": SIZE, "padding": PADDING, "encoding": "lossless-colored-alpha-rgba-priority-indexed-optional",
                "baseline": "alphabetic", "roles": roles,
                "provenance": {"generator": "Tools/build_friends_fonts.py", "sources": sources,
                               "uiSubset": {"path": BODY_SUBSET.relative_to(PROJECT).as_posix(), "sha256": sha256(BODY_SUBSET)},
                               "uiRasterization": RASTER_PROVENANCE,
                               "fixedCopy": {"path": RENDERER.relative_to(PROJECT).as_posix(), "sha256": sha256(RENDERER)},
                               "commonNameCoverage": "GB2312 level-one 3755 characters; unsupported nickname glyphs need system fallback"}}
    save_metadata(metadata)
    expected = {page["file"] for _, face in faces(roles) for page in face["pages"]}
    # Remove only stale atlas pages owned by this generator, never Unity metas.
    for old in DEST.glob("friends-*.png"):
        if old.name not in expected:
            old.unlink()
    return metadata


def repack() -> None:
    metadata = read_metadata()
    for _, face in faces(metadata["roles"]):
        for page in face["pages"]:
            path = DEST / page["file"]
            with Image.open(path) as image:
                rgba = image.convert("RGBA")
            old_pixel_hash = hashlib.sha256(rgba.tobytes()).hexdigest()
            path.write_bytes(encode_page(rgba, page["encoding"]))
            page.update(bytes=path.stat().st_size, sha256=sha256(path), rgbaSha256=old_pixel_hash,
                        alphaSha256=hashlib.sha256(rgba.getchannel("A").tobytes()).hexdigest())
        for glyph in face["glyphs"].values():
            if isinstance(glyph[7], float) and glyph[7].is_integer():
                glyph[7] = int(glyph[7])
    metadata["encoding"] = "lossless-colored-alpha-rgba-priority-indexed-optional"
    save_metadata(metadata)


def check(*, refresh_ui_reference: bool = False) -> dict:
    metadata = read_metadata()
    assert metadata["size"] == SIZE and metadata["padding"] == PADDING
    fixed, priority, common = coverage()
    assert metadata["roles"]["body"]["color"] == metadata["roles"]["display"]["color"] == INK
    assert set(metadata["roles"]["body"]["tints"]) == set(BODY_TINTS)
    assert not metadata["roles"]["display"].get("tints"), "display face needs only ink"
    for role, data in faces(metadata["roles"]):
        required = priority | common if role == "body" else tint_coverage()[role.split("/", 1)[1]] if "/" in role else fixed
        missing = required - {int(c) for c in data["glyphs"]}
        assert not missing, role + " atlas misses current copy or name coverage: " + repr("".join(map(chr, sorted(missing))))
        for page in data["pages"]:
            path = DEST / page["file"]
            assert sha256(path) == page["sha256"], "page checksum mismatch: " + path.name
            with Image.open(path) as image:
                assert image.size == (page["width"], page["height"])
                assert 0 < image.width <= SIDE and 0 < image.height <= SIDE
                rgba = image.convert("RGBA")
                assert image.mode == ("RGBA" if page["encoding"] == "rgba" else "P"), "unexpected font page encoding"
                assert hashlib.sha256(rgba.tobytes()).hexdigest() == page["rgbaSha256"], "decoded pixels changed"
                assert hashlib.sha256(rgba.getchannel("A").tobytes()).hexdigest() == page["alphaSha256"], "glyph alpha changed"
                assert all(rgba.getchannel(channel).getextrema() == (value, value)
                           for channel, value in zip("RGB", rgb(data["color"]))), "wrong pre-rendered font color"
        for char, glyph in data["glyphs"].items():
            assert len(glyph) == 8 and all(math.isfinite(value) for value in glyph)
            page, x, y, width, height, _, _, advance = glyph
            assert advance >= 0 and 0 <= page < len(data["pages"])
            if width and height:
                info = data["pages"][page]
                assert x >= PADDING and y >= PADDING
                assert x + width + PADDING <= info["width"] and y + height + PADDING <= info["height"]
            # A later Unity UI subset can add names already on optional pages.
            # Those glyphs need coverage, but only actual friend-renderer copy
            # must remain on the initially prepared priority pages.
            if int(char) in fixed:
                assert page < data["priorityPages"], "static UI glyph escaped its priority pages"
    for source in metadata["provenance"]["sources"]:
        assert sha256(PROJECT / source["path"]) == source["sha256"], "font source changed"
        assert sha256(DEST / source["license"]) == source["licenseSha256"], "font license changed"
    if not refresh_ui_reference:
        assert sha256(BODY_SUBSET) == metadata["provenance"]["uiSubset"]["sha256"], "shipped UI font changed; validate with --refresh-ui-reference"
    assert sha256(RENDERER) == metadata["provenance"]["fixedCopy"]["sha256"], "renderer fixed-copy source changed"
    for role, reference_path in [("body", BODY_SUBSET), ("display", SOURCES / FONT_CONFIG["display"][0])]:
        reference = ImageFont.truetype(reference_path, SIZE)
        with TTFont(reference_path) as reference_font:
            reference_codes = set(reference_font.getBestCmap())
        pages = {}
        data = metadata["roles"][role]
        for char, entry in data["glyphs"].items():
            code = int(char)
            # Freeze each atlas glyph's actual raster source when Unity grows
            # its subset. Optional-name pages always came from the complete
            # Noto 600 face, even if the new subset now also contains that code.
            selected_reference = reference
            if role == "body" and (entry[0] >= data["priorityPages"] or code not in reference_codes):
                selected_reference, available = raster_font("body")
                assert code in available, "body source lacks atlas glyph: " + str(code)
            elif code not in reference_codes:
                continue
            glyph = glyph_bitmap(selected_reference, code)
            page, x, y, width, height, x_offset, y_offset, advance = entry
            assert glyph["advance"] == advance, "game font advance mismatch: " + str(code)
            if glyph["image"] is None:
                assert width == height == 0
                continue
            if page not in pages:
                with Image.open(DEST / data["pages"][page]["file"]) as image:
                    pages[page] = image.convert("RGBA").getchannel("A")
            assert (glyph["xOffset"], glyph["yOffset"]) == (x_offset, y_offset), "game font baseline mismatch: " + str(code)
            assert glyph["image"].size == (width, height), "game font bounds mismatch: " + str(code)
            assert glyph["image"].tobytes() == pages[page].crop((x, y, x + width, y + height)).tobytes(), "game font raster mismatch: " + str(code)
    # All tint variants must preserve exact body metrics and alpha. Only RGB is
    # different, so game text cannot move or blur while changing semantic color.
    body = metadata["roles"]["body"]
    alpha_pages = {}
    for color, tinted in body["tints"].items():
        assert tinted["color"] == color
        for char, entry in tinted["glyphs"].items():
            base = body["glyphs"][char]
            assert entry[3:] == base[3:], "tint changed glyph geometry or advance"
            if not entry[3] or not entry[4]:
                continue
            bitmaps = []
            for face, glyph in [(body, base), (tinted, entry)]:
                page, x, y, width, height = glyph[:5]
                filename = face["pages"][page]["file"]
                if filename not in alpha_pages:
                    with Image.open(DEST / filename) as image:
                        alpha_pages[filename] = image.convert("RGBA").getchannel("A")
                bitmaps.append(alpha_pages[filename].crop((x, y, x + width, y + height)).tobytes())
            assert bitmaps[0] == bitmaps[1], "tint changed glyph alpha"
    if refresh_ui_reference:
        # Nothing is written unless coverage, exact raster provenance, page
        # checksums, licenses and all tint invariants have passed above.
        metadata["provenance"]["uiSubset"]["sha256"] = sha256(BODY_SUBSET)
        metadata["provenance"]["uiRasterization"] = RASTER_PROVENANCE
        save_metadata(metadata)
    return metadata


def report(metadata: dict) -> None:
    png_bytes = decoded = 0
    for role, data in faces(metadata["roles"]):
        size = sum(page["bytes"] for page in data["pages"])
        pixels = sum(page["width"] * page["height"] * 4 for page in data["pages"])
        png_bytes += size
        decoded += pixels
        print(role + ": " + str(len(data["glyphs"])) + " glyphs, " + str(len(data["pages"])) +
              " pages (" + str(data["priorityPages"]) + " priority), " + str(size) + " PNG bytes, " + str(pixels) + " decoded RGBA bytes")
    total_bytes = sum(path.stat().st_size for path in DEST.iterdir() if path.is_file() and path.suffix != ".meta")
    print("PASS font coverage, metrics, page bounds, hashes and licenses; total assets " + str(total_bytes) +
          " bytes; all-page decoded RGBA " + str(decoded) + " bytes")
    if total_bytes > 2 * 1024 * 1024 or decoded > 40 * 1024 * 1024:
        print("BUDGET NOTICE: assets exceed 2 MiB stored or 40 MiB decoded; keep optional name pages lazy.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="validate assets without writing")
    parser.add_argument("--repack", action="store_true", help="recompress existing PNGs without rasterizing fonts")
    parser.add_argument("--refresh-ui-reference", action="store_true", help="validate refreshed Unity subset and update provenance only; never changes PNGs")
    parser.add_argument("--benchmark", action="store_true", help="compare direct-image PNG encodings without writing")
    parser.add_argument("--benchmark-tints", action="store_true", help="measure fixed-copy tint variants without writing")
    args = parser.parse_args()
    if args.refresh_ui_reference:
        if args.repack or args.check or args.benchmark or args.benchmark_tints:
            parser.error("--refresh-ui-reference must be used on its own")
        report(check(refresh_ui_reference=True))
        raise SystemExit(0)
    if args.benchmark:
        benchmark()
        raise SystemExit(0)
    if args.benchmark_tints:
        for color, tag in BODY_TINTS.items():
            result = build_role("body", [tint_coverage()[color]], color, tag, write_pages=False)
            print(tag + ": " + str(len(result["glyphs"])) + " glyphs; " +
                  str(sum(page["bytes"] for page in result["pages"])) + " true RGBA PNG bytes", flush=True)
        raise SystemExit(0)
    if args.repack:
        repack()
    elif not args.check:
        build()
    report(check())
