#!/usr/bin/env node
/**
 * 記事公開後の導線を用意する。
 *
 *   node devto-export.js <slug> [<slug> ...] [--export] [--out <dir>] [--offline]
 *   node devto-export.js --status
 *
 * 既定では次を標準出力に書く。
 *
 *   1. Search Console の URL 検査に貼る日英 2 URL
 *   2. その記事が dev.to から既にリンクされているか
 *   3. まだなら、追記先の候補と貼り付ける 1 行
 *
 * --export を付けたときだけ、dev.to へ新規転載するための Markdown を書き出す。
 * 転載は毎記事やるものではない(詳細は SKILL.md の Phase 6.2)。
 *
 * どの記事を dev.to に出したかは記録を持たない。dev.to の公開 API が認証なしで
 * 投稿一覧と body_markdown を返すので、毎回そこから実状を読む。台帳を置くと
 * 更新漏れで嘘をつくが、API は常に本当のことを言う。
 *
 * なぜこれが要るのか:
 * このサイトは Search Console のサイトマップ取得が 5 形式すべて失敗しており、
 * 外部リンクも 0 件でクロールバジェットがほとんど無い。放置した記事は
 * 一覧ページからリンクされていても自然発見されなかった(2026-09-09 実測)。
 */

'use strict';

const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..', '..');
const SITE = 'https://s-iguchi09.github.io';
const DEVTO_USER = 's-iguchi09';
const DEVTO_API = 'https://dev.to/api';

// dev.to の front matter で使える項目。description は存在しないので書かない。
const TAGS_BY_CATEGORY = {
  WPF: 'wpf, dotnet, csharp',
  'C#': 'csharp, dotnet, linq',
};
const DEFAULT_TAGS = 'dotnet, csharp';

// slug は記事ファイル名とそのまま結合するので、パス区切りや .. を弾く。
// 通さないと _articles_en の外を読んだり、--out の外へ書いたりできてしまう。
const SLUG_RE = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;

function parseArgs(argv) {
  const slugs = [];
  let exportMd = false;
  let statusOnly = false;
  let offline = false;
  let outDir = process.cwd();
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--export') exportMd = true;
    else if (a === '--status') statusOnly = true;
    else if (a === '--offline') offline = true;
    else if (a === '--out') {
      const v = argv[++i];
      // 値を取らずに次のオプションを食うと、出力せず正常終了して気づけない。
      if (v === undefined || v.startsWith('-')) {
        throw new Error('--out には出力先ディレクトリを指定する');
      }
      outDir = v;
    } else if (a.startsWith('-')) throw new Error(`不明なオプション: ${a}`);
    else {
      const s = a.replace(/\/$/, '');
      if (!SLUG_RE.test(s) || s.includes('..')) {
        throw new Error(`slug に使えない文字が含まれている: ${a}`);
      }
      slugs.push(s);
    }
  }
  if (slugs.length === 0 && !statusOnly) {
    throw new Error('slug を 1 つ以上指定するか --status を付ける。例: node devto-export.js wpf-scrollviewer-not-scrolling');
  }
  return { slugs, exportMd, statusOnly, offline, outDir };
}

function splitFrontMatter(raw) {
  const m = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]*)$/);
  if (!m) throw new Error('front matter が見つからない');
  const fm = {};
  for (const line of m[1].split(/\r?\n/)) {
    const kv = line.match(/^([a-z_]+):\s*(.*)$/);
    if (kv) fm[kv[1]] = kv[2].trim();
  }
  return { fm, body: m[2] };
}

const unquote = (s) => (s && s.startsWith('"') && s.endsWith('"') ? s.slice(1, -1) : s);

function readArticle(slug, lang) {
  const file = path.join(REPO, `_articles_${lang}`, `${slug}.md`);
  if (!fs.existsSync(file)) throw new Error(`記事が見つからない: ${file}`);
  return splitFrontMatter(fs.readFileSync(file, 'utf8'));
}

/**
 * サイトルート基準の相対パスを絶対 URL にする。
 * dev.to は別ドメインなので、相対のままでは画像もリンクも壊れる。
 */
function absolutize(body) {
  return body
    .replace(/src="\/(images|articles|en)\//g, `src="${SITE}/$1/`)
    .replace(/href="\/(images|articles|en)\//g, `href="${SITE}/$1/`)
    .replace(/\]\(\/(images|articles|en)\//g, `](${SITE}/$1/`);
}

function linkedSlugs(text) {
  const set = new Set();
  const re = /https:\/\/s-iguchi09\.github\.io\/(?:ja\/)?articles\/([a-z0-9-]+)\//g;
  let m;
  while ((m = re.exec(text)) !== null) set.add(m[1]);
  return set;
}

// Node の fetch はリクエスト全体のタイムアウトを持たない(undici の
// headersTimeout などは別物)。応答が返らないと待ち続けるので、
// AbortSignal.timeout で上限を切る。
const FETCH_TIMEOUT_MS = 15000;

function getJson(url) {
  return fetch(url, { signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) }).then((res) => {
    if (!res.ok) throw new Error(`dev.to API がエラーを返した: ${res.status} (${url})`);
    return res.json();
  });
}

/** dev.to の公開投稿と、その本文が張っている自サイト記事を集める。 */
async function fetchDevtoPosts() {
  // 1 ページ 100 件までしか返らない。全部読まないと、投稿が増えたときに
  // 「まだリンクされていない」と誤判定して同じリンクを二重に足すことになる。
  const list = [];
  const PER_PAGE = 100;
  // 上限は無限ループへの保険。API が終端を返さない異常時に備えるだけなので、
  // 打ち切ったときは黙って欠落させず警告を出す。
  const MAX_PAGES = 100;
  for (let page = 1; ; page++) {
    const chunk = await getJson(`${DEVTO_API}/articles?username=${DEVTO_USER}&per_page=${PER_PAGE}&page=${page}`);
    if (chunk.length === 0) break;
    list.push(...chunk);
    if (chunk.length < PER_PAGE) break;
    if (page >= MAX_PAGES) {
      console.error(`warning: ${MAX_PAGES} ページ (${list.length} 件) で打ち切った。取得しきれていない可能性がある。`);
      break;
    }
  }

  const posts = [];
  for (const item of list) {
    const d = await getJson(`${DEVTO_API}/articles/${item.id}`);
    posts.push({
      id: d.id,
      title: d.title,
      url: d.url,
      // 一覧 API の tag_list は配列だが、詳細 API では "wpf, dotnet" という
      // 文字列で返る。配列で来る tags を優先し、無ければ文字列を割る。
      tags: Array.isArray(d.tags)
        ? d.tags
        : String(d.tag_list || '').split(',').map((t) => t.trim()).filter(Boolean),
      canonical: d.canonical_url,
      links: linkedSlugs(d.body_markdown || ''),
    });
  }
  return posts;
}

/**
 * SVG を参照している figure を、説明文の引用ブロックへ置き換える。
 *
 * dev.to の画像プロキシは SVG を変換できず、中身を SVG のまま
 * Content-Type: image/webp で返す。ブラウザは webp としてデコードを試みて
 * 失敗し、図が壊れて alt だけが残る(2026-09-11 実測)。data URI での
 * 埋め込みは "Invalid markdown detected!" で拒否された。
 *
 * alt と figcaption には図の内容がそのまま書かれているので、テキストとして
 * 残せば情報は失われない。あわせて元記事へのリンクが増えるため、
 * 外部リンクを得るという転載の目的にも沿う。PNG はプロキシが正しく変換する
 * ので手を触れない。
 */
function replaceSvgFigures(body, slug) {
  return body.replace(/<figure\b[^>]*>[\s\S]*?<\/figure>/g, (block) => {
    const img = block.match(/<img\b[^>]*>/);
    if (!img || !/\bsrc="[^"]*\.svg"/i.test(img[0])) return block;

    const alt = (img[0].match(/\balt="([^"]*)"/) || [])[1] || '';
    const caption = (block.match(/<figcaption\b[^>]*>([\s\S]*?)<\/figcaption>/) || [])[1] || '';

    const lines = [];
    if (alt.trim()) lines.push(`**Figure:** ${alt.trim()}`);
    if (caption.trim()) lines.push('', caption.replace(/\s+/g, ' ').trim());
    lines.push('', `The diagram is rendered in the [original article](${SITE}/articles/${slug}/).`);
    return lines.map((l) => (l ? `> ${l}` : '>')).join('\n');
  });
}

function buildExport(slug) {
  const { fm, body } = readArticle(slug, 'en');
  const title = unquote(fm.title);
  const head = [
    '---',
    `title: "${title.replace(/"/g, '\\"')}"`,
    'published: false',
    `tags: ${TAGS_BY_CATEGORY[fm.category] || DEFAULT_TAGS}`,
    fm.image ? `cover_image: ${SITE}${fm.image}` : null,
    `canonical_url: ${SITE}/articles/${slug}/`,
    '---',
    '',
    `> Originally published at [s-iguchi09.github.io](${SITE}/articles/${slug}/).`,
    `> A [Japanese version](${SITE}/ja/articles/${slug}/) is also available.`,
    '',
    '',
  ].filter((x) => x !== null).join('\n');

  // CRLF が 1 つでも混ざると dev.to は front matter を解釈せず、先頭の --- が
  // 水平線に、末尾の --- が直前行を h2 にする setext heading になる。
  // canonical_url が効かないまま公開されるので、必ず LF に揃える。
  const converted = replaceSvgFigures(absolutize(body), slug);
  return (head + converted.trim() + '\n').replace(/\r\n/g, '\n');
}

function printStatus(posts) {
  console.log('=== dev.to の投稿状況 ===\n');
  if (posts.length === 0) {
    console.log('投稿がまだ無い。まず 1 本を --export で転載し、リンクの受け皿を作る。');
    return;
  }
  for (const p of posts) {
    console.log(`${p.title}`);
    console.log(`  ${p.url}`);
    console.log(`  タグ: ${p.tags.join(', ') || '(なし)'}`);
    console.log(`  リンクしている自サイト記事: ${p.links.size} 本`);
  }
  const all = new Set();
  for (const p of posts) for (const s of p.links) all.add(s);
  console.log(`\n合計 ${posts.length} 投稿から ${all.size} 記事へリンクしている。`);
}

/** タグが重なり、かつリンク数が少ない投稿を追記先として薦める。 */
function suggestTargets(posts, wantTags) {
  const want = new Set(wantTags.split(',').map((t) => t.trim()));
  return posts
    .map((p) => ({ post: p, overlap: p.tags.filter((t) => want.has(t)).length }))
    .sort((a, b) => b.overlap - a.overlap || a.post.links.size - b.post.links.size)
    .slice(0, 3);
}

async function main() {
  const { slugs, exportMd, statusOnly, offline, outDir } = parseArgs(process.argv.slice(2));

  let posts = null;
  if (!offline) {
    try {
      posts = await fetchDevtoPosts();
    } catch (err) {
      console.error(`warning: dev.to の状態を取得できなかった (${err.message})`);
      console.error('warning: リンク済みかどうかの判定は省略する。\n');
    }
  }

  if (statusOnly) {
    if (!posts) throw new Error('dev.to の状態を取得できなかったため --status は実行できない');
    printStatus(posts);
    return;
  }

  console.log('=== Search Console に登録する URL ===');
  console.log('URL 検査の検索窓に貼る → 「インデックス登録をリクエスト」（1 日 10 件まで）\n');
  for (const slug of slugs) {
    readArticle(slug, 'en');
    readArticle(slug, 'ja');
    console.log(`${SITE}/articles/${slug}/`);
    console.log(`${SITE}/ja/articles/${slug}/`);
  }

  console.log('\n=== dev.to からのリンク ===\n');
  for (const slug of slugs) {
    const { fm } = readArticle(slug, 'en');
    const title = unquote(fm.title);
    const tags = TAGS_BY_CATEGORY[fm.category] || DEFAULT_TAGS;

    if (!posts) {
      console.log(`${slug}: 追記する行`);
      console.log(`  - [${title}](${SITE}/articles/${slug}/)\n`);
      continue;
    }

    const already = posts.filter((p) => p.links.has(slug));
    if (already.length > 0) {
      console.log(`${slug}: 既にリンク済み`);
      for (const p of already) console.log(`  ← ${p.title}\n    ${p.url}`);
      console.log('');
      continue;
    }

    console.log(`${slug}: まだどこからもリンクされていない`);
    if (posts.length === 0) {
      console.log('  dev.to に投稿が無い。--export で転載する。\n');
      continue;
    }
    console.log('  追記先の候補（タグの重なり順、同点ならリンク数の少ない順）:');
    for (const { post, overlap } of suggestTargets(posts, tags)) {
      console.log(`    ${post.title}`);
      console.log(`      ${post.url}  (タグ一致 ${overlap} / リンク ${post.links.size} 本)`);
    }
    console.log('  Edit → 末尾の Related Articles にこの行を追記して Save changes:');
    console.log(`    - [${title}](${SITE}/articles/${slug}/)\n`);
  }

  if (!exportMd) {
    console.log('（新規転載する場合は --export を付けて実行する）');
    return;
  }

  console.log('=== dev.to へ新規転載する Markdown ===');
  fs.mkdirSync(outDir, { recursive: true });
  for (const slug of slugs) {
    const dest = path.join(outDir, `devto-${slug}.md`);
    fs.writeFileSync(dest, buildExport(slug), 'utf8');
    console.log(dest);
  }
  console.log('\npublished: false で出力してある。dev.to に貼り、プレビューで');
  console.log('タイトルとタグが認識されているのを確認してから published: true にする。');
}

main().catch((err) => {
  console.error(`error: ${err.message}`);
  // 未完了の fetch を抱えたまま process.exit すると libuv が assertion で落ちる。
  process.exitCode = 1;
});
