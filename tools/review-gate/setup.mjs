// このクローンの git の設定（.git/config）で、フックの置き場所を .githooks にする。
// これで、通常の push では .githooks/pre-push（承認した内容との照合）が動く。--no-verify などで外した場合と、
// .githooks の無いブランチでは動かない。照合するのは CLAUDECODE=1 の環境からの push だけ（pre-push.mjs を参照）。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset core.hooksPath
import { git } from "./lib.mjs";

const before = (() => {
  try {
    return git(["config", "--get", "core.hooksPath"]);
  } catch {
    return "";
  }
})();
if (before === ".githooks") {
  console.log("core.hooksPath は既に .githooks になっている（変更しない）。");
  process.exit(0);
}
if (before) {
  console.error(`core.hooksPath は既に ${before} に設定されている。上書きせずに止める。`);
  process.exit(1);
}
git(["config", "core.hooksPath", ".githooks"]);
console.log("core.hooksPath を .githooks にした。");
