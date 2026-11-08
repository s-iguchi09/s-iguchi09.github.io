// 独立レビューで確かめた内容を、push してよい内容として記録する。
// 記録は .git/review-gate/ に置く（コミットされない）。照合は pre-push.mjs（git の pre-push フック）が行う。
//
// 使い方（リポジトリのルートで実行する）:
//   node tools/review-gate/approve.mjs start
//       レビュアーを起動する直前に実行し、レビューしてもらう内容（作業ツリー）を記録する。
//   node tools/review-gate/approve.mjs done "要修正 0 件（3 観点）"
//       要修正が 0 件になったら実行する。作業ツリーが start の時点と同じときだけ、承認として記録する。
//       違えば記録せずに終了コード 1（レビューした後に変えている）。
//   node tools/review-gate/approve.mjs direct "<理由>"
//       独立レビューを省いてよいと手順書が定める場合（pr スキル Phase 1 手順 1 の「対象」の例外）だけに使う。
//
// 承認は「記録した内容と push する内容が同じこと」を保証するだけで、レビューが行われたことまでは確かめない。
import fs from "node:fs";
import { approvalFile, git, pendingFile, untrackedFiles, workingTreeHash } from "./lib.mjs";

const [mode, ...rest] = process.argv.slice(2);
const note = rest.join(" ").trim();

/** 承認を書き出し、記録した内容を表示する。 */
function writeApproval(tree, kind) {
  const record = {
    tree,
    branch: git(["rev-parse", "--abbrev-ref", "HEAD"]),
    approvedAt: new Date().toISOString(),
    kind,
    note,
  };
  fs.writeFileSync(approvalFile(), JSON.stringify(record, null, 2) + "\n");
  console.log(`承認として記録した: tree ${tree}（${record.branch}、${kind}）`);
  const untracked = untrackedFiles();
  if (untracked.length) {
    console.log("未追跡のファイルも承認した内容に含まれる。コミットしないと push の照合で一致しない:");
    for (const f of untracked) console.log(`  ${f}`);
  }
}

if (mode === "start") {
  const tree = workingTreeHash();
  fs.writeFileSync(pendingFile(), JSON.stringify({ tree, startedAt: new Date().toISOString() }, null, 2) + "\n");
  console.log(`レビューする内容を記録した: tree ${tree}`);
} else if (mode === "done") {
  if (!note) {
    console.error("レビューの結果を書く（例: done \"要修正 0 件（3 観点）\"）。");
    process.exit(1);
  }
  if (!fs.existsSync(pendingFile())) {
    console.error("レビューを始めた記録が無い。レビュアーを起動する前に `approve.mjs start` を実行する。");
    process.exit(1);
  }
  const started = JSON.parse(fs.readFileSync(pendingFile(), "utf8"));
  const tree = workingTreeHash();
  if (started.tree !== tree) {
    console.error(
      `作業ツリーが、レビューを始めた時点（${started.startedAt}）から変わっている。` +
        "変えた後の内容はレビューされていないので承認しない。`approve.mjs start` からレビューをやり直す。",
    );
    process.exit(1);
  }
  writeApproval(tree, "reviewed");
} else if (mode === "direct") {
  if (!note) {
    console.error("独立レビューを省く理由を書く（例: direct \"article-workflow の Phase 2 で収束\"）。");
    process.exit(1);
  }
  writeApproval(workingTreeHash(), "direct");
} else {
  console.error("使い方: approve.mjs start | done \"<結果>\" | direct \"<理由>\"");
  process.exit(1);
}
