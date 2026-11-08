// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、git push のコマンドの前に次を確かめる。
// 照合そのものは pre-push.mjs（git の pre-push フック）が行う。ここは、照合が動く状態かと、
// 照合を外すよく知られた書き方をしていないかだけを、コマンドの文字列から見る。
//
// 対象: 引用符の中身とヒアドキュメントの中身を除いたうえで、`git` の次のサブコマンドが `push` のもの
//       （git の前置きのオプション `-C <dir>`・`-c <k=v>` などは読み飛ばす）。`git stash push` やメッセージの中の文字は対象外。
//       対象のリポジトリは $CLAUDE_PROJECT_DIR のリポジトリ（作業ディレクトリがその作業ツリーか、コマンドにそのパスを含む場合）。
//       同じリポジトリの別の worktree からの push は対象外。
// 確かめること:
//   - core.hooksPath が .githooks か、今のチェックアウトに .githooks/pre-push があるか
//   - コマンド全体（引用符の外）に、次の書き方が無いか: `--no-veri` で始まるオプション（--no-verify とその省略形）、
//     `core.hooksPath`、CLAUDECODE の上書き・削除（`CLAUDECODE=`、`unset CLAUDECODE`、`env -u CLAUDECODE`、`env -i`、
//     PowerShell の `$env:CLAUDECODE`・`Env:CLAUDECODE`）
// 確かめられないもの: スクリプトや別名の中で実行する push、上に無い書き方、Bash・PowerShell 以外の経路。
//
// 止めるときは終了コード 2（Claude Code は終了コードでは 2 のときだけツールの実行を止め、標準エラーを Claude に返す）。
// 判定の途中のエラーでは止めない（この hook が壊れて、関係の無いコマンドまで止めないため）。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"},"cwd":"."}' | CLAUDE_PROJECT_DIR="$PWD" node tools/review-gate/check-setup.mjs; echo $?
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

/** ヒアドキュメント・ヒア文字列・引用符の中身を除いた文字列を返す（左から 1 文字ずつ読む）。 */
function stripQuoted(command, powershell) {
  const noHeredoc = command
    .replace(/<<-?\s*(['"]?)(\w+)\1[^\n]*\n[\s\S]*?\n\s*\2\s*(?=\n|$)/g, " ")
    .replace(/@'[\s\S]*?'@|@"[\s\S]*?"@/g, " ");
  let out = "";
  let quote = null;
  for (let i = 0; i < noHeredoc.length; i++) {
    const ch = noHeredoc[i];
    if (quote) {
      if (!powershell && quote === '"' && ch === "\\") {
        i++;
      } else if (ch === quote) {
        quote = null;
        out += " ";
      }
      continue;
    }
    if (ch === "'" || ch === '"') {
      quote = ch;
      continue;
    }
    out += ch;
  }
  return out;
}

try {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk;
  }
  const data = JSON.parse(input);
  const command = data?.tool_input?.command;
  if (typeof command !== "string") {
    process.exit(0);
  }
  const plain = stripQuoted(command, data?.tool_name === "PowerShell");
  // git（git.exe を含む）の後に、前置きのオプションを飛ばして push が来るか。
  const pushRe = /(?:^|[\s;&|(])git(?:\.exe)?(?:\s+(?:-[Cc]\s+\S+|--?[\w-]+(?:=\S+)?))*\s+push\b/i;
  if (!pushRe.test(plain)) {
    process.exit(0);
  }
  const project = process.env.CLAUDE_PROJECT_DIR;
  if (!project) {
    process.exit(0);
  }
  const projectTop = gitOut(["rev-parse", "--show-toplevel"], project);
  if (!projectTop) {
    process.exit(0);
  }
  const cwd = typeof data.cwd === "string" && data.cwd ? data.cwd : process.cwd();
  const cwdTop = gitOut(["rev-parse", "--show-toplevel"], cwd);
  /** パスを比べられる形（絶対パス・スラッシュ・小文字）にする。 */
  const norm = (p) => path.resolve(p).replace(/\\/g, "/").toLowerCase();
  // パスは引用符で囲んで書くことが多いので、リポジトリのパスは元のコマンドから探す。
  const raw = command.replace(/\\/g, "/").toLowerCase();
  const inProject =
    (cwdTop && norm(cwdTop) === norm(projectTop)) ||
    raw.includes(norm(projectTop)) ||
    raw.includes(projectTop.replace(/\\/g, "/").toLowerCase());
  if (!inProject) {
    process.exit(0);
  }

  if (/--no-veri\w*|core\.hooksPath|CLAUDECODE\s*=|unset\s+CLAUDECODE|env\s+-u\s*CLAUDECODE|\benv\s+-i\b|\$env:CLAUDECODE|Env:\\?CLAUDECODE/i.test(plain)) {
    block("push の照合を外す書き方（--no-verify とその省略形、core.hooksPath の変更、CLAUDECODE の上書き・削除）を含むコマンドは実行しない。照合を通して push する。");
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
