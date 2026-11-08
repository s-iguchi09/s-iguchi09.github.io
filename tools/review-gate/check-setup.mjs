// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、push しそうなコマンドの前に次を確かめる。
// 照合そのものは pre-push.mjs（git の pre-push フック）が行う。ここは、照合が動く状態かと、
// 照合を外す語を含んでいないかだけを、コマンドの文字列から単純に見る。
//
// シェルの構文（引用符・ヒアドキュメント・エスケープ・区切り）は解析しない。解析すると書き方の穴が尽きないため、
// 元のコマンドの文字列に語が含まれるかだけで判定し、迷う場合は止める側に倒す。
//
//   1. コマンドに「push」を含めば（大文字小文字を問わず、部分一致）、$CLAUDE_PROJECT_DIR のリポジトリで
//      core.hooksPath が .githooks か、今のチェックアウトに .githooks/pre-push があるかを確かめ、無ければ止める。
//      作業ディレクトリや push の対象のリポジトリは見ない（フックが有効なら、関係の無いコマンドは止まらない）。
//   2. コマンドに「push」を含み、さらに照合を外す語（`--no-veri`、`hookspath`、`CLAUDECODE`、`--ignore-environment`、
//      環境を空にする `env -i`・`env -`）のどれかを含めば止める。メッセージや引用符の中に書いた語でも止まる
//      （止める側の誤り。そうした語を含むコミットメッセージは `git commit -F <ファイル>` で渡す）。
//
// 確かめられないもの: 変数・別名・スクリプトの中で組み立てる push と照合を外す操作、Bash・PowerShell 以外の経路。
//
// 止めるときは終了コード 2（Claude Code の hook は、既定（onFailure 未指定）では終了コード 2 のときだけツールの実行を止め、
// 標準エラーを Claude に返す）。判定の途中のエラーでは止めない（この hook が壊れて、関係の無いコマンドまで止めないため）。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"}}' | CLAUDE_PROJECT_DIR="$PWD" node tools/review-gate/check-setup.mjs; echo $?
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";

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
  if (/--no-veri|hookspath|CLAUDECODE|--ignore-environment|\benv\s+(?:-[a-z]*i\b|-(?=\s))/i.test(command)) {
    block(
      "push を含むコマンドに、照合を外す語（--no-verify とその省略形、hooksPath、CLAUDECODE、環境を空にする env）が含まれている。" +
        "照合を外さずに push する。メッセージに書いただけなら、メッセージをファイルにして `git commit -F <ファイル>` などで渡す。",
    );
  }
  const project = process.env.CLAUDE_PROJECT_DIR;
  const projectTop = project ? gitOut(["rev-parse", "--show-toplevel"], project) : null;
  if (!projectTop) {
    process.exit(0);
  }
  if (gitOut(["config", "--get", "core.hooksPath"], projectTop) !== ".githooks") {
    block("git の pre-push フックが有効になっていない（core.hooksPath が .githooks でない）。`node tools/review-gate/setup.mjs` を実行してから、もう一度実行する。");
  }
  if (!fs.existsSync(path.join(projectTop, ".githooks", "pre-push"))) {
    block("今のチェックアウトに .githooks/pre-push が無いので、push の照合が動かない。照合の仕組みが入った main を取り込んでから push する。");
  }
  process.exit(0);
} catch {
  process.exit(0);
}
