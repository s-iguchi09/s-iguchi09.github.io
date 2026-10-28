// 記事とコントロールデモの OGP 画像（1200x630 の PNG）を生成する。
//
//   cd tools/og-image && npm ci
//   node generate.mjs           # 新しいページと、内容が変わったページだけ生成する。対応するページが無い PNG は消す
//   node generate.mjs --all     # 全ページを生成し直す（テンプレートを変えたとき）
//   node generate.mjs --check   # 生成・削除・コミットの漏れを一覧し、あれば終了コード 1（ブラウザを起動しない）
//
// 出力先は images/og/<ページの URL>.png（例: /articles/<slug>/ → images/og/articles/<slug>.png）。
// _includes/head.html が同じ規則でパスを組み立て、ファイルが無いページには
// images/og/default.png（日本語のページは images/og/ja/default.png）を使う。
// 生成した内容は images/og/manifest.json に URL ごとのハッシュで記録し、変わったものだけ作り直す。
//
// 描画には Chrome / Edge を使う（OG_BROWSER_PATH で実行ファイルを指定できる）。
// どちらも無い環境では `cd tools/og-image && npx playwright-core install chromium` で Chromium を入れる
// （このディレクトリで実行し、lock に固定した playwright-core に合う版を入れる）。
// フォントは Google Fonts から読み込むため、ネットワークが必要。読み込めなければ中断する。

import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import yaml from "js-yaml";
import { loadDefaultJapaneseParser } from "budoux";

// テンプレートを変えたら上げる。manifest のハッシュが変わり、全ページが作り直される。
const TEMPLATE_VERSION = 2;

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const outDir = path.join(root, "images/og");
const manifestPath = path.join(outDir, "manifest.json");

// コントロールデモの見出しは _layouts/control-demo.html の <title> から接尾辞（| Control Demo など）を除いた文言にする。
// 片方を変えたら、もう片方も合わせる。
const sources = [
  { dir: "_articles_en", lang: "en", url: (n) => `/articles/${n}/`, kind: "article" },
  { dir: "_articles_ja", lang: "ja", url: (n) => `/ja/articles/${n}/`, kind: "article" },
  { dir: "apps/wpf-standard-control-demo", lang: "en", url: null, kind: "demo" },
  { dir: "ja/apps/wpf-standard-control-demo", lang: "ja", url: null, kind: "demo" },
];

const defaults = [
  { url: "/", file: "default.png", lang: "en", cat: "Articles", title: "WPF and .NET technical articles", foot: "" },
  { url: "/ja/", file: "ja/default.png", lang: "ja", cat: "技術記事", title: "WPF と .NET の技術記事", foot: "" },
];

function readFrontMatter(file) {
  const text = fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n");
  const m = text.match(/^---\n([\s\S]*?)\n---/);
  if (!m) throw new Error(`front matter が無い: ${file}`);
  return yaml.load(m[1]);
}

function formatDate(d) {
  // front matter の date は UTC の日付（js-yaml は UTC の 0 時として読む）
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}/${p(d.getUTCMonth() + 1)}/${p(d.getUTCDate())}`;
}

function collectCards() {
  const cards = [];
  for (const src of sources) {
    for (const name of fs.readdirSync(path.join(root, src.dir)).filter((f) => f.endsWith(".md")).sort()) {
      const fm = readFrontMatter(path.join(root, src.dir, name));
      const slug = name.replace(/\.md$/, "");
      if (src.kind === "article") {
        if (!(fm.date instanceof Date)) throw new Error(`date が日付として読めない: ${src.dir}/${name}`);
        // Jekyll の :name はファイル名を slugify した値。英小文字・数字・ハイフンだけなら一致する
        if (!fm.permalink && !/^[a-z0-9-]+$/.test(slug)) throw new Error(`ファイル名から URL を決められない: ${src.dir}/${name}`);
        cards.push({ url: fm.permalink || src.url(slug), lang: src.lang, cat: fm.category, title: fm.title, foot: formatDate(fm.date) });
      } else {
        const ja = src.lang === "ja";
        cards.push({
          url: fm.permalink,
          lang: src.lang,
          cat: ja ? "コントロールデモ" : "Control demo",
          title: ja ? `WPF ${fm.title} の挙動を .NET 10 で実測` : `WPF ${fm.title}: Behavior Measured on .NET 10`,
          foot: ja ? "WPF 標準コントロールデモ" : "WPF Standard Control Demo",
        });
      }
    }
  }
  for (const c of cards) {
    if (!c.url || !c.title || !c.cat) throw new Error(`url / title / category が欠けている: ${JSON.stringify(c)}`);
    c.file = c.url.replace(/^\//, "").replace(/\/$/, "").replace(/\.html$/, "") + ".png";
  }
  return [...cards, ...defaults];
}

const hashOf = (c) =>
  crypto.createHash("sha256").update(JSON.stringify([TEMPLATE_VERSION, c.lang, c.cat, c.title, c.foot])).digest("hex").slice(0, 16);

const escapeHtml = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

const jaParser = loadDefaultJapaneseParser();

function html(c) {
  // 「.NET 10」「.NET Framework 4.8」の名前と番号の間では改行しない（空白を改行しない空白に置き換える）
  const text = escapeHtml(c.title).replace(/\.NET (?=Framework \d|\d)/g, ".NET ").replace(/Framework (?=\d)/g, "Framework ");
  // 日本語は BudouX で文節の切れ目に改行位置を入れ、単語の途中で改行しないようにする
  const title = c.lang === "ja" ? jaParser.translateHTMLString(text) : text;
  return `<!doctype html><html lang="${c.lang}"><head><meta charset="utf-8">
<link href="https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@500;700&family=JetBrains+Mono:wght@500&display=block" rel="stylesheet">
<style>
*{margin:0;box-sizing:border-box}
html,body{width:1200px;height:630px;overflow:hidden}
body{font-family:"Noto Sans JP",sans-serif;background:#e9eef6}
.mono{font-family:"JetBrains Mono","Noto Sans JP",monospace}
.win{position:absolute;inset:40px;border:2px solid #d5deea;border-radius:20px;background:#fff;overflow:hidden}
.tab{height:64px;border-bottom:2px solid #d5deea;background:#f5f7fb;display:flex;align-items:center;gap:14px;padding:0 28px}
.dot{width:18px;height:18px;border-radius:50%}
.file{margin-left:20px;font-size:24px;color:#667085}
.body{position:absolute;left:64px;right:64px;top:110px;bottom:56px;display:flex;flex-direction:column;justify-content:space-between}
.cmt{font-size:28px;color:#2563eb}
.title{font-weight:700;line-height:1.3;color:#1f2933;max-height:300px;overflow:hidden;overflow-wrap:anywhere;line-break:strict}
.foot{display:flex;justify-content:flex-end;font-size:24px;color:#667085;min-height:32px}
</style></head><body>
<div class="win"><div class="tab"><span class="dot" style="background:#f87171"></span><span class="dot" style="background:#fbbf24"></span><span class="dot" style="background:#34d399"></span><span class="file mono">s-iguchi09.github.io</span></div>
<div class="body"><div class="cmt mono">// ${escapeHtml(c.cat)}</div><div class="title" id="t">${title}</div><div class="foot mono">${escapeHtml(c.foot)}</div></div></div>
</body></html>`;
}

async function launch() {
  const { chromium } = await import("playwright-core");
  if (process.env.OG_BROWSER_PATH) return chromium.launch({ executablePath: process.env.OG_BROWSER_PATH });
  for (const channel of ["chrome", "msedge"]) {
    try {
      return await chromium.launch({ channel });
    } catch {
      // 次の候補を試す
    }
  }
  return chromium.launch(); // `npx playwright-core install chromium` で入れた Chromium
}

async function render(page, c, dest) {
  const failed = [];
  const onFail = (req) => {
    if (/fonts\.(googleapis|gstatic)\.com/.test(req.url())) failed.push(req.url());
  };
  page.on("requestfailed", onFail);
  try {
    await page.setContent(html(c), { waitUntil: "networkidle" });
    if (failed.length) throw new Error(`Web フォントを読み込めなかった: ${failed[0]}`);
    await page.evaluate(async () => {
      // CSS が取れないと @font-face が 0 件になり、document.fonts.check() は true を返してしまう。
      // face があることと、実際の文字で読み込めたことを確かめる。
      const families = new Set([...document.fonts].map((f) => f.family.replace(/"/g, "")));
      for (const name of ["Noto Sans JP", "JetBrains Mono"]) {
        if (!families.has(name)) throw new Error(`Web フォントが登録されていない: ${name}`);
      }
      const all = document.body.textContent;
      const loaded = [
        await document.fonts.load('700 64px "Noto Sans JP"', all),
        await document.fonts.load('500 24px "Noto Sans JP"', all),
        await document.fonts.load('500 24px "JetBrains Mono"', "s-iguchi09.github.io //0123456789"),
      ];
      if (loaded.some((faces) => faces.length === 0)) throw new Error("Web フォントを読み込めなかった");
      await document.fonts.ready;

      // 収まるまで文字を小さくする（下限 34px）
      const t = document.getElementById("t");
      let size = 64;
      t.style.fontSize = size + "px";
      while (t.scrollHeight > 300 && size > 34) {
        size -= 2;
        t.style.fontSize = size + "px";
      }
      if (t.scrollHeight > 300) throw new Error("下限の文字サイズでもタイトルが収まらない");
    });
  } finally {
    page.off("requestfailed", onFail);
  }
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  await page.screenshot({ path: dest, type: "png" });
}

// images/og 配下の PNG を、images/og からの相対パス（/ 区切り）で列挙する
function listPngs(dir = outDir, prefix = "") {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? listPngs(path.join(dir, e.name), `${prefix}${e.name}/`) : e.name.endsWith(".png") ? [`${prefix}${e.name}`] : []
  );
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const cards = collectCards();
  const manifest = fs.existsSync(manifestPath) ? JSON.parse(fs.readFileSync(manifestPath, "utf8")) : {};
  const stale = cards.filter((c) => args.has("--all") || manifest[c.url] !== hashOf(c) || !fs.existsSync(path.join(outDir, c.file)));
  const expected = new Set(cards.map((c) => c.file));
  const orphans = listPngs().filter((f) => !expected.has(f));

  if (args.has("--check")) {
    for (const c of stale) console.log(`要生成: ${c.url}`);
    for (const f of orphans) console.log(`対応するページが無い: images/og/${f}`);
    // 生成してもコミットし忘れると公開されないので、作業ツリーとの差も見る
    const dirty = execFileSync("git", ["status", "--porcelain", "--", "images/og"], { cwd: root, encoding: "utf8" }).trim();
    if (dirty) console.log(`コミットされていない変更がある:\n${dirty}`);
    process.exitCode = stale.length || orphans.length || dirty ? 1 : 0;
    return;
  }

  for (const f of orphans) {
    fs.rmSync(path.join(outDir, f));
    console.log(`削除: images/og/${f}`);
  }
  if (stale.length) {
    const browser = await launch();
    try {
      const page = await browser.newPage({ viewport: { width: 1200, height: 630 } });
      for (const c of stale) {
        await render(page, c, path.join(outDir, c.file));
        manifest[c.url] = hashOf(c);
        console.log(`生成: images/og/${c.file}`);
      }
    } finally {
      await browser.close();
    }
  } else {
    console.log("生成が必要なページはありません");
  }

  // 削除したページの記録を残さない
  const urls = new Set(cards.map((c) => c.url));
  const sorted = Object.fromEntries(Object.entries(manifest).filter(([u]) => urls.has(u)).sort(([a], [b]) => a.localeCompare(b)));
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(manifestPath, JSON.stringify(sorted, null, 2) + "\n");
}

main().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
