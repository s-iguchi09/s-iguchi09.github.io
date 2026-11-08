// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、push しそうなコマンドの前に、
// git の pre-push フック（.githooks/pre-push）が有効になっているかを確かめる。
// 照合そのものは pre-push.mjs が行う。ここはフックの有効化を忘れていないかだけを見る。
//
// - コマンドに「push」という語が含まれ、かつ core.hooksPath が .githooks でなければ、終了コード 2 で止める
//   （Claude Code は終了コード 2 のときだけツールの実行を止め、標準エラーを Claude に返す）。
// - それ以外と、判定の途中のエラーでは止めない（この hook が壊れて、関係の無いコマンドまで止めないため）。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"},"cwd":"."}' | node tools/review-gate/check-setup.mjs; echo $?
import { execFileSync } from "node:child_process";

try {
  let input = "";
  for await (const chunk of process.stdin) {
    input += chunk;
  }
  const data = JSON.parse(input);
  const command = data?.tool_input?.command;
  if (typeof command !== "string" || !/push/i.test(command)) {
    process.exit(0);
  }
  const cwd = typeof data.cwd === "string" && data.cwd ? data.cwd : process.cwd();
  let hooksPath = "";
  try {
    hooksPath = execFileSync("git", ["config", "--get", "core.hooksPath"], { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    // 未設定（終了コード 1）か、リポジトリの外。リポジトリの外なら止めない。
    try {
      execFileSync("git", ["rev-parse", "--git-dir"], { cwd, stdio: "ignore" });
    } catch {
      process.exit(0);
    }
  }
  if (hooksPath !== ".githooks") {
    process.stderr.write(
      "[review-gate] git の pre-push フックが有効になっていない（core.hooksPath が .githooks でない）。" +
        "`node tools/review-gate/setup.mjs` を実行してから、もう一度実行する。\n",
    );
    process.exit(2);
  }
  process.exit(0);
} catch {
  process.exit(0);
}
