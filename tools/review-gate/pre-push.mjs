// git の pre-push フック（.githooks/pre-push）から呼ばれ、push される各ブランチの先頭のコミットの内容（ツリー）が、
// approve.mjs で承認した内容と同じかを確かめる。違えば終了コード 1 で push を止める。
//
// - フックが動けば、照合する ref は git が標準入力で渡すので、コマンドのつなぎ方やブランチの指定には左右されない。
//   標準入力の各行は「送り元の式 送り元の sha 送り先の ref 送り先の sha」で、送り元の式は空白を含むことがある
//   （HEAD@{0 seconds ago} など）ので、右の 3 列から読む。形の合わない行があれば止める。
//   ただし、--no-verify・core.hooksPath の変更・core.hooksPath の指すチェックアウトが .githooks の無いブランチやコミットにある場合
//   （切り替えると check-setup.mjs と hook の設定も無くなる）ではフックが動かず、
//   CLAUDECODE を外した実行ではフックは動くが照合しない（check-setup.mjs は一部の語や設定を含む場合だけを止め、ほかは手順書で禁じている。
//   仕組みでは防げない）。.git の中や --git-dir・GIT_DIR で作業ツリーのルートが変わった push は、core.hooksPath が絶対パスなので
//   フックが動き、作業ツリーのルートを特定できないか、そこに照合のスクリプトが無ければ .githooks/pre-push の sh 側が止める（あれば照合する）。
// - 照合するのは環境変数 CLAUDECODE=1 の環境（Claude Code のツール・hook の子プロセスや、Claude Code の IDE 拡張の
//   統合ターミナルなど）からの push だけ。IDE 拡張の統合ターミナルから人が push した場合も照合する。
//   それ以外の端末から人が push するときは照合しない（判定は .githooks/pre-push の sh 側）。
// - 照合するのは各ブランチの先頭のコミットの内容だけで、間のコミットの内容は確かめない。
// - 照合するのは送り先がブランチ（refs/heads/）の push だけ。削除と、送り先がタグなどブランチ以外の push は照合しない
//   （送り元がタグでも、送り先がブランチなら照合する。refs/Heads/ のように大文字小文字を変えた書き方も、
//   大文字小文字を区別しないファイルシステムではブランチになりうるので、止める側に倒して照合する）。
// - 照合の途中でエラーが起きたら、止める側に倒す（終了コード 1）。
//
// 手で試す（push される ref を標準入力で渡す。最後の 2 つの引数は使わない）。Claude Code の Bash では
// 環境変数 CLAUDECODE が既に 1 なので、そのまま実行する（コマンドに書き足すと check-setup.mjs に止められる）。
// それ以外の端末では、環境変数 CLAUDECODE を 1 にしてから実行する。
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
      "承認の記録が無い。.claude/skills/pr/SKILL.md の Phase 1 手順 1 のとおり、" +
        "`node tools/review-gate/article-check.mjs` と `node tools/review-gate/approve.mjs start` → 独立レビュー → " +
        "要修正 0 件で `node tools/review-gate/approve.mjs done \"<結果>\"` を実行してから push する。" +
        "手順書の「対象」の例外に当たるときは、同じ手順の条件（article-check.mjs の (c) の確認など）を満たしたうえで " +
        "`node tools/review-gate/approve.mjs direct \"<理由>\"` で記録する。",
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
          "または内容の違う別のブランチを push しようとしている。変更したなら、`node tools/review-gate/article-check.mjs` と " +
          "`node tools/review-gate/approve.mjs start` から独立レビューをやり直して承認し直す。",
      );
    }
  }
  process.exit(0);
} catch (e) {
  block(`照合の途中でエラーが起きたので止めた: ${e.message}`);
}
