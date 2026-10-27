"""Normalize existing generated friend-rank artwork without redrawing it.

Uses Pillow. Optional zopfli applies lossless PNG compression. Originals remain
unchanged in Assets/Editor/StickerArtSource; --check only validates outputs.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parents[1]
SOURCES = PROJECT / "Assets/Editor/StickerArtSource"
DEST = PROJECT / "Assets/Editor/FriendsArt"
PREVIEW = PROJECT.parent / "好友排行_素材预览.png"
SOURCE_NAMES = {"rank1": "friends_rank_1.png", "rank2": "friends_rank_2.png",
                "rank3": "friends_rank_3.png", "empty": "friends_empty.png"}
PADDING, ALPHA_THRESHOLD = 4, 8
RANK_SIZE = (160, 216)
EMPTY_WIDTH = 384


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def visible_crop(image: Image.Image) -> tuple[int, int, int, int]:
    # Generation left isolated <=3% alpha pixels around the entire canvas.
    # Crop around the visible silhouette plus a 2px source safety border; do
    # not modify any alpha/color pixels inside that crop.
    bounds = image.getchannel("A").point(lambda value: 255 if value > ALPHA_THRESHOLD else 0).getbbox()
    assert bounds, "source artwork is empty"
    return (max(0, bounds[0] - 2), max(0, bounds[1] - 2),
            min(image.width, bounds[2] + 2), min(image.height, bounds[3] + 2))


def digit_center(image: Image.Image) -> tuple[tuple[float, float], tuple[int, int, int, int]]:
    # Measure the large pale number disk from the source, not the crown or
    # whole-image center. Its dark border separates this flood-filled region.
    red, green, blue, alpha = image.split()
    mask = ImageChops.multiply(red.point(lambda v: 255 if v >= 230 else 0),
                               green.point(lambda v: 255 if v >= 220 else 0))
    mask = ImageChops.multiply(mask, blue.point(lambda v: 255 if v >= 185 else 0))
    mask = ImageChops.multiply(mask, alpha.point(lambda v: 255 if v >= 200 else 0))
    seed = (image.width // 2, image.height // 2)
    assert mask.getpixel(seed) == 255, "number disk is not at the expected source position"
    ImageDraw.floodfill(mask, seed, 128)
    disk = mask.point(lambda v: 255 if v == 128 else 0)
    bounds = disk.getbbox()
    assert bounds and bounds[2] - bounds[0] > image.width / 5 and bounds[3] - bounds[1] > image.height / 5
    return ((bounds[0] + bounds[2]) / 2, (bounds[1] + bounds[3]) / 2), bounds


def resize(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    # Premultiplied interpolation avoids dark/color fringes at transparent edges.
    return image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")


def png_bytes(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True, compress_level=9)
    data = buffer.getvalue()
    try:
        import zopfli.png
    except ImportError:
        pass
    else:
        compressed = zopfli.png.optimize(data, lossy_transparent=False, lossy_8bit=False,
                                         num_iterations=3, num_iterations_large=3)
        if len(compressed) < len(data):
            data = compressed
    with Image.open(io.BytesIO(data)) as decoded:
        assert decoded.convert("RGBA").tobytes() == image.tobytes(), "compression changed pixels"
    return data


def normalized() -> tuple[dict, dict]:
    images, sources = {}, {}
    for key, filename in SOURCE_NAMES.items():
        path = SOURCES / filename
        with Image.open(path) as source:
            assert source.mode == "RGBA", "source must preserve RGBA transparency"
            image = source.copy()
        assert image.getchannel("A").getextrema() == (0, 255)
        crop = visible_crop(image)
        sources[key] = {"file": path.relative_to(PROJECT).as_posix(), "sha256": digest(path),
                        "size": list(image.size), "alphaBounds": list(image.getchannel("A").getbbox()),
                        "visibleCrop": list(crop), "alphaThreshold": ALPHA_THRESHOLD}
        images[key] = image
    center = (RANK_SIZE[0] / 2, RANK_SIZE[1] / 2)
    limits = []
    for key in ("rank1", "rank2", "rank3"):
        measured, disk = digit_center(images[key])
        sources[key].update(sourceDigit=list(measured), sourceDigitDisk=list(disk))
        left, top, right, bottom = sources[key]["visibleCrop"]
        limits.extend([(center[0] - PADDING) / (measured[0] - left),
                       (RANK_SIZE[0] - PADDING - center[0]) / (right - measured[0]),
                       (center[1] - PADDING) / (measured[1] - top),
                       (RANK_SIZE[1] - PADDING - center[1]) / (bottom - measured[1])])
    scale = min(limits)
    frames = {}
    for key, image in images.items():
        left, top, right, bottom = sources[key]["visibleCrop"]
        cropped = image.crop((left, top, right, bottom))
        if key == "empty":
            size = (EMPTY_WIDTH, round(cropped.height * EMPTY_WIDTH / cropped.width))
            frames[key] = {"image": resize(cropped, size)}
            sources[key].update(resized=list(size), scale=EMPTY_WIDTH / cropped.width)
            continue
        size = (round(cropped.width * scale), round(cropped.height * scale))
        small = resize(cropped, size)
        measured = sources[key]["sourceDigit"]
        x_scale, y_scale = size[0] / cropped.width, size[1] / cropped.height
        offset = (round(center[0] - (measured[0] - left) * x_scale),
                  round(center[1] - (measured[1] - top) * y_scale))
        assert offset[0] >= 0 and offset[1] >= 0 and offset[0] + size[0] <= RANK_SIZE[0] and offset[1] + size[1] <= RANK_SIZE[1]
        frame = Image.new("RGBA", RANK_SIZE)
        frame.alpha_composite(small, offset)
        point = [(offset[0] + (measured[0] - left) * x_scale) / RANK_SIZE[0],
                 (offset[1] + (measured[1] - top) * y_scale) / RANK_SIZE[1]]
        frames[key] = {"image": frame, "digit": [round(v, 8) for v in point]}
        sources[key].update(resized=list(size), scale=scale, frameOffset=list(offset))
    return frames, sources


def contact_sheet(frames: dict) -> None:
    image = Image.new("RGBA", (1000, 650), "#fff9ee")
    draw = ImageDraw.Draw(image)
    font_path = PROJECT / "Assets/Resources/Fonts/LiangxiaUISans-Medium.ttf"
    digit_font = ImageFont.truetype(str(font_path), 52)
    label_font = ImageFont.truetype(str(PROJECT / "Assets/Resources/Fonts/LiangxiaUISans-Medium.ttf"), 22)
    draw.text((32, 20), "好友排行", font=label_font, fill="#493362")
    for index, key in enumerate(("rank1", "rank2", "rank3")):
        item = frames[key]
        x, y = 38 + index * 182, 90
        image.alpha_composite(item["image"], (x, y))
        cx = x + item["digit"][0] * RANK_SIZE[0]
        cy = y + item["digit"][1] * RANK_SIZE[1]
        text = str(index + 1)
        bounds = digit_font.getbbox(text, anchor="ls")
        baseline = cy - (bounds[1] + bounds[3]) / 2
        draw.text((cx - digit_font.getlength(text) / 2, baseline), text, font=digit_font, fill="#493362", anchor="ls")
        label = "第" + text + "名"
        draw.text((x + (RANK_SIZE[0] - label_font.getlength(label)) / 2, 328), label, font=label_font, fill="#8a739f")
    image.alpha_composite(frames["empty"]["image"], (585, 82))
    # Checkerboard underneath a second small sample makes alpha edge quality
    # inspectable without baking a background into the production atlas.
    for y in range(430, 620, 12):
        for x in range(38, 960, 12):
            draw.rectangle((x, y, x + 11, y + 11), fill="#e7d8f2" if ((x - 38) // 12 + (y - 430) // 12) % 2 else "#f8f0ff")
    for index, key in enumerate(("rank1", "rank2", "rank3", "empty")):
        art = frames[key]["image"]
        art = resize(art, (round(art.width * 170 / art.height), 170))
        image.alpha_composite(art, (65 + index * 225, 440))
    image.convert("RGB").save(PREVIEW, optimize=True)


def build() -> dict:
    frames, sources = normalized()
    width = RANK_SIZE[0] * 3 + PADDING * 4
    empty = frames["empty"]["image"]
    height = PADDING * 3 + RANK_SIZE[1] + empty.height
    atlas = Image.new("RGBA", (width, height))
    metadata = {"file": "friends-art.png", "width": width, "height": height, "frames": {}}
    for index, key in enumerate(("rank1", "rank2", "rank3")):
        x, y = PADDING + index * (RANK_SIZE[0] + PADDING), PADDING
        item = frames[key]
        atlas.paste(item["image"], (x, y))
        metadata["frames"][key] = {"x": x, "y": y, "w": RANK_SIZE[0], "h": RANK_SIZE[1], "digit": item["digit"]}
    x, y = (width - empty.width) // 2, PADDING * 2 + RANK_SIZE[1]
    atlas.paste(empty, (x, y))
    metadata["frames"]["empty"] = {"x": x, "y": y, "w": empty.width, "h": empty.height}
    metadata["normalization"] = {"padding": PADDING, "method": "visible alpha bbox, common rank scale, premultiplied Lanczos, no redrawing",
                                 "sources": sources}
    DEST.mkdir(parents=True, exist_ok=True)
    target = DEST / metadata["file"]
    target.write_bytes(png_bytes(atlas))
    metadata.update(bytes=target.stat().st_size, sha256=digest(target), rgbaSha256=hashlib.sha256(atlas.tobytes()).hexdigest())
    (DEST / "friends-art.js").write_text("// Generated from the original RGBA sticker artwork.\nmodule.exports = " +
                                       json.dumps(metadata, ensure_ascii=True, separators=(",", ":")) + ";\n", encoding="utf-8")
    contact_sheet(frames)
    return metadata


def check() -> dict:
    data = (DEST / "friends-art.js").read_text(encoding="utf-8")
    metadata = json.loads(data.split("module.exports = ", 1)[1].rstrip().removesuffix(";"))
    target = DEST / metadata["file"]
    assert digest(target) == metadata["sha256"]
    with Image.open(target) as image:
        atlas = image.convert("RGBA")
    assert atlas.size == (metadata["width"], metadata["height"])
    assert hashlib.sha256(atlas.tobytes()).hexdigest() == metadata["rgbaSha256"]
    expected, sources = normalized()
    occupied = Image.new("1", atlas.size)
    for key, frame in metadata["frames"].items():
        x, y, w, h = (frame[name] for name in ("x", "y", "w", "h"))
        assert 0 <= x < x + w <= atlas.width and 0 <= y < y + h <= atlas.height
        assert occupied.crop((x, y, x + w, y + h)).getbbox() is None
        ImageDraw.Draw(occupied).rectangle((x, y, x + w - 1, y + h - 1), fill=1)
        assert atlas.crop((x, y, x + w, y + h)).tobytes() == expected[key]["image"].tobytes()
        assert sources[key]["sha256"] == metadata["normalization"]["sources"][key]["sha256"]
        if key.startswith("rank"):
            assert (w, h) == RANK_SIZE and frame["digit"] == expected[key]["digit"]
            assert all(math.isfinite(value) and 0.49 < value < 0.51 for value in frame["digit"])
    assert atlas.getchannel("A").getextrema() == (0, 255)
    return metadata


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="validate without rewriting assets")
    args = parser.parse_args()
    if not args.check:
        build()
    result = check()
    print("PASS original hashes, alpha bounds, proportional resize, aligned digit centers, packing and lossless PNG")
    print("Atlas " + str(result["width"]) + "x" + str(result["height"]) + ", " + str(result["bytes"]) + " bytes")
    print(json.dumps(result["frames"], ensure_ascii=False))
    if result["bytes"] > 200 * 1024:
        print("BUDGET NOTICE: atlas exceeds 200 KiB")
