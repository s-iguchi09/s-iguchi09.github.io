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
 * 唯一の例外が index-hold.json で、ここには「導線をあえて張らない記事」を書く。
 * これはユーザーの意図であって API からは読めないため、台帳でしか表現できない。
 * 代わりに until を必須にし、過ぎたら保留を自動解除して判定を促す。放置されても
 * 「保留中です」と嘘をつき続けることがない。
 *
 * なぜこれが要るのか:
 * このサイトは Search Console のサイトマップ取得が 5 形式すべて失敗しており、
 * 外部リンクも 0 件でクロールバジェットがほとんど無い。放置した記事は
 * 一覧ページからリンクされていても自然発見されなかった(2026-09-09 JST 実測)。
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

// slug に使える文字。引数の検証(SLUG_RE)と dev.to 本文からの URL 抽出(linkedSlugs)で
// 同じ定義を使う。食い違うと、抽出できない slug が「まだリンクされていない」と誤判定され、
// 保留中の記事の汚染検出まで静かに漏れる。
const SLUG_CHARS = '[A-Za-z0-9][A-Za-z0-9._-]*';

// slug は記事ファイル名とそのまま結合するので、パス区切りや .. を弾く。
// 通さないと _articles_en の外を読んだり、--out の外へ書いたりできてしまう。
const SLUG_RE = new RegExp(`^${SLUG_CHARS}$`);

const HOLD_FILE = path.join(__dirname, 'index-hold.json');
const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * 実行環境の TZ に関わらず JST の日付を返す。
 * このサイトの運用日付(記事の date、Search Console の確認日)はすべて JST なので、
 * UTC で判定すると日本時間の午前中に 1 日ずれて保留が早く切れる。
 */
function todayJst() {
  return new Date(Date.now() + 9 * 3600 * 1000).toISOString().slice(0, 10);
}

/**
 * YYYY-MM-DD として実在する日かを見る。
 *
 * 形式だけ見ると 2026-02-30 が通ってしまう。V8 はこれを 2026-03-02 として
 * 解釈するので daysLeft が 2 日ずれるうえ、expired は文字列比較なので
 * "2026-02-30" < today が成立し、**保留が黙って解除される**。日付を作り直して
 * 元の文字列に戻るかで弾く(うるう年も 2024-02-29 は通り 2026-02-29 は落ちる)。
 */
function isRealDate(s) {
  if (!DATE_RE.test(s)) return false;
  const d = new Date(`${s}T00:00:00Z`);
  return !Number.isNaN(d.getTime()) && d.toISOString().slice(0, 10) === s;
}

const daysBetween = (from, to) =>
  Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86400000);

/**
 * インデックス導線をあえて張らない記事を読む。
 *
 * 「手動登録をやめてよいか」の判定は、登録も dev.to 追記もしていない新記事が
 * 自然に拾われるかでしか測れない。2026-09-19 の判定記事は、このワークフローが
 * 例外なく導線を提示したせいで日英とも登録され、判定が無効になった。
 *
 * until が壊れているものは保留のままにする。解除の側に倒すと事故が再発するので、
 * 迷ったら「出さない」に倒す。
 *
 * slug は引数と同じ規則で検証し、外れたら例外で止める。parseArgs は末尾の / を
 * 落とすので、台帳に "foo/" と書くと heldNow には "foo/" が入り、引数から来た
 * "foo" と一致せず**保留が黙って効かない**。保留が効かないまま導線を出すくらいなら
 * 止まったほうがよいので、警告で済ませずに throw する。
 */
function readHolds() {
  // 台帳が無いのを「保留が無い」と読み替えない。読み替えると heldNow が空になり、
  // 保留中の記事にも導線が出る。ファイルはスキルに同梱されているので、無いのは
  // 取り違えか削除であって、そこで止めたほうがよい。
  if (!fs.existsSync(HOLD_FILE)) {
    throw new Error(
      `${path.basename(HOLD_FILE)} が見つからない。保留を黙って無効化しないために止めた。` +
        '保留が無いときも {"holds": []} のファイルを置く。'
    );
  }
  let parsed;
  try {
    parsed = JSON.parse(fs.readFileSync(HOLD_FILE, 'utf8'));
  } catch (err) {
    throw new Error(`${path.basename(HOLD_FILE)} を読めない: ${err.message}`);
  }
  // holds が配列でなければ止める。黙って [] にすると heldNow が空になり、
  // 台帳に書いたはずの保留が全件無効のまま導線が出る。台帳が読めない状態と
  // 「保留が無い状態」は区別が付かないので、区別が付くように落とす。
  const holds = parsed && parsed.holds;
  if (!Array.isArray(holds)) {
    throw new Error(
      `${path.basename(HOLD_FILE)} の holds が配列でない。保留を黙って無効化しないために止めた。` +
        '保留が無いときも "holds": [] と明示する。'
    );
  }
  const today = todayJst();
  return holds
    .map((h, i) => {
      const slug = h && typeof h.slug === 'string' ? h.slug : '';
      if (!SLUG_RE.test(slug) || slug.includes('..')) {
        throw new Error(
          `${path.basename(HOLD_FILE)} の holds[${i}] の slug が不正: ${JSON.stringify(slug)}。` +
            '引数と同じ規則で書く(末尾の / も付けない)。保留が効かないまま導線を出さないために止めた。'
        );
      }
      const valid = isRealDate(h.until || '');
      if (!valid) {
        console.error(`warning: ${slug} の until が実在する YYYY-MM-DD でない。保留のまま扱う。`);
      }
      return {
        slug,
        until: h.until || '(未設定)',
        reason: h.reason || '(理由の記載なし)',
        expired: valid && h.until < today,
        daysLeft: valid ? daysBetween(today, h.until) : null,
      };
    });
}

/**
 * 保留中の記事が既に dev.to からリンクされていないか調べる。
 *
 * 事故は「別の記事を処理しているセッションが、ついでに判定記事へ 1 行足す」形で
 * 起きた。引数に判定記事が含まれない実行でも気づけるよう、保留は毎回全件見る。
 */
function reportHoldStatus(holds, posts) {
  if (holds.length === 0) return false;
  console.log('=== インデックス導線を保留中の記事 ===\n');
  let broken = false;
  for (const h of holds) {
    if (h.expired) {
      console.log(`${h.slug}: 判定日 ${h.until} を過ぎた → 保留は解除済み`);
      console.log(`  ${h.reason}`);
      console.log('  Search Console の URL 検査で日英 2 URL を調べ、インデックス状況を記録する。');
      console.log(`  判定を書き留めたら ${path.basename(HOLD_FILE)} の holds から削除する。\n`);
      continue;
    }
    const left = h.daysLeft === null ? '期限不明' : `あと ${h.daysLeft} 日`;
    console.log(`${h.slug}: 判定日 ${h.until}（${left}）`);
    console.log(`  ${h.reason}`);
    console.log('  → Search Console への登録も dev.to への追記もしない。');
    if (posts) {
      const linked = posts.filter((p) => p.links.has(h.slug));
      if (linked.length > 0) {
        broken = true;
        console.log('  !! dev.to から既にリンクされている。判定条件が壊れている:');
        for (const p of linked) console.log(`     ${p.title}\n       ${p.url}`);
        console.log('     リンクを消しても発見済みの事実は戻らない。別の記事で判定をやり直す。');
      } else {
        console.log('  dev.to からのリンク: なし（正常）');
      }
    } else {
      // 黙って飛ばすと「確認した結果なし」と区別が付かない。--offline や API 失敗で
      // 汚染を見逃した回こそ、見ていないと言う必要がある。
      console.log('  dev.to の状態を取得していないので、汚染は確認できていない。');
    }
    console.log('');
  }
  return broken;
}

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
  const re = new RegExp(`https://s-iguchi09\\.github\\.io/(?:ja/)?articles/(${SLUG_CHARS})/`, 'g');
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
    // getJson は任意の JSON 値を返す。文字列が返ると chunk.length === 0 が成立して
    // 「投稿なし」に化け、汚染を見逃したまま「なし（正常）」と報告してしまう。
    if (!Array.isArray(chunk)) {
      console.error('warning: dev.to の投稿一覧が配列でない。リンク判定が不完全なので未取得として扱う。');
      return null;
    }
    if (chunk.length === 0) break;
    // 要素が壊れていると item.id で TypeError になったり /undefined を叩いたりして、
    // 明示的な未取得の経路を通らずに例外へ落ちる。結果は同じ「未取得」でも、
    // 何が起きたか分からなくなるので入口で弾く。
    // id は詳細 API の URL にそのまま入るので、正の整数であることまで見る。
    // != null だけだと {} や "x" や 0 が通り、/articles/[object Object] を叩く。
    if (!chunk.every((item) => item && typeof item === 'object' && Number.isInteger(item.id) && item.id > 0)) {
      console.error('warning: dev.to の投稿一覧に不正な要素がある。リンク判定が不完全なので未取得として扱う。');
      return null;
    }
    list.push(...chunk);
    if (chunk.length < PER_PAGE) break;
    if (page >= MAX_PAGES) {
      // 部分配列を返すと reportHoldStatus が完全な一覧として扱い、未取得ページに
      // あるリンクを「なし（正常）」と報告してしまう。不完全なら未取得と同じ扱いにする。
      console.error(`warning: ${MAX_PAGES} ページ (${list.length} 件) で打ち切った。取得しきれていないので未取得として扱う。`);
      return null;
    }
  }

  const posts = [];
  for (const item of list) {
    const d = await getJson(`${DEVTO_API}/articles/${item.id}`);
    // 本文が読めない投稿が 1 つでもあると、そこに張られたリンクを見落とす。
    // 「リンクなし」と「本文を読めていない」は区別が付かないので、未取得に倒す。
    if (!d || typeof d.body_markdown !== 'string') {
      console.error(`warning: dev.to の投稿 ${item.id} の本文を取得できない。リンク判定が不完全なので未取得として扱う。`);
      return null;
    }
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
 * 失敗し、図が壊れて alt だけが残る(2026-09-11 JST 実測)。data URI での
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

    // alt も caption も、改行が残っていると 2 行目以降に "> " が付かず
    // 引用ブロックの外へ出てしまう。どちらも 1 行へ潰す。
    const alt = ((img[0].match(/\balt="([^"]*)"/) || [])[1] || '').replace(/\s+/g, ' ').trim();
    const caption = ((block.match(/<figcaption\b[^>]*>([\s\S]*?)<\/figcaption>/) || [])[1] || '')
      .replace(/\s+/g, ' ').trim();

    const lines = [];
    if (alt) lines.push(`**Figure:** ${alt}`);
    if (caption) lines.push('', caption);
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
  const holds = readHolds();

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
    if (!posts) {
      // 投稿一覧は出せなくても、保留の一覧と「汚染を確認できていない」ことは伝わる。
      // 先に出してから落とす。黙って落とすと、保留の存在ごと見えなくなる。
      reportHoldStatus(holds, null);
      throw new Error('dev.to の状態を取得できなかったため投稿一覧は出せない');
    }
    // 汚染の扱いは通常モードと揃える。--status で握り潰すと、状況確認のつもりで
    // 実行したときだけ異常が終了コードに出ない。
    if (reportHoldStatus(holds, posts)) process.exitCode = 1;
    printStatus(posts);
    return;
  }

  // 保留は引数に関わらず全件出す。事故は「別の記事の作業のついでに判定記事へ
  // リンクを足す」形で起きたので、判定記事を指定していない実行でこそ目に入る必要がある。
  // 汚染は黙って流すと気づかれないので、出力は最後まで出したうえで異常終了させる。
  if (reportHoldStatus(holds, posts)) process.exitCode = 1;
  const heldNow = new Set(holds.filter((h) => !h.expired).map((h) => h.slug));
  const active = slugs.filter((s) => !heldNow.has(s));

  if (active.length === 0) {
    console.log('指定された記事はすべて保留中のため、導線は出さない。');
    console.log(`判定日まで待つか、方針を変えるなら ${path.basename(HOLD_FILE)} を編集する。`);
    return;
  }
  if (active.length < slugs.length) {
    console.log(`保留中の ${slugs.length - active.length} 本を除いて続ける。\n`);
  }

  console.log('=== Search Console に登録する URL ===');
  console.log('URL 検査の検索窓に貼る → 「インデックス登録をリクエスト」（1 日 10 件まで）\n');
  for (const slug of active) {
    readArticle(slug, 'en');
    readArticle(slug, 'ja');
    console.log(`${SITE}/articles/${slug}/`);
    console.log(`${SITE}/ja/articles/${slug}/`);
  }

  console.log('\n=== dev.to からのリンク ===\n');
  for (const slug of active) {
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
  for (const slug of active) {
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
