// 更新前後の sitemap.xml を比べ、追加・更新・削除された URL を IndexNow に送る。
//
// 使い方: node submit-indexnow.mjs <before.xml> <after.xml> [--dry-run]
//   INDEXNOW_KEY_FILE にリポジトリ直下のキーファイル名を渡す。
//   --dry-run では送信せず、送る予定の URL だけを表示する。
//
// IndexNow は Bing・Yandex などが対応するプロトコルで、Google は対応していない。
// 送信はあくまで補助であり、失敗しても sitemap の更新自体は成功している。
// そのため失敗は ::warning:: で知らせるだけにし、終了コードは 0 のままにする。

import { readFileSync } from 'node:fs';
import { basename } from 'node:path';
import { pathToFileURL } from 'node:url';

const ENDPOINT = 'https://api.indexnow.org/indexnow';
const DEPLOY_WAIT_MS = 10 * 60 * 1000;
const DEPLOY_POLL_MS = 20 * 1000;
// 応答が返らない接続で待ち続けないよう、通信は 1 回ごとに打ち切る。
const REQUEST_TIMEOUT_MS = 30 * 1000;
// IndexNow が 1 回の送信で受け付ける URL の上限。
const MAX_URLS_PER_REQUEST = 10000;

const args = process.argv.slice(2);
const dryRun = args.includes('--dry-run');
const [beforePath, afterPath] = args.filter(a => a !== '--dry-run');

function warn(message) {
  console.log(`::warning::${message}`);
}

// <url> ブロックごとに <loc> と <lastmod> を取り出す。
// 生成元は generate_sitemap.py で、書式は固定なので正規表現で足りる。
export function parseSitemap(xml) {
  const entries = new Map();
  for (const block of xml.matchAll(/<url>([\s\S]*?)<\/url>/g)) {
    const loc = block[1].match(/<loc>([^<]+)<\/loc>/)?.[1]?.trim();
    const lastmod = block[1].match(/<lastmod>([^<]+)<\/lastmod>/)?.[1]?.trim() ?? '';
    if (loc) entries.set(loc, lastmod);
  }
  return entries;
}

// 追加・更新された URL に加え、削除された URL も送る（IndexNow は削除の通知も受け付ける）。
export function diffSitemaps(beforeXml, afterXml) {
  const before = parseSitemap(beforeXml);
  const after = parseSitemap(afterXml);
  const updated = [...after].filter(([loc, lastmod]) => before.get(loc) !== lastmod);
  const removed = [...before.keys()].filter(loc => !after.has(loc));
  return { updated, removed };
}

function readKey() {
  const keyFile = process.env.INDEXNOW_KEY_FILE;
  if (!keyFile) throw new Error('INDEXNOW_KEY_FILE が未設定である');
  const key = readFileSync(keyFile, 'utf8').trim();
  // IndexNow のキーは 8〜128 文字の英数字とハイフン。ファイル名はキーと一致させる決まりである。
  if (!/^[A-Za-z0-9-]{8,128}$/.test(key)) throw new Error(`キーの形式が不正である: ${keyFile}`);
  if (basename(keyFile) !== `${key}.txt`) throw new Error(`キーファイル名がキーと一致しない: ${keyFile}`);
  return key;
}

// push 直後は GitHub Pages のデプロイが終わっておらず、新しい URL はまだ 404 を返す。
// 公開中の sitemap.xml で、更新した URL の lastmod が今回の値以上になるまで待ってから送る。
// 完全一致で待つと、待機中に次の push が公開された場合にいつまでも一致しない。
// 削除した URL は、公開中の sitemap から消えていることも確かめる。
// lastmod は generate_sitemap.py が UTC の固定書式（YYYY-MM-DDThh:mm:ssZ）で出すため、文字列の比較で前後を判定できる。
export function isDeployed(liveXml, updated, removed) {
  const live = parseSitemap(liveXml);
  return updated.every(([loc, lastmod]) => live.has(loc) && live.get(loc) >= lastmod)
    && removed.every(loc => !live.has(loc));
}

// 期限は各通信（本文の読み取りを含む）と待機の両方に効かせる。
// 期限を過ぎた時点で false を返し、応答の無い接続で止まり続けない。
export async function waitForDeploy(siteSitemapUrl, updated, removed, {
  waitMs = DEPLOY_WAIT_MS,
  pollMs = DEPLOY_POLL_MS,
  requestTimeoutMs = REQUEST_TIMEOUT_MS,
} = {}) {
  const deadline = Date.now() + waitMs;
  for (;;) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) return false;
    try {
      const res = await fetch(`${siteSitemapUrl}?t=${Date.now()}`, {
        cache: 'no-store',
        signal: AbortSignal.timeout(Math.min(remaining, requestTimeoutMs)),
      });
      if (res.ok && isDeployed(await res.text(), updated, removed)) return true;
    } catch {
      // 通信エラーと打ち切りは、期限まで待機を続ける
    }
    const left = deadline - Date.now();
    if (left <= 0) return false;
    await new Promise(r => setTimeout(r, Math.min(pollMs, left)));
  }
}

export function chunk(items, size) {
  const chunks = [];
  for (let i = 0; i < items.length; i += size) chunks.push(items.slice(i, i + size));
  return chunks;
}

// 上限を超える場合は分割して送る。失敗は送信単位ごとに警告し、残りの送信は続ける。
async function submit(base, urls) {
  const batches = chunk(urls, MAX_URLS_PER_REQUEST);
  for (const [i, urlList] of batches.entries()) {
    const label = batches.length > 1 ? ` (${i + 1}/${batches.length})` : '';
    try {
      const res = await fetch(ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json; charset=utf-8' },
        body: JSON.stringify({ ...base, urlList }),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
      });
      // 200 は受理、202 は受理済みでキーの検証待ち。どちらも成功として扱う。
      if (res.status === 200 || res.status === 202) {
        console.log(`IndexNow に ${urlList.length} 件を送信した${label} (HTTP ${res.status})。`);
        continue;
      }
      // 403 はキー不一致、422 は URL がホストに属さないかキーが見つからない、429 は送りすぎ。
      warn(`IndexNow への送信が受理されなかった${label} (HTTP ${res.status}): ${(await res.text()).slice(0, 200)}`);
    } catch (err) {
      warn(`IndexNow への送信に失敗した${label}: ${err.message}`);
    }
  }
}

async function main() {
  if (!beforePath || !afterPath) throw new Error('使い方: submit-indexnow.mjs <before.xml> <after.xml> [--dry-run]');
  const { updated, removed } = diffSitemaps(readFileSync(beforePath, 'utf8'), readFileSync(afterPath, 'utf8'));
  const urls = [...updated.map(([loc]) => loc), ...removed];
  if (urls.length === 0) {
    console.log('追加・更新・削除された URL は無い。送信しない。');
    return;
  }

  const host = new URL(urls[0]).host;
  const key = readKey();
  const base = { host, key, keyLocation: `https://${host}/${key}.txt` };
  console.log(`送信対象 ${urls.length} 件（追加・更新 ${updated.length} / 削除 ${removed.length}）:`);
  console.log(urls.map(u => `  ${u}`).join('\n'));
  if (dryRun) return;

  if (!(await waitForDeploy(`https://${host}/sitemap.xml`, updated, removed))) {
    warn(`GitHub Pages のデプロイが ${DEPLOY_WAIT_MS / 60000} 分以内に確認できなかったため、IndexNow への送信を見送った。`);
    return;
  }

  await submit(base, urls);
}

// テストから import したときは実行しない。
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(err => warn(`IndexNow への送信で例外が発生した: ${err.message}`));
}
