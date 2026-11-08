// このリポジトリの git の設定（.git/config）で、フックの置き場所を .githooks にする。
// これで .githooks/pre-push（承認した内容との照合）が push のたびに動く。
// 照合するのは Claude Code から実行した push だけ（pre-push.mjs を参照）。
//
// 使い方（リポジトリのルートで、1 回だけ実行する）:
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
if (before && before !== ".githooks") {
  console.error(`core.hooksPath は既に ${before} に設定されている。上書きせずに止める。`);
  process.exit(1);
}
git(["config", "core.hooksPath", ".githooks"]);
console.log("core.hooksPath を .githooks にした。");
