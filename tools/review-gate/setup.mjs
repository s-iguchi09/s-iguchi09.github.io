// このクローンの git の設定（.git/config）で、フックの置き場所を .githooks の絶対パスにする。
// これで、通常の push では .githooks/pre-push（承認した内容との照合）が動く。相対パス（.githooks）にしないのは、
// .git の中や --git-dir・GIT_DIR で作業ツリーのルートが変わると、相対パスが別の場所を指し、フックが黙って動かなくなるため。
// core.hooksPath は同じリポジトリの worktree の間で共有されるので、どれか 1 つのチェックアウトの .githooks を指せば、
// すべての worktree の push でフックが動く（各 worktree の照合のスクリプトは、その worktree のものを使う）。
// フックが動かない場合（--no-verify など）は、pr スキルの「仕組みで防げないもの」を参照。
// 照合するのは CLAUDECODE=1 の環境からの push だけ（pre-push.mjs を参照）。
//
// 動き:
//   - 未設定、または以前の版が設定した相対パス（.githooks）なら、このチェックアウトの .githooks の絶対パスにする。
//   - 既に、このリポジトリのチェックアウト（別の worktree を含む）の pre-push のある絶対パスなら、変更しない。
//   - 絶対パスだが、その場所に pre-push が無い（指していた worktree を消した・移したなど）なら、置き換える。
//   - それ以外（別のリポジトリや別のフックの置き場所）は、上書きせずに止める。
//
// 使い方（リポジトリのルートで、クローンごとに 1 回実行する。Web 実行環境ではセッションの初めに実行する）:
//   node tools/review-gate/setup.mjs
// 元に戻す:
//   git config --unset core.hooksPath
import fs from "node:fs";
import path from "node:path";
import { git, repoRoot } from "./lib.mjs";

const desired = `${repoRoot()}/.githooks`;
const before = (() => {
  try {
    return git(["config", "--get", "core.hooksPath"]);
  } catch {
    return "";
  }
})();

/** パスを比べられる形にする（Windows では大文字小文字と区切りの違いを無視する）。 */
function norm(p) {
  const r = path.resolve(p).replace(/\\/g, "/");
  return process.platform === "win32" ? r.toLowerCase() : r;
}

/** dir が属するリポジトリの共通の .git の絶対パス。取れなければ null。 */
function commonDir(dir) {
  try {
    return norm(git(["rev-parse", "--path-format=absolute", "--git-common-dir"], { cwd: dir }));
  } catch {
    return null;
  }
}

if (before && path.isAbsolute(before)) {
  if (fs.existsSync(path.join(before, "pre-push"))) {
    if (commonDir(path.dirname(before)) === commonDir(process.cwd())) {
      console.log(`core.hooksPath は既にこのリポジトリの ${before} になっている（変更しない）。`);
      process.exit(0);
    }
    console.error(
      `core.hooksPath は別のリポジトリのフックの置き場所（${before}）に設定されている。上書きせずに止める。` +
        "このリポジトリのフックに切り替えるなら、`git config --unset core.hooksPath` のあとで、もう一度実行する。",
    );
    process.exit(1);
  }
  console.log(`core.hooksPath の指す ${before} に pre-push が無いので、置き換える。`);
} else if (before && before !== ".githooks") {
  console.error(
    `core.hooksPath は既に ${before} に設定されている。上書きせずに止める。` +
      "このリポジトリのフックに切り替えるなら、`git config --unset core.hooksPath` のあとで、もう一度実行する。",
  );
  process.exit(1);
}
git(["config", "core.hooksPath", desired]);
console.log(`core.hooksPath を ${desired} にした。`);
