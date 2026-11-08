// git の pre-push フック（.githooks/pre-push）から呼ばれ、push される各ブランチの内容が、
// approve.mjs で承認した内容と同じかを確かめる。違えば終了コード 1 で push を止める。
//
// - 照合するのは、git が標準入力で渡す「push されるコミット」のツリー。コマンドの書き方
//   （つなぎ方・オプション・別のブランチの指定）に左右されない。
// - Claude Code から実行した push（環境変数 CLAUDECODE=1）だけを照合する。人が手で push するときは照合しない。
// - 削除の push は照合しない。
// - 途中で例外が起きたら、止める側に倒す（終了コード 1）。
//
// 手で試す（push される ref を標準入力で渡す。最後の 2 つは使わない）:
//   echo "refs/heads/x $(git rev-parse HEAD) refs/heads/x 0000000000000000000000000000000000000000" \
//     | CLAUDECODE=1 node tools/review-gate/pre-push.mjs origin url; echo $?
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

try {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk;
  }
  const updates = input
    .split(/\r?\n/)
    .map((l) => l.trim().split(/\s+/))
    .filter((p) => p.length >= 4 && !ZERO.test(p[1]));
  if (updates.length === 0) {
    process.exit(0);
  }

  const file = approvalFile(process.cwd(), false);
  if (!fs.existsSync(file)) {
    block(
      "承認の記録が無い。.claude/skills/pr/SKILL.md の Phase 1 手順 1 のとおり、" +
        "`node tools/review-gate/approve.mjs start` → 独立レビュー → 要修正 0 件で `approve.mjs done \"<結果>\"` を実行してから push する。",
    );
  }
  const record = JSON.parse(fs.readFileSync(file, "utf8"));
  for (const [localRef, localSha] of updates) {
    const tree = git(["rev-parse", `${localSha}^{tree}`]);
    if (tree !== record.tree) {
      block(
        `${localRef} の内容（tree ${tree.slice(0, 12)}）が、承認した内容（tree ${String(record.tree).slice(0, 12)}、` +
          `${record.approvedAt}）と違う。承認の後に変更したか、承認した変更をまだコミットしていないか、別のブランチを push しようとしている。` +
          "変更したなら、`approve.mjs start` から独立レビューをやり直して承認し直す。",
      );
    }
  }
  process.exit(0);
} catch (e) {
  block(`照合の途中でエラーが起きたので止めた: ${e.message}`);
}
