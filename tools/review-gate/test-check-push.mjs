// check-push.mjs の判定を、いくつかのコマンドで確かめる。
// 承認の記録を一時的に退避して「記録が無い」状態で実行し、終わったら元に戻す。
//
// 使い方:
//   node tools/review-gate/test-check-push.mjs
// 終了コード: すべて期待どおりなら 0、1 つでも違えば 1。
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { approvalFile } from "./lib.mjs";

const script = path.join(path.dirname(fileURLToPath(import.meta.url)), "check-push.mjs");
const file = approvalFile();
const saved = fs.existsSync(file) ? fs.readFileSync(file) : null;
fs.rmSync(file, { force: true });
const cases = [
  ["git push を含まない", "ls -la", 0],
  ["push する", 'git push -u origin "x"', 2],
  ["commit とつないだ push", "git add -A && git commit -m x && git push", 2],
  ["リモートの削除", "git push origin --delete x", 0],
  ["メッセージ中の git push（ヒアドキュメント）", "git commit -q -F - <<'EOF'\nhook は git push の前に動く\nEOF\ngit log -1", 0],
  ["メッセージ中の git push（引用符）", 'git commit -m "git push の前に動く"', 0],
  ["ヒアドキュメントの後で本当に push", "git commit -q -F - <<'EOF'\nmsg\nEOF\ngit push", 2],
  ["PowerShell のヒア文字列", "git commit -m @'\ngit push について\n'@", 0],
];
let failed = 0;
for (const [name, command, expected] of cases) {
  const r = spawnSync("node", [script], { input: JSON.stringify({ tool_input: { command } }), encoding: "utf8" });
  const ok = r.status === expected;
  if (!ok) failed++;
  console.log(`${ok ? "OK " : "NG "} ${name}: exit ${r.status}（期待 ${expected}）`);
}
if (saved) {
  fs.writeFileSync(file, saved);
}
process.exit(failed ? 1 : 0);
