// git の pre-push フック（.githooks/pre-push）から呼ばれ、送り先がブランチ（refs/heads/。大文字小文字は区別しない）の各 ref について、
// 先頭のコミットの内容（ツリー）が approve.mjs で承認した内容と同じかを確かめる。違えば終了コード 1 で push を止める。
// 削除と、送り先がブランチ以外の push は照合しない。照合の途中でエラーが起きたら止める。
// 仕組みの全体・防ぐ範囲・防げないものは .claude/skills/pr/SKILL.md の Phase 1 手順 1「承認と push の照合」に書く。
//
// 手で試す（push される ref を標準入力で渡す。最後の 2 つの引数は使わない）。Claude Code の Bash では
// 環境変数 CLAUDECODE が既に 1 なので、そのまま実行する。それ以外の端末では、CLAUDECODE を 1 にしてから実行する。
//   echo "refs/heads/x $(git rev-parse HEAD) refs/heads/x 0000000000000000000000000000000000000000" \
//     | node tools/review-gate/pre-push.mjs origin url; echo $?
import fs from "node:fs";
import { approvalFile, git } from "./lib.mjs";

const ZERO = /^0+$/;

/** 理由を標準エラーに書き、終了コード 1 で push を止める。 */
function block(message) {
  process.stderr.write(`[review-gate] ${message}\n`);
  process.exit(1);
}

if (process.env.CLAUDECODE !== "1") {
  process.exit(0);
}

// 置き換えの ref（git replace）に従うと、rev-parse は置き換え先の内容を返すが、push は元のオブジェクトを送る。
// 実際に送られる内容で照合するため、この照合の git では置き換えを無効にする（子プロセスの git にも引き継がれる）。
process.env.GIT_NO_REPLACE_OBJECTS = "1";

try {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk;
  }
  const lines = input.split(/\r?\n/).filter((l) => l.trim() !== "");
  const sha = /^[0-9a-f]{40,64}$/;
  const parsed = lines.map((l) => {
    // 送り元の式は空白を含むことがあるので、空白を含まない右の 3 列（送り元の sha、送り先の ref、送り先の sha）から読む。
    const p = l.trim().split(" ");
    if (p.length < 4 || !sha.test(p.at(-3)) || !sha.test(p.at(-1))) {
      block(`git から渡された push の内容を読めない（${l}）。照合できないので止めた。`);
    }
    return [p.slice(0, -3).join(" "), p.at(-3), p.at(-2), p.at(-1)];
  });
  // 照合するのは送り先がブランチ（refs/heads/）のものだけ。削除と、タグなどブランチ以外への push は照合しない。
  // 送り元がタグでも、送り先がブランチなら照合する。
  const updates = parsed.filter((p) => !ZERO.test(p[1]) && p[2].toLowerCase().startsWith("refs/heads/"));
  if (updates.length === 0) {
    process.exit(0);
  }

  const file = approvalFile(process.cwd(), false);
  if (!fs.existsSync(file)) {
    block(
      "承認の記録が無い。.claude/skills/pr/SKILL.md の Phase 1 手順 1 のとおり独立レビューを通し、" +
        "`node tools/review-gate/approve.mjs done` で承認を記録してから push する（例外の場合は同じ手順の direct）。",
    );
  }
  let record;
  try {
    record = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch {
    block(`承認の記録（${file}）を読めない。承認をやり直す（Phase 1 手順 1）。`);
  }
  if (typeof record?.tree !== "string") {
    block(`承認の記録（${file}）に tree が無い。承認をやり直す（Phase 1 手順 1）。`);
  }
  for (const [localRef, localSha, remoteRef] of updates) {
    const tree = git(["rev-parse", `${localSha}^{tree}`]);
    if (tree !== record.tree) {
      block(
        `${localRef} → ${remoteRef} の内容（tree ${tree.slice(0, 12)}）が、承認した内容（tree ${String(record.tree).slice(0, 12)}、` +
          `${record.approvedAt ?? "日時不明"}）と違う。承認の後に変更した、承認した変更（承認のときにあった未追跡のファイルを含む）をまだコミットしていない、` +
          "または内容の違う別のブランチを push しようとしている。変更したなら、pr スキル Phase 1 手順 1 の独立レビューからやり直す。",
      );
    }
  }
  process.exit(0);
} catch (e) {
  block(`照合の途中でエラーが起きたので止めた: ${e.message}`);
}
