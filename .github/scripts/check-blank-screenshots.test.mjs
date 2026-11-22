// check-blank-screenshots.mjs の判定と PNG の検証のテスト。
// 実行: node --test .github/scripts/check-blank-screenshots.test.mjs
//
// テスト用の PNG はバイナリを置かず、ここで組み立てる。

import { test } from "node:test";
import assert from "node:assert/strict";
import { deflateSync } from "node:zlib";
import { checkPng } from "./check-blank-screenshots.mjs";

const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) {
    c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  }
  return c >>> 0;
});

function crc32(bytes) {
  let c = 0xffffffff;
  for (const b of bytes) {
    c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  }
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(data.length, 0);
  head.write(type, 4, "latin1");
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([head.subarray(4), data])));
  return Buffer.concat([head, data, crc]);
}

// pixel(x, y) が返すバイト列を並べた 8 ビットの PNG を作る。
function makePng({ width = 60, height = 50, colorType = 6, channels = 4, pixel, palette, alpha, ihdr = {} }) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(ihdr.width ?? width, 0);
  header.writeUInt32BE(ihdr.height ?? height, 4);
  header[8] = ihdr.bitDepth ?? 8;
  header[9] = colorType;
  header[10] = ihdr.compression ?? 0;
  header[11] = ihdr.filterMethod ?? 0;
  header[12] = 0;

  const rows = [];
  for (let y = 0; y < height; y++) {
    const row = Buffer.alloc(1 + width * channels);
    for (let x = 0; x < width; x++) {
      Buffer.from(pixel(x, y)).copy(row, 1 + x * channels);
    }
    rows.push(row);
  }

  const parts = [Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]), chunk("IHDR", header)];
  if (palette) {
    parts.push(chunk("PLTE", Buffer.from(palette.flat())));
  }
  if (alpha) {
    parts.push(chunk("tRNS", Buffer.from(alpha)));
  }
  parts.push(chunk("IDAT", deflateSync(Buffer.concat(rows))), chunk("IEND", Buffer.alloc(0)));
  return Buffer.concat(parts);
}

const WHITE = [255, 255, 255, 255];
const GRAY = [200, 200, 200, 255];
const BLACK = [0, 0, 0, 255];

// 上端 20 画素がタイトルバー（灰色）、その下が白い RGBA の画像。dot があればその画素を黒にする。
function windowPng(dot) {
  return makePng({
    pixel: (x, y) => (y < 20 ? GRAY : dot && x === dot[0] && y === dot[1] ? BLACK : WHITE),
  });
}

function withoutTail(buffer, bytes) {
  return buffer.subarray(0, buffer.length - bytes);
}

test("タイトルバーの下が一色なら空白と判定する", () => {
  assert.match(checkPng(windowPng()), /すべて同じ色/);
});

test("タイトルバーの下に 1 画素でも違う色があれば通す", () => {
  assert.equal(checkPng(windowPng([30, 40])), null);
});

test("末尾の IEND の CRC が欠けた PNG は読めない", () => {
  assert.match(checkPng(withoutTail(windowPng([30, 40]), 4)), /読めない PNG \(チャンクが途中で切れている\)/);
});

test("IEND が無い PNG は読めない", () => {
  assert.match(checkPng(withoutTail(windowPng([30, 40]), 12)), /読めない PNG \(IEND が無い/);
});

test("CRC が合わない PNG は読めない", () => {
  const png = Buffer.from(windowPng([30, 40]));
  png[png.length - 1] ^= 0xff;
  assert.match(checkPng(png), /CRC が合わない/);
});

test("IDAT が途中で切れた PNG は読めない", () => {
  assert.match(checkPng(withoutTail(windowPng([30, 40]), 20)), /読めない PNG \(IDAT チャンクが途中で切れている\)/);
});

test("IHDR が仕様に合わない PNG は読めない", () => {
  const pixel = (x, y) => (y < 20 ? GRAY : WHITE);
  assert.match(checkPng(makePng({ pixel, ihdr: { width: 0 } })), /幅か高さが 0/);
  assert.match(checkPng(makePng({ pixel, ihdr: { bitDepth: 4 } })), /許されないビット深度/);
  assert.match(checkPng(makePng({ pixel, ihdr: { compression: 1 } })), /未知の圧縮方式/);
  assert.match(checkPng(makePng({ pixel, ihdr: { filterMethod: 1 } })), /フィルター方式 1/);
});

test("パレットの画像に PLTE が無ければ読めない", () => {
  const png = makePng({ colorType: 3, channels: 1, pixel: (x, y) => [y < 20 ? 1 : 0] });
  assert.match(checkPng(png), /PLTE が無い/);
});

test("パレットの色が RGB だけ同じでもアルファが違えば空白ではない", () => {
  const palette = [[0, 0, 0], [0, 0, 0], [128, 128, 128]];
  const pixel = (x, y) => [y < 20 ? 2 : x === 30 && y === 40 ? 1 : 0];
  // 項目 0 は透明な黒、項目 1 は不透明な黒。
  assert.equal(checkPng(makePng({ colorType: 3, channels: 1, pixel, palette, alpha: [0, 255] })), null);
  // tRNS が無ければ両方とも不透明な黒なので、一色の空白になる。
  assert.match(checkPng(makePng({ colorType: 3, channels: 1, pixel, palette })), /すべて同じ色/);
});
