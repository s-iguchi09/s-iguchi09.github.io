// 記事の図（ウィンドウの画面キャプチャ）が空白になっていないかを調べる。
//
// 画面の消灯中やロック中に撮ると、PrintWindow はタイトルバーだけを描き、その下を一色で返す。
// tools/screenshot-capture の Capture.SaveWindow は撮影時にこれを止めるが、ツールを通らずに
// 入った PNG（手で置いた画像、検査を入れる前に撮った画像など）は止められない。
// CodeRabbit も画像をレビューしないため、PR ごとにすべての PNG をここで調べる。
//
// 判定: タイトルバーより下（上端から TITLE_BAR_HEIGHT 画素を除き、左右と下の縁 EDGE 画素も除く）が
// すべて同じ色なら空白とみなす。間引かずに全画素を見るので、1 画素幅の線だけの図でも通る。
//
// 使い方:
//   node .github/scripts/check-blank-screenshots.mjs            # images/articles 以下のすべての PNG
//   node .github/scripts/check-blank-screenshots.mjs a.png b.png # 指定した PNG だけ
//
// 空白の図や読めない PNG が 1 枚でもあれば終了コード 1 を返す。外部パッケージは使わない。

import { readFileSync, readdirSync } from "node:fs";
import { join, relative } from "node:path";
import { inflateSync } from "node:zlib";

// 空白だった 5 枚（表示倍率 100% で撮影）では、一色の領域が上端から 22〜23 画素目で始まっていた。
// 余裕を見て 30 画素を除く。Capture.SaveWindow は表示倍率を確かめない（100% 以外を拒むのは
// SaveScreenArea だけ）。倍率を上げて撮るとタイトルバーが 30 画素を超えることがあり、
// その文字が検査領域に入ると、空白の図でも一色にならずに見逃す。
const TITLE_BAR_HEIGHT = 30;
// ウィンドウの枠（1 画素）と影を避ける。
const EDGE = 3;
const DEFAULT_ROOT = "images/articles";

const CHANNELS = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 };
// 色の型ごとに許されるビット深度（PNG の仕様）。
const BIT_DEPTHS = { 0: [1, 2, 4, 8, 16], 2: [8, 16], 3: [1, 2, 4, 8], 4: [8, 16], 6: [8, 16] };

function decodePng(buffer) {
  const signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
  if (!signature.every((b, i) => buffer[i] === b)) {
    throw new Error("PNG のシグネチャではない");
  }

  let offset = 8;
  let header;
  let palette;
  let paletteAlpha;
  const idat = [];
  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString("latin1", offset + 4, offset + 8);
    const data = buffer.subarray(offset + 8, offset + 8 + length);
    if (data.length !== length) {
      throw new Error(`${type} チャンクが途中で切れている`);
    }
    offset += 12 + length;
    if (type === "IHDR") {
      header = {
        width: data.readUInt32BE(0),
        height: data.readUInt32BE(4),
        bitDepth: data[8],
        colorType: data[9],
        compression: data[10],
        filterMethod: data[11],
        interlace: data[12],
      };
    } else if (type === "PLTE") {
      palette = data;
    } else if (type === "tRNS") {
      paletteAlpha = data;
    } else if (type === "IDAT") {
      idat.push(data);
    } else if (type === "IEND") {
      break;
    }
  }

  if (!header) {
    throw new Error("IHDR が無い");
  }
  const { width, height, bitDepth, colorType, compression, filterMethod, interlace } = header;
  if (width === 0 || height === 0) {
    throw new Error(`幅か高さが 0 (${width}x${height})`);
  }
  const channels = CHANNELS[colorType];
  if (!channels) {
    throw new Error(`未知の色の型 ${colorType}`);
  }
  if (!BIT_DEPTHS[colorType].includes(bitDepth)) {
    throw new Error(`色の型 ${colorType} に許されないビット深度 ${bitDepth}`);
  }
  if (compression !== 0 || filterMethod !== 0) {
    throw new Error(`未知の圧縮方式 ${compression} かフィルター方式 ${filterMethod}`);
  }
  if (interlace !== 0) {
    throw new Error("インターレースの PNG は未対応");
  }
  if (colorType === 3 && !palette) {
    throw new Error("パレットの画像に PLTE が無い");
  }

  const bitsPerPixel = channels * bitDepth;
  const stride = Math.ceil((width * bitsPerPixel) / 8);
  // フィルターの左隣は 1 画素ぶん（1 バイト未満なら 1 バイト）。
  const filterStep = Math.max(1, bitsPerPixel / 8);
  const raw = inflateSync(Buffer.concat(idat));
  if (raw.length < (stride + 1) * height) {
    throw new Error("画像データが足りない");
  }

  const rows = [];
  let previous = Buffer.alloc(stride);
  for (let y = 0; y < height; y++) {
    const start = y * (stride + 1);
    const filter = raw[start];
    const line = Buffer.from(raw.subarray(start + 1, start + 1 + stride));
    for (let i = 0; i < stride; i++) {
      const left = i >= filterStep ? line[i - filterStep] : 0;
      const up = previous[i];
      const upLeft = i >= filterStep ? previous[i - filterStep] : 0;
      let add;
      switch (filter) {
        case 0: add = 0; break;
        case 1: add = left; break;
        case 2: add = up; break;
        case 3: add = (left + up) >> 1; break;
        case 4: {
          const p = left + up - upLeft;
          const pa = Math.abs(p - left);
          const pb = Math.abs(p - up);
          const pc = Math.abs(p - upLeft);
          add = pa <= pb && pa <= pc ? left : pb <= pc ? up : upLeft;
          break;
        }
        default:
          throw new Error(`未知のフィルター ${filter}（${y} 行目）`);
      }
      line[i] = (line[i] + add) & 0xff;
    }
    rows.push(line);
    previous = line;
  }

  // 画素の値を、色の比較に使う文字列で返す。パレットは色を引いてから比べる。
  function pixel(x, y) {
    const line = rows[y];
    let values;
    if (bitDepth < 8) {
      const bit = x * bitDepth;
      const shift = 8 - bitDepth - (bit % 8);
      values = [(line[bit >> 3] >> shift) & ((1 << bitDepth) - 1)];
    } else {
      const bytes = bitDepth / 8;
      const at = x * channels * bytes;
      values = Array.from(line.subarray(at, at + channels * bytes));
    }
    if (colorType === 3) {
      // 同じ RGB でもアルファが違う項目があるので、tRNS のアルファ（省略時は 255）も比べる。
      const index = values[0];
      if (index * 3 + 3 > palette.length) {
        throw new Error(`パレットに無い番号 ${index}`);
      }
      const alpha = paletteAlpha && index < paletteAlpha.length ? paletteAlpha[index] : 255;
      values = [...palette.subarray(index * 3, index * 3 + 3), alpha];
    }
    return values.join(",");
  }

  return { width, height, pixel };
}

// 空白なら理由の文字列、そうでなければ null を返す。
function findBlank(image) {
  const { width, height, pixel } = image;
  const top = TITLE_BAR_HEIGHT;
  const bottom = height - EDGE;
  const left = EDGE;
  const right = width - EDGE;
  if (bottom <= top || right <= left) {
    return null;
  }

  const first = pixel(left, top);
  for (let y = top; y < bottom; y++) {
    for (let x = left; x < right; x++) {
      if (pixel(x, y) !== first) {
        return null;
      }
    }
  }
  return `タイトルバーより下（${width}x${height} の y=${top} 以降）がすべて同じ色 (${first})`;
}

function listPngs(directory) {
  const found = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      found.push(...listPngs(path));
    } else if (entry.isFile() && entry.name.toLowerCase().endsWith(".png")) {
      found.push(path);
    }
  }
  return found.sort();
}

const files = process.argv.length > 2 ? process.argv.slice(2) : listPngs(DEFAULT_ROOT);
const failures = [];
for (const file of files) {
  const name = relative(process.cwd(), file).replaceAll("\\", "/");
  try {
    const reason = findBlank(decodePng(readFileSync(file)));
    if (reason) {
      failures.push({ name, reason });
    }
  } catch (error) {
    failures.push({ name, reason: `読めない PNG (${error.message})` });
  }
}

console.log(`${files.length} 枚の PNG を調べた。`);
if (failures.length > 0) {
  for (const { name, reason } of failures) {
    // GitHub Actions の注釈として PR の画面に出す。
    console.log(`::error file=${name}::${name}: ${reason}`);
  }
  console.log(
    `空白の図が ${failures.length} 枚ある。画面を点けた状態で ` +
      "dotnet run --project tools/screenshot-capture -c Release -- <slug> を実行して撮り直す。",
  );
  process.exit(1);
}
console.log("空白の図は無い。");
