// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、push しそうなコマンドの前に次を確かめる。
// 照合そのものは pre-push.mjs（git の pre-push フック）が行う。ここは、照合が動く状態かと、
// 照合を外す語を含んでいないかだけを、コマンドの文字列から単純に見る。
//
// シェルの構文（引用符・ヒアドキュメント・エスケープ・区切り）は解析しない。解析すると書き方の穴が尽きないため、
// 元のコマンドの文字列に語が含まれるかだけで判定し、迷う場合は止める側に倒す。
//
//   1. コマンドに「push」を含めば（大文字小文字を問わず、部分一致）、$CLAUDE_PROJECT_DIR のリポジトリで
//      core.hooksPath が、同じリポジトリのチェックアウトのルート直下の、pre-push のある .githooks の絶対パスであること
//      （lib.mjs の hooksPathProblem）と、今のチェックアウトに .githooks/pre-push があることの両方を確かめ、
//      どちらかが欠けていれば止める。作業ディレクトリや push の対象のリポジトリは見ない（フックが有効なら、関係の無いコマンドは止まらない）。
//   2. コマンドに「push」を含み、さらに照合を外すときに使う語（`--no-veri`、`hookspath`、`include.path`・`includeIf`、`CLAUDECODE`、
//      `--ignore-e`、オプションを付けた `env`、環境を空にする `exec -c`、`UseNewEnvironment`、`wsl`、`git-dir`・`GIT_DIR`、
//      `work-tree`・`GIT_WORK_TREE`）の
//      どれかを含めば止める。網羅ではない。`git-dir` などで作業ツリーのルートが別の場所になった push は、フックの sh 側でも
//      止まるか（ルートを特定できない・そこに照合のスクリプトが無い）照合されるが、
//      理由が分かりやすいようにここでも止める。ブランチ名にこれらの語を含んでも止まるので、ブランチ名には使わない。
//      メッセージや引用符・ヒアドキュメントの中に書いた語でも止まる（止める側の誤り。そうした語を含むコミットメッセージは、
//      作業ツリーの外に置いたファイルから `git commit -F "<ファイル>"` で渡す。作業ツリーの中に置くと、git add -A でコミットに入り、
//      承認した内容と一致しなくなる。ファイルは Write ツールで作る。ヒアドキュメントや echo で書くと、そのコマンドが止まる）。
//
// 確かめられないもの: 上に無い語や書き方、変数・別名・スクリプトの中で組み立てる push と照合を外す操作、
// フックを有効にしていない別のクローン（cd・git -C で移った先）からの push、core.hooksPath の指すチェックアウトを
// .githooks の無い（照合の仕組みが入る前の）ブランチやコミットに切り替えた後の push（同じコマンドの中でも、別のコマンドでも。
// 切り替えるとこのスクリプトも無くなるので、hook の設定が残っていても実行できない）、照合のフック・スクリプト・承認の記録の書き換え、
// git を通さずにリモートのブランチを書き換える操作（GitHub の API・MCP・git send-pack など）、Bash・PowerShell 以外の経路。
//
// 止めるときは終了コード 2（Claude Code の hook は、既定（onFailure 未指定）では終了コード 2 のときだけツールの実行を止め、
// 標準エラーを Claude に返す）。判定の途中のエラーでは止めない（この hook が壊れて、関係の無いコマンドまで止めないため）。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"}}' | CLAUDE_PROJECT_DIR="$PWD" node tools/review-gate/check-setup.mjs; echo $?
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { hooksPathProblem } from "./lib.mjs";

/** git を実行して標準出力を返す。失敗したら null。 */
function gitOut(args, cwd) {
  try {
    return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return null;
  }
}

/** 理由を標準エラーに書き、終了コード 2 で止める。 */
function block(message) {
  process.stderr.write(`[review-gate] ${message}\n`);
  process.exit(2);
}

try {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk;
  }
  const command = JSON.parse(input)?.tool_input?.command;
  if (typeof command !== "string" || !/push/i.test(command)) {
    process.exit(0);
  }
  if (/--no-veri|hookspath|include\.?path|includeif|CLAUDECODE|--ignore-e|\benv\s+-|\bexec\s+-\w*c|UseNewEnvironment|\bwsl\b|git[-_]dir|work[-_]tree/i.test(command)) {
    block(
      "push を含むコマンドに、照合を外すときに使う語（--no-verify とその省略形、hooksPath、include.path・includeIf、CLAUDECODE、" +
        "--ignore-environment とその省略形、オプションを付けた env、exec -c、UseNewEnvironment、wsl、git-dir・GIT_DIR、work-tree・GIT_WORK_TREE）が含まれている。照合を外さずに push する。" +
        "メッセージに書いただけなら、Write ツールで作業ツリーの外に置いたファイルから `git commit -F \"<ファイル>\"` などで渡す。" +
        "ブランチ名に含むなら、ブランチ名を変える。",
    );
  }
  const project = process.env.CLAUDE_PROJECT_DIR;
  const projectTop = project ? gitOut(["rev-parse", "--show-toplevel"], project) : null;
  if (!projectTop) {
    process.exit(0);
  }
  // 相対パスは、.git の中や --git-dir での push で別の場所を指し、フックが黙って動かなくなるので受け付けない。
  // 同じリポジトリのチェックアウトのルート直下の、pre-push のある .githooks の絶対パスだけを有効とみなす。
  const problem = hooksPathProblem(gitOut(["config", "--get", "core.hooksPath"], projectTop), projectTop);
  if (problem) {
    block(
      `git の pre-push フックが有効になっていない（${problem}）。` +
        "`node tools/review-gate/setup.mjs` を実行してから、もう一度実行する（setup が上書きを拒んだら、" +
        "`git config --unset-all core.hooksPath` のあとで setup を実行し直す。setup が値の出どころを表示したら、そのファイルの値を消す）。",
    );
  }
  if (!fs.existsSync(path.join(projectTop, ".githooks", "pre-push"))) {
    block("今のチェックアウトに照合の仕組み（.githooks/pre-push）が入っていない。照合の仕組みが入った main を取り込んでから push する。");
  }
  process.exit(0);
} catch {
  process.exit(0);
}
