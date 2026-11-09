// このクローンの git の設定（.git/config）で、フックの置き場所をこのチェックアウトの .githooks の絶対パスにする。
// これで、通常の push では .githooks/pre-push（承認した内容との照合）が動く。相対パス（.githooks）にしないのは、
// .git の中や --git-dir・GIT_DIR で作業ツリーのルートが変わると、相対パスが別の場所を指し、フックが黙って動かなくなるため。
// フックが動かない場合（--no-verify など）は、pr スキルの「仕組みで防げないもの」を参照。
// 照合するのは CLAUDECODE=1 の環境からの push だけ（pre-push.mjs を参照）。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset core.hooksPath
import { git, repoRoot } from "./lib.mjs";

const desired = `${repoRoot()}/.githooks`;
const before = (() => {
  try {
    return git(["config", "--get", "core.hooksPath"]);
  } catch {
    return "";
  }
})();
if (before === desired) {
  console.log(`core.hooksPath は既に ${desired} になっている（変更しない）。`);
  process.exit(0);
}
// 以前の版の setup.mjs が設定した相対パスは、絶対パスに置き換える。
if (before && before !== ".githooks") {
  console.error(`core.hooksPath は既に ${before} に設定されている。上書きせずに止める。`);
  process.exit(1);
}
git(["config", "core.hooksPath", desired]);
console.log(`core.hooksPath を ${desired} にした。`);
