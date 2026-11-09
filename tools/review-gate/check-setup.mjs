// Claude Code の PreToolUse hook（Bash / PowerShell）から呼ばれ、「push」を含むコマンドの前に、
// pre-push フックが有効か（lib.mjs の hooksPathProblem）と、照合を外すときに使う語（下の正規表現）を含んでいないかを確かめる。
// 仕組みの全体・防ぐ範囲・防げないものは .claude/skills/pr/SKILL.md の Phase 1 手順 1「承認と push の照合」に書く。
//
// コマンドの文字列は解析せず、語が含まれるかだけを見る（迷う場合は止める側に倒す）。止めるときは終了コード 2
// （Claude Code の hook は、onFailure を指定しなければ終了コード 2 のときだけ止める）。判定の途中のエラーでは止めない。
//
// 手で試す:
//   echo '{"tool_input":{"command":"git push"}}' | CLAUDE_PROJECT_DIR="$PWD" node tools/review-gate/check-setup.mjs; echo $?
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { hooksPathProblem } from "./lib.mjs";

/** 照合を外すときに使う語。網羅ではない。 */
const bypassWords =
  /--no-veri|hookspath|include\.?path|includeif|CLAUDECODE|--ignore-e|\benv\s+-|\bexec(?:\s+-a\s+\S+|\s+-[a-z]+)*\s+-[a-z]*c|UseNewEnvironment|\bwsl\b|git[-_]dir|work[-_]tree/i;

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
  const word = command.match(bypassWords);
  if (word) {
    block(
      `push を含むコマンドに、照合を外すときに使う語（${word[0]}）が含まれている。照合を外さずに push する。` +
        "メッセージに書いただけなら、Write ツールで作業ツリーの外に置いたファイルから `git commit -F \"<ファイル>\"` で渡す。" +
        "ブランチ名に含むなら、ブランチ名を変える。",
    );
  }
  const project = process.env.CLAUDE_PROJECT_DIR;
  const projectTop = project ? gitOut(["rev-parse", "--show-toplevel"], project) : null;
  if (!projectTop) {
    process.exit(0);
  }
  const problem = hooksPathProblem(gitOut(["config", "--get", "core.hooksPath"], projectTop), projectTop);
  if (problem) {
    block(`git の pre-push フックが有効になっていない（${problem}）。\`node tools/review-gate/setup.mjs\` を実行して、表示に従う。`);
  }
  if (!fs.existsSync(path.join(projectTop, ".githooks", "pre-push"))) {
    block("このチェックアウトに照合の仕組み（.githooks/pre-push）が入っていない。照合の仕組みが入った main を取り込んでから push する。");
  }
  process.exit(0);
} catch {
  process.exit(0);
}
