// Claude Code の PreToolUse hook（Bash）から呼ばれ、git push の前に、
// push する HEAD のツリーが独立レビューで承認した内容（approve.mjs の記録）と同じかを確かめる。
// 一致しなければ終了コード 2 で push を止め、理由を標準エラーに書く（Claude Code はそれを Claude に返す）。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"}}' | node tools/review-gate/check-push.mjs; echo $?
//
// 止めないもの: git push を含まないコマンド、--delete / -d によるリモートブランチの削除、--dry-run。
import fs from "node:fs";
import { approvalFile, headTreeHash } from "./lib.mjs";

/** 理由を標準エラーに書き、終了コード 2 で push を止める。 */
function block(message) {
  process.stderr.write(`[review-gate] ${message}\n`);
  process.exit(2);
}

let input = "";
for await (const chunk of process.stdin) {
  input += chunk;
}

let command = "";
try {
  command = JSON.parse(input)?.tool_input?.command ?? "";
} catch {
  // 読めない入力は対象外として通す（hook の誤動作で全コマンドを止めないため）。
  process.exit(0);
}

// コミットメッセージなどに書いた「git push」で誤って止めないよう、ヒアドキュメントの中身と、
// 引用符で囲んだ文字列を除いてから判定する。
command = command
  .replace(/<<-?\s*(['"]?)(\w+)\1[^\n]*\n[\s\S]*?\n\s*\2\s*(?=\n|$)/g, "<<heredoc")
  .replace(/'[^']*'/g, "''")
  .replace(/"(?:[^"\\]|\\.)*"/g, '""')
  .replace(/@'[\s\S]*?'@/g, "''");

if (!/\bgit\s+(?:-C\s+\S+\s+)?push\b/.test(command)) {
  process.exit(0);
}

if (/\s(?:--delete|-d|--dry-run|-n)\b/.test(command)) {
  process.exit(0);
}

// commit と同じコマンドの中で push すると、hook の時点ではまだ HEAD が古く、照合できない。
if (/\bgit\s+(?:-C\s+\S+\s+)?commit\b/.test(command)) {
  block("commit と push を 1 つのコマンドにつないでいる。commit を先に実行し、push は別のコマンドで実行する。");
}

const file = approvalFile();
if (!fs.existsSync(file)) {
  block(
    "独立レビューの承認の記録が無い。.claude/skills/pr/SKILL.md の Phase 1 手順 1 の独立レビューを通し、" +
      "要修正が 0 件になったら `node tools/review-gate/approve.mjs \"<結果>\"` を実行してから push する。",
  );
}

const record = JSON.parse(fs.readFileSync(file, "utf8"));
const head = headTreeHash();
if (record.tree !== head) {
  block(
    `push する内容（HEAD のツリー ${head.slice(0, 12)}）が、独立レビューで承認した内容（${String(record.tree).slice(0, 12)}、` +
      `${record.approvedAt}）と違う。承認の後に変更したか、承認した変更をまだコミットしていない。` +
      "変更したなら、変更後の差分全体で独立レビューをやり直し、approve.mjs で記録し直してから push する。",
  );
}

process.exit(0);
