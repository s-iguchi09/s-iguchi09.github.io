// 独立レビューで要修正が 0 件になった時点の作業ツリーの内容を、レビュー済みとして記録する。
//
// 使い方（リポジトリのどこからでもよい）:
//   node tools/review-gate/approve.mjs "要修正 0 件（3 観点）"
//
// 記録は .git/review-gate/approved.json に置く（コミットされない）。
// push の前に動く hook（check-push.mjs）が、push する HEAD のツリーと照合する。
// 記録した後に 1 文字でも変えると一致しなくなるので、直したら独立レビューからやり直す。
import fs from "node:fs";
import { approvalFile, git, workingTreeHash } from "./lib.mjs";

const note = process.argv.slice(2).join(" ").trim();
if (!note) {
  console.error("レビューの結果を引数に書く（例: \"要修正 0 件（3 観点）\"）。");
  process.exit(1);
}

const tree = workingTreeHash();
const record = {
  tree,
  branch: git(["rev-parse", "--abbrev-ref", "HEAD"]),
  approvedAt: new Date().toISOString(),
  note,
};
fs.writeFileSync(approvalFile(), JSON.stringify(record, null, 2) + "\n");
console.log(`レビュー済みとして記録した: tree ${tree}（${record.branch}）`);
