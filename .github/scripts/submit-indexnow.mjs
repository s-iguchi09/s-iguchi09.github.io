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
export function isDeployed(liveXml, updated, removed) {
  const live = parseSitemap(liveXml);
  return updated.every(([loc, lastmod]) => live.has(loc) && live.get(loc) >= lastmod)
    && removed.every(loc => !live.has(loc));
}

async function waitForDeploy(siteSitemapUrl, updated, removed) {
  const deadline = Date.now() + DEPLOY_WAIT_MS;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(`${siteSitemapUrl}?t=${Date.now()}`, { cache: 'no-store' });
      if (res.ok && isDeployed(await res.text(), updated, removed)) return true;
    } catch {
      // 一時的な通信エラーは待機を続ける
    }
    await new Promise(r => setTimeout(r, DEPLOY_POLL_MS));
  }
  return false;
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
  const payload = { host, key, keyLocation: `https://${host}/${key}.txt`, urlList: urls };
  console.log(`送信対象 ${urls.length} 件（追加・更新 ${updated.length} / 削除 ${removed.length}）:`);
  console.log(urls.map(u => `  ${u}`).join('\n'));
  if (dryRun) return;

  if (!(await waitForDeploy(`https://${host}/sitemap.xml`, updated, removed))) {
    warn(`GitHub Pages のデプロイが ${DEPLOY_WAIT_MS / 60000} 分以内に確認できなかったため、IndexNow への送信を見送った。`);
    return;
  }

  const res = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify(payload),
  });
  // 200 は受理、202 は受理済みでキーの検証待ち。どちらも成功として扱う。
  if (res.status === 200 || res.status === 202) {
    console.log(`IndexNow に送信した (HTTP ${res.status})。`);
    return;
  }
  // 403 はキー不一致、422 は URL がホストに属さないかキーが見つからない、429 は送りすぎ。
  warn(`IndexNow への送信が受理されなかった (HTTP ${res.status}): ${(await res.text()).slice(0, 200)}`);
}

// テストから import したときは実行しない。
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(err => warn(`IndexNow への送信で例外が発生した: ${err.message}`));
}
