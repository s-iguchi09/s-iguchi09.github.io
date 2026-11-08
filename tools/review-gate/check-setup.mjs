// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、git push の前に次を確かめる。
// 照合そのものは pre-push.mjs（git の pre-push フック）が行う。ここは、照合が動く状態かと、
// 照合を外す書き方をしていないかだけを見る。
//
//   - このリポジトリ（$CLAUDE_PROJECT_DIR）で core.hooksPath が .githooks になっているか、
//     今のチェックアウトに .githooks/pre-push があるか
//   - push のコマンドに、照合を外す書き方（--no-verify、core.hooksPath の上書き、CLAUDECODE の上書き）が無いか
//
// 対象は、; & | 改行 で区切った中に「git」と「push」の両方を含むもの（push は部分一致で見る）。作業ディレクトリが
// このリポジトリの外なら止めない。止めるときは終了コード 2（Claude Code は終了コードでは 2 のときだけツールの実行を止め、
// 標準エラーを Claude に返す）。判定の途中のエラーでは止めない（この hook が壊れて、関係の無いコマンドまで止めないため）。
// コマンドの文字列だけを見るので、スクリプトの中から実行する push などは確かめられない。
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
  // コマンドを ; & | 改行 で区切り、「git」と「push」を同じ区切りの中に含むものだけを対象にする
  // （引用符は考えない。ほかのコマンドの文字列に push と書いただけでは対象にしないため）。
  const pushSegments = command.split(/[;&|\n]/).filter((s) => /\bgit\b/i.test(s) && /push/i.test(s));
  if (pushSegments.length === 0) {
    process.exit(0);
  }
  const project = process.env.CLAUDE_PROJECT_DIR;
  if (!project) {
    process.exit(0);
  }
  const projectTop = gitOut(["rev-parse", "--show-toplevel"], project);
  const cwd = typeof data.cwd === "string" && data.cwd ? data.cwd : process.cwd();
  const cwdTop = gitOut(["rev-parse", "--show-toplevel"], cwd);
  if (!projectTop || !cwdTop || path.resolve(projectTop) !== path.resolve(cwdTop)) {
    process.exit(0);
  }

  if (pushSegments.some((s) => /--no-verify\b|core\.hooksPath|(?:^|\s)CLAUDECODE\s*=|env\s+-u\s+CLAUDECODE/i.test(s))) {
    block("push の照合を外す書き方（--no-verify、core.hooksPath の上書き、CLAUDECODE の上書き）は使わない。照合を通して push する。");
  }
  if (gitOut(["config", "--get", "core.hooksPath"], projectTop) !== ".githooks") {
    block("git の pre-push フックが有効になっていない（core.hooksPath が .githooks でない）。`node tools/review-gate/setup.mjs` を実行してから、もう一度実行する。");
  }
  if (!fs.existsSync(path.join(projectTop, ".githooks", "pre-push"))) {
    block("今のチェックアウトに .githooks/pre-push が無いので、push の照合が動かない。照合の仕組みが入った main からブランチを作り直すか、取り込んでから push する。");
  }
  process.exit(0);
} catch {
  process.exit(0);
}
