// このクローンの git の設定（.git/config）で、pre-push フックの置き場所（core.hooksPath）を .githooks の絶対パスにする。
// 仕組みの全体は .claude/skills/pr/SKILL.md の Phase 1 手順 1「承認と push の照合」に書く。
//
// 動き（判定に使うのはこのクローンの設定（--local）だけ）:
//   - 既にこのリポジトリ（別の worktree を含む）の、pre-push のある .githooks の絶対パスなら、何もしない。
//   - 未設定、以前の版が設定した相対パス（.githooks）、または pre-push の無い .githooks なら、このチェックアウトの .githooks にする。
//   - それ以外は上書きせずに止め、git config --unset-all core.hooksPath を案内する。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset-all core.hooksPath
import fs from "node:fs";
import path from "node:path";
import { git, hooksPathProblem, repoRoot } from "./lib.mjs";

let root;
try {
  root = repoRoot();
} catch {
  console.error("作業ツリーのルートを特定できない（.git の中や bare リポジトリなど）。リポジトリのルートで実行する。");
  process.exit(1);
}
const desired = `${root}/.githooks`;
const before = (() => {
  try {
    return git(["config", "--local", "--get", "core.hooksPath"]);
  } catch {
    return "";
  }
})();

if (before && !hooksPathProblem(before)) {
  console.log(`core.hooksPath は既にこのリポジトリの ${before} になっている（変更しない）。`);
  process.exit(0);
}
const replaceable =
  !before || before === ".githooks" || (path.basename(before) === ".githooks" && !fs.existsSync(path.join(before, "pre-push")));
if (!replaceable) {
  console.error(
    `core.hooksPath は既に ${before} に設定されている。上書きせずに止める。` +
      "このリポジトリのフックに切り替えるなら、`git config --unset-all core.hooksPath` のあとで、もう一度実行する。",
  );
  process.exit(1);
}
if (!fs.existsSync(path.join(desired, "pre-push"))) {
  console.error(`このチェックアウトに ${desired}/pre-push が無い。照合の仕組みが入ったチェックアウトで実行する。`);
  process.exit(1);
}
git(["config", "--local", "core.hooksPath", desired]);
console.log(`core.hooksPath を ${desired} にした。`);
