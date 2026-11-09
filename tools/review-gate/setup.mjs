// このクローンの git の設定（.git/config）で、フックの置き場所を .githooks の絶対パスにする。
// これで、通常の push では .githooks/pre-push（承認した内容との照合）が動く。相対パス（.githooks）にしないのは、
// .git の中や --git-dir・GIT_DIR で作業ツリーのルートが変わると、相対パスが別の場所を指し、フックが黙って動かなくなるため。
// core.hooksPath は同じリポジトリの worktree の間で共有されるので、どれか 1 つのチェックアウトの .githooks を指せば、
// 指す先に pre-push がある間は、すべての worktree の push でフックが動く（各 worktree の照合のスクリプトは、その worktree のものを使う）。
// フックが動かない場合（--no-verify など）は、pr スキルの「仕組みで防げないもの」を参照。
// 照合するのは CLAUDECODE=1 の環境からの push だけ（pre-push.mjs を参照）。
//
// 動き:
//   - 既に、このリポジトリのチェックアウト（別の worktree を含む）のルート直下の、pre-push のある .githooks の絶対パスなら、変更しない。
//   - 未設定、以前の版が設定した相対パス（.githooks）、または pre-push の無い .githooks の絶対パス（指していた worktree を消した・
//     移したなど）なら、このチェックアウトの .githooks の絶対パスにする（このチェックアウトに .githooks/pre-push が無ければ止める）。
//   - それ以外（別のリポジトリ、.githooks 以外の置き場所）は、上書きせずに止める。
//   判定に使うのはこのクローンの設定（--local）だけ。グローバルやシステムの core.hooksPath は、ローカルの設定で上書きされる。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset core.hooksPath
import fs from "node:fs";
import path from "node:path";
import { git, hooksPathProblem, repoRoot } from "./lib.mjs";

const unsetHint = "このリポジトリのフックに切り替えるなら、`git config --unset core.hooksPath` のあとで、もう一度実行する。";

let root;
try {
  root = repoRoot();
} catch {
  console.error("作業ツリーのルートを特定できない（.git の中や bare リポジトリなど）。リポジトリのルートで実行する。");
  process.exit(1);
}
const desired = `${root}/.githooks`;
// 上書きしてよいかは、このクローンの設定（--local）だけで決める。グローバルやシステムの設定は、
// ローカルに設定すれば上書きされるので、触らない（git config --unset で消せるのもローカルだけ）。
const before = (() => {
  try {
    return git(["config", "--local", "--get", "core.hooksPath"]);
  } catch {
    return "";
  }
})();

if (before && path.isAbsolute(before)) {
  if (!hooksPathProblem(before)) {
    console.log(`core.hooksPath は既にこのリポジトリの ${before} になっている（変更しない）。`);
    process.exit(0);
  }
  // 置き換えるのは、名前が .githooks で pre-push が無いもの（指していた worktree を消した・移したなど）だけ。
  if (path.basename(before) !== ".githooks" || fs.existsSync(path.join(before, "pre-push"))) {
    console.error(`${hooksPathProblem(before)}。上書きせずに止める。${unsetHint}`);
    process.exit(1);
  }
  console.log(`core.hooksPath の指す ${before} に pre-push が無いので、置き換える。`);
} else if (before && before !== ".githooks") {
  console.error(`core.hooksPath は既に ${before} に設定されている。上書きせずに止める。${unsetHint}`);
  process.exit(1);
}
if (!fs.existsSync(path.join(desired, "pre-push"))) {
  console.error(`このチェックアウトに ${desired}/pre-push が無い。照合の仕組みが入ったチェックアウトで実行する。`);
  process.exit(1);
}
git(["config", "core.hooksPath", desired]);
console.log(`core.hooksPath を ${desired} にした。`);
