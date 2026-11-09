// このクローンの git の設定（.git/config）で、フックの置き場所を .githooks の絶対パスにする。
// これで、通常の push では .githooks/pre-push（承認した内容との照合）が動く。相対パス（.githooks）にしないのは、
// .git の中や --git-dir・GIT_DIR で作業ツリーのルートが変わると、相対パスが別の場所を指し、フックが黙って動かなくなるため。
// core.hooksPath は同じリポジトリの worktree の間で共有されるので、どれか 1 つのチェックアウトの .githooks を指せば、
// 指す先に pre-push がある間は、すべての worktree の push でフックが動く（各 worktree の照合のスクリプトは、その worktree のものを使う。
// worktree 単位の設定（extensions.worktreeConfig）や include で別の値を読み込んでいれば、そちらが効く。下の「確かめる」を参照）。
// フックが動かない場合（--no-verify など）は、pr スキルの「仕組みで防げないもの」を参照。
// 照合するのは CLAUDECODE=1 の環境からの push だけ（pre-push.mjs を参照）。
//
// 動き:
//   - 既に、このリポジトリのチェックアウト（別の worktree を含む）のルート直下の、pre-push のある .githooks の絶対パスなら、変更しない。
//   - 未設定、以前の版が設定した相対パス（.githooks）、または pre-push の無い .githooks の絶対パス（どのリポジトリのものでも。
//     指していた worktree を消した・移したなど）なら、このチェックアウトの .githooks の絶対パスにする
//     （このチェックアウトに .githooks/pre-push が無ければ止める）。
//   - それ以外（pre-push のある別のリポジトリの .githooks、.githooks 以外の置き場所、値が複数ある）は、上書きせずに止める。
//   上書きしてよいかの判定に使うのはこのクローンの設定（--local）だけ。グローバルやシステムの core.hooksPath は、
//   ローカルの設定で上書きされる。
//   確かめる: 最後に、実際に効いている値（git config --get）を同じ条件で確かめ、違えば値の出どころ（--show-origin）を表示して止める
//   （worktree 単位の設定や include で読み込んだ値は、ローカルの値より後に読まれて効くことがある）。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset-all core.hooksPath
import fs from "node:fs";
import path from "node:path";
import { git, hooksPathProblem, repoRoot } from "./lib.mjs";

const unsetHint = "このリポジトリのフックに切り替えるなら、`git config --unset-all core.hooksPath` のあとで、もう一度実行する。";

let root;
try {
  root = repoRoot();
} catch {
  console.error("作業ツリーのルートを特定できない（.git の中や bare リポジトリなど）。リポジトリのルートで実行する。");
  process.exit(1);
}
const desired = `${root}/.githooks`;

/** git config の値の一覧。無ければ空の配列。 */
function configAll(args) {
  try {
    return git(["config", ...args, "--get-all", "core.hooksPath"]).split(/\r?\n/).filter(Boolean);
  } catch {
    return [];
  }
}

/** 実際に効いている値を確かめ、問題があれば出どころを表示して止める。 */
function verifyEffective(done) {
  const effective = configAll([]).at(-1) ?? "";
  const problem = hooksPathProblem(effective);
  if (problem) {
    let origin = "";
    try {
      origin = git(["config", "--show-origin", "--get-all", "core.hooksPath"]);
    } catch {
      // 出どころを取れなくても、理由は表示する。
    }
    console.error(
      `${done}が、実際に効いている core.hooksPath に問題がある（${problem}）。値の出どころ:\n${origin}\n` +
        "worktree 単位の設定（config.worktree）や include で読み込んだ設定の値なら、そのファイルの core.hooksPath を消してから、もう一度実行する。",
    );
    process.exit(1);
  }
}

const locals = configAll(["--local"]);
if (locals.length > 1) {
  console.error(`core.hooksPath がこのクローンの設定に ${locals.length} 個ある。上書きせずに止める。${unsetHint}`);
  process.exit(1);
}
const before = locals[0] ?? "";

if (before && path.isAbsolute(before)) {
  if (!hooksPathProblem(before)) {
    verifyEffective("このクローンの設定は正しい");
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
git(["config", "--local", "core.hooksPath", desired]);
verifyEffective(`このクローンの設定を ${desired} にした`);
console.log(`core.hooksPath を ${desired} にした。`);
