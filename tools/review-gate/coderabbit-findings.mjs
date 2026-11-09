// 直近にマージした PR で CodeRabbit が付けた指摘を集め、独立レビューのレビュアーに渡す一覧を作る。
// このリポジトリでは CodeRabbit が同じ型の指摘（主張が計測より広い、コード片の誤りなど）を繰り返しているので、
// その型を push 前のレビューで先に確かめるために使う。
//
// 集めるもの:
//   - インラインの指摘（返信は除く）
//   - レビュー本文の項目（「Outside diff range」「Nitpick」「Additional comments」など。区分は見分けない。
//     CodeRabbit が検証できなかった指摘（Unverified finding）は、重要度に「未検証」と付けて残す）
//   - 失敗・警告になった pre-merge check（Docstring Coverage など）
//
// 使い方（リポジトリのルートで実行する）:
//   node tools/review-gate/coderabbit-findings.mjs [PR の件数（既定 15）]
// 出力: `git rev-parse --git-path review-gate` の場所（通常は .git/review-gate/）の coderabbit-findings.md（コミットされない）。
//       パスと件数を標準出力に書く。
// 前提: gh が使え、リポジトリを読めること。
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { gateDir } from "./lib.mjs";

const limit = Number.parseInt(process.argv[2] ?? "15", 10);
if (!Number.isInteger(limit) || limit < 1) {
  console.error("PR の件数は 1 以上の整数で指定する。");
  process.exit(1);
}

/** gh を実行して標準出力を返す。失敗したら例外。 */
function gh(args) {
  return execFileSync("gh", args, { encoding: "utf8", maxBuffer: 256 * 1024 * 1024 });
}

/** gh api の結果を、ページ送りを含めて 1 つの配列で返す。 */
function ghList(endpoint) {
  return gh(["api", endpoint, "--paginate", "--jq", ".[] | tojson"])
    .split("\n")
    .filter(Boolean)
    .map((l) => JSON.parse(l));
}

const repo = gh(["repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"]).trim();
// gh pr list は作成日時の順なので、作成が新しい limit*3 件を取り、マージした日時で並べ直して limit 件にする
// （作成がそれより古く、最近マージされた PR は入らないことがある）。
const prs = JSON.parse(gh(["pr", "list", "--state", "merged", "--limit", String(limit * 3), "--json", "number,title,mergedAt"]))
  .sort((a, b) => Date.parse(b.mergedAt) - Date.parse(a.mergedAt))
  .slice(0, limit);

const severityWords = /(?:🔴|🟠|🟡|🔵|⚪)?\s*\b(Critical|Major|Minor|Trivial|Nitpick)\b/;

/** 指摘の本文から、重要度・見出し・問題の説明（最初の段落）を取り出す。 */
function summarize(body) {
  const icon = (body.match(/icons-v1\/(\w+)-16\.svg/) || [])[1];
  const label =
    { amber: "Minor", major: "Major", red: "Critical", neutral: "Nitpick" }[icon] ?? (body.match(severityWords) || [])[1] ?? "?";
  // 消すのは CodeRabbit が飾りに使う HTML のタグだけにする（指摘の中の XAML のタグは残す）。
  const plain = body
    .replace(/<details>[\s\S]*?<\/details>/g, "")
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/<\/?(?:picture|img|source|details|summary|dl|dd|dt|br|blockquote|sub|sup|h[1-6]|p|a|div|span)\b[^>]*>/gi, "");
  const bolds = [...plain.matchAll(/\*\*([^*\n]{6,})\*\*/g)]
    .map((m) => m[1].trim())
    .filter((t) => !/^[^\p{L}\p{N}]*(?:Critical|Major|Minor|Trivial|Nitpick)[^\p{L}\p{N}]*$/u.test(t))
    .filter((t) => !/Quick win|Functional Correctness|Stability|Maintainability|Data Integrity|Documentation|Performance|Security/.test(t));
  const title = bolds[0] ?? "";
  const afterTitle = title ? plain.slice(plain.indexOf(title) + title.length) : plain;
  const paragraph =
    afterTitle
      .split(/\n\s*\n/)
      .map((p) => p.replace(/\*\*/g, "").replace(/\s+/g, " ").trim())
      .find((p) => p.length > 20 && !p.startsWith("```") && !severityWords.test(p.slice(0, 20))) ?? "";
  return { label, title, paragraph: paragraph.slice(0, 400) };
}

/**
 * レビュー本文の「Additional comments」の項目を取り出す。この形式は、重要度と題が summary の外にある:
 *   <summary>パス-開始行-終了行: 件数</summary> … `開始行-終了行`: **重要度** · … （**Unverified finding**） **題**
 * 「Unverified finding」が付いた項目は、未検証として一覧に残す。
 */
function additionalComments(body) {
  const items = [];
  let file = "";
  let current = null;
  let inBlock = false;
  for (const raw of body.split(/\r?\n/)) {
    const line = raw.replace(/<[^>]+>/g, "").trim();
    // 解析するのは「Additional comments: N」の節の中だけ（節の後ろの区切り線や次の節で終える）。
    if (/Additional comments: \d+/.test(line)) {
      inBlock = true;
      continue;
    }
    if (!inBlock) continue;
    if (line === "---" || /Prompt to fix all issues|Review info/.test(line)) {
      inBlock = false;
      current = null;
      continue;
    }
    const fileHead = raw.match(/<summary>(.+?)-\d+-\d+: \d+<\/summary>/) || raw.match(/<summary>(.+?)-\d+: \d+<\/summary>/);
    if (fileHead) {
      file = fileHead[1].trim();
      current = null;
      continue;
    }
    const entry = line.match(/^`(\d+(?:-\d+)?)`: (.*)$/);
    if (entry) {
      const sev = (entry[2].match(severityWords) || [])[1] ?? "?";
      current = { label: sev, title: "", where: `${file}:${entry[1]}`, kind: "レビュー本文の項目（Additional comments）", unverified: false };
      items.push(current);
      continue;
    }
    if (!current || current.title) continue;
    if (/Unverified finding/.test(line)) {
      current.unverified = true;
      continue;
    }
    const bold = line.match(/^\*\*(.+?)\*\*/);
    if (bold && !/^Unverified finding$/.test(bold[1])) current.title = bold[1].trim();
  }
  return items.map((i) => (i.unverified ? { ...i, label: `${i.label}・未検証` } : i));
}

/** レビュー本文から、差分の外・Nitpick などの項目（区分は見分けない）と、失敗・警告の pre-merge check を取り出す。 */
function reviewBodyItems(body) {
  const items = [];
  for (const m of body.matchAll(/<summary>([\s\S]*?)<\/summary>/g)) {
    const s = m[1].replace(/<[^>]+>/g, "").replace(/\s+/g, " ").trim();
    const parts = s.split(" · ").map((x) => x.trim());
    const sev = (parts[0].match(severityWords) || [])[1];
    // 「パス:行番号」の形の部分（拡張子の無い .githooks/pre-push:3 なども含む）。
    const where = parts.find((p) => /^[^\s:][^:]*:\d+(?:-\d+)?$/.test(p));
    if (sev && parts.length >= 3 && where) {
      // どの区分（差分の外・Nitpick など）の項目かは見分けていない。
      items.push({ label: sev, title: parts[1], where, kind: "レビュー本文の項目" });
    }
  }
  items.push(...additionalComments(body));
  for (const m of body.matchAll(/^\|\s*([^|]+?)\s*\|\s*!\[(Warning|Failed|Error)\][^|]*\|\s*([^|]+?)\s*\|/gm)) {
    items.push({ label: m[2], title: `pre-merge check: ${m[1]}`, where: "", kind: "pre-merge check", paragraph: m[3].slice(0, 300) });
  }
  return items;
}

const lines = [
  "# 過去の PR で CodeRabbit が付けた指摘",
  "",
  `${repo} の、マージ済みの PR のうち作成が新しい ${limit * 3} 件の中で、マージした日時が新しい ${prs.length} 件から集めた（指摘の無い PR は載せない）。`,
  "インラインの指摘（返信を除く）と、",
  "レビュー本文の項目（「Outside diff range」「Nitpick」「Additional comments」など。区分は見分けない。検証できなかった指摘は「未検証」）、" +
    "失敗・警告になった pre-merge check を含む。",
  "同じ型の問題が今回の差分に無いかを確かめる。日英で同じ指摘が 2 件ずつ付いていることがある。",
  "",
];
let total = 0;
for (const pr of prs) {
  const inline = ghList(`repos/${repo}/pulls/${pr.number}/comments`).filter(
    (c) => c.user?.login === "coderabbitai[bot]" && !c.in_reply_to_id,
  );
  const fromBodies = ghList(`repos/${repo}/pulls/${pr.number}/reviews`)
    .filter((r) => r.user?.login === "coderabbitai[bot]" && r.body)
    .flatMap((r) => reviewBodyItems(r.body));
  const fromIssue = ghList(`repos/${repo}/issues/${pr.number}/comments`)
    .filter((c) => c.user?.login === "coderabbitai[bot]" && /pre-merge/i.test(c.body ?? ""))
    .flatMap((c) => reviewBodyItems(c.body).filter((i) => i.kind === "pre-merge check"));
  const seen = new Set();
  const bodyItems = [...fromBodies, ...fromIssue].filter((i) => {
    const key = `${i.kind}|${i.title}|${i.where}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  if (inline.length === 0 && bodyItems.length === 0) {
    continue;
  }
  lines.push(`## #${pr.number} ${pr.title}`, "");
  for (const c of inline) {
    const s = summarize(c.body);
    lines.push(`- **[${s.label}] ${s.title}**（${c.path}）`);
    if (s.paragraph) {
      lines.push(`  ${s.paragraph}`);
    }
    total++;
  }
  for (const i of bodyItems) {
    lines.push(`- **[${i.label}] ${i.title}**（${i.kind}${i.where ? `、${i.where}` : ""}）`);
    if (i.paragraph) {
      lines.push(`  ${i.paragraph}`);
    }
    total++;
  }
  lines.push("");
}

const out = path.join(gateDir(), "coderabbit-findings.md");
fs.writeFileSync(out, lines.join("\n"));
console.log(`${out}（${total} 件）`);
