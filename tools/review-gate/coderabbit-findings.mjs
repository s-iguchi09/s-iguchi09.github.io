// 直近にマージした PR で CodeRabbit が付けた指摘を集め、独立レビューのレビュアーに渡す一覧を作る。
// CodeRabbit は過去の PR から学んだ内容でレビューするので、同じ型の指摘を push 前に潰すために使う。
//
// 使い方:
//   node tools/review-gate/coderabbit-findings.mjs [PR の件数（既定 15）]
// 出力: .git/review-gate/coderabbit-findings.md（コミットされない）。パスを標準出力に書く。
// 前提: gh が使え、リポジトリを読めること。
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { gateDir } from "./lib.mjs";

const limit = Number.parseInt(process.argv[2] ?? "15", 10);

/** gh を実行して標準出力を返す。失敗したら例外。 */
function gh(args) {
  return execFileSync("gh", args, { encoding: "utf8", maxBuffer: 256 * 1024 * 1024 });
}

const repo = gh(["repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"]).trim();
const prs = JSON.parse(gh(["pr", "list", "--state", "merged", "--limit", String(limit), "--json", "number,title"]));

/** CodeRabbit の本文から、見出し・重要度・問題の説明（最初の段落）を取り出す。 */
function summarize(body) {
  const severity = (body.match(/icons-v1\/(\w+)-16\.svg/) || [])[1] ?? "?";
  const label = { amber: "Minor", major: "Major", red: "Critical", neutral: "Nitpick" }[severity] ?? severity;
  const plain = body
    .replace(/<details>[\s\S]*?<\/details>/g, "")
    .replace(/<!--[\s\S]*?-->/g, "")
    // 消すのは CodeRabbit が飾りに使う HTML のタグだけにする（指摘の中の XAML のタグは残す）。
    .replace(/<\/?(?:picture|img|source|details|summary|dl|dd|dt|br|blockquote|sub|sup|h[1-6]|p|a|div|span)\b[^>]*>/gi, "");
  const bolds = [...plain.matchAll(/\*\*([^*\n]{6,})\*\*/g)]
    .map((m) => m[1].trim())
    .filter((t) => !/^(Minor|Major|Critical|Trivial|Nitpick)$/.test(t) && !/Quick win|Functional Correctness|Stability|Maintainability|Data Integrity|Documentation/.test(t));
  const title = bolds[0] ?? "";
  const afterTitle = title ? plain.slice(plain.indexOf(title) + title.length) : plain;
  const paragraph = afterTitle
    .split(/\n\s*\n/)
    .map((p) => p.replace(/\*\*/g, "").replace(/\s+/g, " ").trim())
    .find((p) => p.length > 20 && !p.startsWith("```")) ?? "";
  return { label, title, paragraph: paragraph.slice(0, 400) };
}

const lines = [
  "# 過去の PR で CodeRabbit が付けた指摘",
  "",
  `${repo} の直近 ${prs.length} 件のマージ済み PR から、CodeRabbit のインラインの指摘（返信を除く）を集めた。`,
  "同じ型の問題が今回の差分に無いかを確かめる。日英で同じ指摘が 2 件ずつ付いていることがある。",
  "",
];
let total = 0;
for (const pr of prs) {
  const raw = gh(["api", `repos/${repo}/pulls/${pr.number}/comments`, "--paginate", "--jq", ".[] | tojson"]);
  const items = raw.split("\n").filter(Boolean).map((l) => JSON.parse(l));
  const top = items.filter((c) => c.user?.login === "coderabbitai[bot]" && !c.in_reply_to_id);
  if (top.length === 0) {
    continue;
  }
  lines.push(`## #${pr.number} ${pr.title}`, "");
  for (const c of top) {
    const s = summarize(c.body);
    lines.push(`- **[${s.label}] ${s.title}**（${c.path}）`);
    if (s.paragraph) {
      lines.push(`  ${s.paragraph}`);
    }
    total++;
  }
  lines.push("");
}

const out = path.join(gateDir(), "coderabbit-findings.md");
fs.writeFileSync(out, lines.join("\n"));
console.log(`${out}（${total} 件）`);
