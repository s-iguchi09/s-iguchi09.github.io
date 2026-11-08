// review-gate の照合（pre-push.mjs）・承認（approve.mjs）・有効化の確認（check-setup.mjs）を、
// 一時ディレクトリに作ったリポジトリで試す。このリポジトリの記録や設定には触らない。
//
// 使い方:
//   node tools/review-gate/test-gate.mjs
// 終了コード: すべて期待どおりなら 0、1 つでも違えば 1。
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const prePush = path.join(here, "pre-push.mjs");
const approve = path.join(here, "approve.mjs");
const checkSetup = path.join(here, "check-setup.mjs");

const root = fs.mkdtempSync(path.join(os.tmpdir(), "review-gate-test-"));
const repo = path.join(root, "repo");
const remote = path.join(root, "remote.git");
const outside = path.join(root, "outside");
let failed = 0;

/** git を実行する（試験用のリポジトリで）。 */
function g(args, cwd = repo) {
  return execFileSync("git", args, { cwd, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] }).trim();
}

/** 結果を期待と比べて表示する。 */
function expect(name, actual, expected) {
  const ok = actual === expected;
  if (!ok) failed++;
  console.log(`${ok ? "OK " : "NG "} ${name}: ${actual}（期待 ${expected}）`);
}

/** コマンドを実行して終了コードを返す。 */
function run(cmd, args, { input = "", env = {}, cwd = repo } = {}) {
  const r = spawnSync(cmd, args, { cwd, input, encoding: "utf8", env: { ...process.env, ...env } });
  return r.status;
}

/** 本物の git push を実行し、終了コードを返す。 */
function push(args, claude = true) {
  const env = { ...process.env };
  if (claude) env.CLAUDECODE = "1";
  else delete env.CLAUDECODE;
  return spawnSync("git", ["push", ...args], { cwd: repo, encoding: "utf8", env }).status;
}

try {
  fs.mkdirSync(repo);
  fs.mkdirSync(outside);
  g(["init", "-q", "--bare", remote], root);
  g(["init", "-q", "-b", "work"]);
  g(["config", "user.email", "test@example.com"]);
  g(["config", "user.name", "test"]);
  g(["remote", "add", "origin", remote]);
  fs.mkdirSync(path.join(repo, ".githooks"));
  fs.writeFileSync(path.join(repo, ".githooks", "pre-push"), `#!/bin/sh\nexec node "${prePush.replace(/\\/g, "/")}" "$@"\n`);
  fs.chmodSync(path.join(repo, ".githooks", "pre-push"), 0o755);
  fs.writeFileSync(path.join(repo, "a.txt"), "1\n");
  g(["add", "-A"]);
  g(["commit", "-q", "-m", "first"]);

  // ---- check-setup.mjs ----
  const hookInput = (command, cwd = repo) => JSON.stringify({ tool_input: { command }, cwd });
  expect("check-setup: push を含まない", run("node", [checkSetup], { input: hookInput("ls -la") }), 0);
  expect("check-setup: フックが無効で push", run("node", [checkSetup], { input: hookInput("git log -n 1 && git push 2>&1 | tail -n 5") }), 2);
  // ホームディレクトリなど上位がリポジトリになっている環境でも「外」になるよう、git が上を探さないようにする。
  expect("check-setup: リポジトリの外", run("node", [checkSetup], { input: hookInput("git push", outside), env: { GIT_CEILING_DIRECTORIES: root } }), 0);
  expect("check-setup: command が文字列でない", run("node", [checkSetup], { input: JSON.stringify({ tool_input: { command: 1 } }) }), 0);
  expect("check-setup: 壊れた入力", run("node", [checkSetup], { input: "{" }), 0);
  g(["config", "core.hooksPath", ".githooks"]);
  expect("check-setup: フックが有効で push", run("node", [checkSetup], { input: hookInput("git push") }), 0);

  // ---- approve.mjs ----
  expect("approve: start より前の done", run("node", [approve, "done", "x"]), 1);
  expect("approve: start", run("node", [approve, "start"]), 0);
  fs.writeFileSync(path.join(repo, "a.txt"), "2\n");
  expect("approve: start の後に変えて done", run("node", [approve, "done", "x"]), 1);
  expect("approve: start をやり直す", run("node", [approve, "start"]), 0);
  expect("approve: 変えずに done", run("node", [approve, "done", "要修正 0 件"]), 0);
  g(["commit", "-qam", "second"]);

  // ---- 本物の git push と pre-push.mjs ----
  expect("push: 承認と同じ内容（Claude Code から）", push(["-u", "origin", "work"]), 0);
  fs.writeFileSync(path.join(repo, "a.txt"), "3\n");
  g(["commit", "-qam", "third"]);
  expect("push: 承認の後に変えた（Claude Code から）", push(["origin", "work"]), 1);
  expect("push: 承認の後に変えた（-c と後ろのパイプ相当の書き方）", spawnSync("git", ["-c", "core.x=y", "push", "origin", "HEAD:work"], { cwd: repo, encoding: "utf8", env: { ...process.env, CLAUDECODE: "1" } }).status, 1);
  expect("push: 承認の後に変えた（人が手で push）", push(["origin", "work"], false), 0);
  g(["branch", "other", "HEAD~1"]);
  expect("push: 承認した内容の別のブランチ", push(["origin", "other"]), 0);
  expect("push: 削除", push(["origin", "--delete", "other"]), 0);
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
