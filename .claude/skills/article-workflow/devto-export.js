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
    else if (a === '--out') outDir = argv[++i];
    else if (a.startsWith('-')) throw new Error(`不明なオプション: ${a}`);
    else slugs.push(a.replace(/\/$/, ''));
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

/** dev.to の公開投稿と、その本文が張っている自サイト記事を集める。 */
async function fetchDevtoPosts() {
  const listRes = await fetch(`${DEVTO_API}/articles?username=${DEVTO_USER}&per_page=100`);
  if (!listRes.ok) throw new Error(`dev.to API がエラーを返した: ${listRes.status}`);
  const list = await listRes.json();

  const posts = [];
  for (const item of list) {
    const detailRes = await fetch(`${DEVTO_API}/articles/${item.id}`);
    if (!detailRes.ok) throw new Error(`記事 ${item.id} の取得に失敗: ${detailRes.status}`);
    const d = await detailRes.json();
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
  return (head + absolutize(body).trim() + '\n').replace(/\r\n/g, '\n');
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
