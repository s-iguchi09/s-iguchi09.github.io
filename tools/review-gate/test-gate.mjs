// review-gate の照合（.githooks/pre-push と pre-push.mjs）・承認（approve.mjs）・hook の確認（check-setup.mjs）を、
// 一時ディレクトリに作ったリポジトリで試す。このリポジトリの本物の .githooks/pre-push と tools/review-gate を
// 一時リポジトリにコピーして使う。このリポジトリの記録や設定には触らない。
//
// 使い方（リポジトリのルートで実行する）:
//   node tools/review-gate/test-gate.mjs
// 終了コード: すべて期待どおりなら 0、1 つでも違えば 1。
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.resolve(here, "..", "..");

const root = fs.mkdtempSync(path.join(os.tmpdir(), "review-gate-test-"));
const repo = path.join(root, "repo");
const other = path.join(root, "other");
const remote = path.join(root, "remote.git");
let failed = 0;

// 呼び出し元の GIT_DIR などを引き継ぐと、別のリポジトリに書き込むおそれがあるので外す。
const baseEnv = Object.fromEntries(Object.entries(process.env).filter(([k]) => !k.startsWith("GIT_")));

/** git を実行する（既定は試験用のリポジトリで）。 */
function g(args, cwd = repo) {
  return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], env: baseEnv }).trim();
}

/** 結果を期待と比べて表示する。 */
function expect(name, actual, expected) {
  const ok = actual === expected;
  if (!ok) failed++;
  console.log(`${ok ? "OK " : "NG "} ${name}: ${actual}（期待 ${expected}）`);
}

/** コマンドを実行して終了コードを返す。 */
function run(cmd, args, { input = "", env = {}, cwd = repo } = {}) {
  return spawnSync(cmd, args, { cwd, input, encoding: "utf8", env: { ...baseEnv, ...env } }).status;
}

/** 本物の git push を実行し、終了コードを返す。claude が false なら CLAUDECODE を外す。 */
function push(args, claude = true) {
  const env = { ...baseEnv };
  if (claude) env.CLAUDECODE = "1";
  else delete env.CLAUDECODE;
  return spawnSync("git", args[0] === "-c" ? args : ["push", ...args], { cwd: repo, encoding: "utf8", env }).status;
}

try {
  // ---- 試験用のリポジトリ（本物のフックとスクリプトをコピー）と、裸のリモート、別のリポジトリ ----
  fs.mkdirSync(repo);
  fs.mkdirSync(other);
  g(["init", "-q", "--bare", remote], root);
  g(["init", "-q", "-b", "work"]);
  g(["config", "user.email", "test@example.com"]);
  g(["config", "user.name", "test"]);
  g(["remote", "add", "origin", remote]);
  fs.cpSync(path.join(source, ".githooks"), path.join(repo, ".githooks"), { recursive: true });
  fs.chmodSync(path.join(repo, ".githooks", "pre-push"), 0o755);
  fs.mkdirSync(path.join(repo, "tools"), { recursive: true });
  fs.cpSync(here, path.join(repo, "tools", "review-gate"), { recursive: true });
  fs.writeFileSync(path.join(repo, "a.txt"), "1\n");
  g(["add", "-A"]);
  g(["commit", "-q", "-m", "first"]);
  g(["init", "-q"], other);

  const checkSetup = path.join(repo, "tools", "review-gate", "check-setup.mjs");
  const approve = path.join(repo, "tools", "review-gate", "approve.mjs");
  const prePush = path.join(repo, "tools", "review-gate", "pre-push.mjs");
  /** Claude Code の PreToolUse hook に渡る入力の JSON を作る。 */
  const hookInput = (command, cwd = repo) => JSON.stringify({ tool_input: { command }, cwd });
  const projectEnv = { CLAUDE_PROJECT_DIR: repo };
  /** check-setup.mjs を、このリポジトリを $CLAUDE_PROJECT_DIR として実行する。 */
  const check = (command, cwd = repo) => run("node", [checkSetup], { input: hookInput(command, cwd), env: projectEnv });

  // ---- check-setup.mjs ----
  expect("check-setup: git も push も含まない", check("ls -la"), 0);
  expect("check-setup: git を含まない push（Push-Location）", check("Push-Location C:/x; Pop-Location"), 0);
  expect("check-setup: 別の区切りに git と push がある", check("echo push && git status"), 0);
  expect("check-setup: フックが無効で push（後ろにパイプ）", check("git push 2>&1 | tail -n 5"), 2);
  expect("check-setup: 別のリポジトリでの push", check("git push", other), 0);
  expect("check-setup: command が文字列でない", run("node", [checkSetup], { input: JSON.stringify({ tool_input: { command: 1 } }), env: projectEnv }), 0);
  expect("check-setup: 壊れた入力", run("node", [checkSetup], { input: "{", env: projectEnv }), 0);
  g(["config", "core.hooksPath", ".githooks"]);
  expect("check-setup: フックが有効で push", check("git push -u origin work"), 0);
  expect("check-setup: git stash push", check("git stash push -m x"), 0);
  expect("check-setup: --no-verify", check("git push --no-verify origin work"), 2);
  expect("check-setup: core.hooksPath の上書き", check("git -c core.hooksPath=/x push"), 2);
  expect("check-setup: CLAUDECODE の上書き", check("CLAUDECODE= git push"), 2);
  fs.renameSync(path.join(repo, ".githooks"), path.join(repo, ".githooks-off"));
  expect("check-setup: .githooks が無いチェックアウト", check("git push"), 2);
  fs.renameSync(path.join(repo, ".githooks-off"), path.join(repo, ".githooks"));

  // ---- approve.mjs ----
  expect("approve: start より前の done", run("node", [approve, "done", "x"]), 1);
  expect("approve: start", run("node", [approve, "start"]), 0);
  fs.writeFileSync(path.join(repo, "a.txt"), "2\n");
  expect("approve: start の後に変えて done", run("node", [approve, "done", "x"]), 1);
  expect("approve: start をやり直す", run("node", [approve, "start"]), 0);
  expect("approve: 変えずに done", run("node", [approve, "done", "要修正 0 件"]), 0);
  g(["commit", "-qam", "second"]);

  // ---- 本物の git push（.githooks/pre-push → pre-push.mjs） ----
  expect("push: 承認と同じ内容", push(["-u", "origin", "work"]), 0);
  fs.writeFileSync(path.join(repo, "a.txt"), "3\n");
  g(["commit", "-qam", "third"]);
  expect("push: 承認の後に変えた", push(["origin", "work"]), 1);
  expect("push: 承認の後に変えた（-c 付き・ブランチの指定を変えた書き方）", push(["-c", "core.x=y", "push", "origin", "HEAD:work"]), 1);
  expect("push: 承認の後に変えた（CLAUDECODE の無い環境）", push(["origin", "work"], false), 0);
  g(["branch", "other", "HEAD~1"]);
  expect("push: 承認した内容と同じ別のブランチ", push(["origin", "other"]), 0);
  expect("push: 削除", push(["origin", "--delete", "other"]), 0);
  g(["tag", "v1"]);
  expect("push: タグ（承認していない内容を指す）", push(["origin", "v1"]), 0);
  fs.writeFileSync(path.join(repo, ".git", "review-gate", "approved.json"), "{ broken");
  expect("push: 記録が壊れている", push(["origin", "HEAD:work2"]), 1);
  fs.rmSync(path.join(repo, ".git", "review-gate", "approved.json"));
  expect("push: 記録が無い", push(["origin", "HEAD:work3"]), 1);
  expect("approve: direct", run("node", [approve, "direct", "例外の試験"]), 0);
  expect("push: direct で記録した内容", push(["origin", "HEAD:work4"]), 0);

  // ---- pre-push.mjs を直接（標準入力が空） ----
  expect("pre-push: push する ref が無い", run("node", [prePush, "origin", "x"], { env: { CLAUDECODE: "1" } }), 0);
} finally {
  fs.rmSync(root, { recursive: true, force: true });
}
process.exit(failed ? 1 : 0);
