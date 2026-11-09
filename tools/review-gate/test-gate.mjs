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

/** 作業ディレクトリを指定して git（push を含む引数）を CLAUDECODE=1 で実行する。返す値は push() と同じ。 */
function spawnPush(cwd, args) {
  const r = spawnSync("git", args, { cwd, encoding: "utf8", env: { ...baseEnv, CLAUDECODE: "1" } });
  return r.status === 1 && !`${r.stderr}`.includes("[review-gate]") ? -1 : r.status;
}

/**
 * 本物の git push を実行し、終了コードを返す。claude が false なら環境変数を外す。
 * 終了コード 1 でも、照合の文言（[review-gate]）が無ければ別の理由の失敗なので -1 を返す。
 */
function push(args, claude = true) {
  const env = { ...baseEnv };
  if (claude) env.CLAUDECODE = "1";
  else delete env.CLAUDECODE;
  const r = spawnSync("git", args[0] === "-c" ? args : ["push", ...args], { cwd: repo, encoding: "utf8", env });
  return r.status === 1 && !`${r.stderr}`.includes("[review-gate]") ? -1 : r.status;
}

try {
  // ---- 試験用のリポジトリ（本物のフックとスクリプトをコピー）と、裸のリモート、別のリポジトリ ----
  fs.mkdirSync(repo);
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

  const checkSetup = path.join(repo, "tools", "review-gate", "check-setup.mjs");
  const approve = path.join(repo, "tools", "review-gate", "approve.mjs");
  const prePush = path.join(repo, "tools", "review-gate", "pre-push.mjs");
  const projectEnv = { CLAUDE_PROJECT_DIR: repo };
  /** check-setup.mjs に、Claude Code の PreToolUse hook と同じ形の入力を渡して実行する。 */
  const check = (command) => run("node", [checkSetup], { input: JSON.stringify({ tool_input: { command }, cwd: repo }), env: projectEnv });

  // ---- check-setup.mjs（フックが無効な状態） ----
  expect("check-setup: push を含まない", check("ls -la"), 0);
  expect("check-setup: フックが無効で push", check("git push 2>&1 | tail -n 5"), 2);
  expect("check-setup: フックが無効で push を含む別のコマンド（止める側に倒す）", check("Push-Location C:/x"), 2);
  expect("check-setup: command が文字列でない", run("node", [checkSetup], { input: JSON.stringify({ tool_input: { command: 1 } }), env: projectEnv }), 0);
  expect("check-setup: 壊れた入力", run("node", [checkSetup], { input: "{", env: projectEnv }), 0);
  // ---- check-setup.mjs（フックが有効な状態） ----
  g(["config", "core.hooksPath", ".githooks"]);
  expect("check-setup: core.hooksPath が相対パス", check("git push -u origin work"), 2);
  expect("setup: 相対パスを絶対パスに置き換える", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")]), 0);
  // 別の worktree（core.hooksPath は共有される）で setup を実行しても、変更せずに終わる。
  const wt = path.join(root, "wt");
  g(["worktree", "add", "-q", "-b", "wt", wt]);
  const hooksBefore = g(["config", "--get", "core.hooksPath"]);
  expect("setup: 別の worktree で実行（変更しない）", run("node", [path.join(wt, "tools", "review-gate", "setup.mjs")], { cwd: wt }), 0);
  expect("setup: 別の worktree で実行しても設定は同じ", g(["config", "--get", "core.hooksPath"]) === hooksBefore, true);
  // 指す先に pre-push が無ければ置き換える。
  g(["config", "core.hooksPath", path.join(root, "gone", ".githooks").replace(/\\/g, "/")]);
  expect("setup: 指す先が無い絶対パスを置き換える", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")]), 0);
  expect("setup: 置き換えた後の設定", g(["config", "--get", "core.hooksPath"]) === hooksBefore, true);
  // 同じリポジトリでも、.githooks 以外（.git/hooks など）に pre-push があるだけなら有効とみなさない。
  const otherHooks = path.join(repo, ".git", "hooks");
  fs.mkdirSync(otherHooks, { recursive: true });
  fs.writeFileSync(path.join(otherHooks, "pre-push"), "#!/bin/sh\nexit 0\n");
  g(["config", "core.hooksPath", otherHooks.replace(/\\/g, "/")]);
  expect("check-setup: .githooks 以外の置き場所", check("git push -u origin work"), 2);
  expect("setup: .githooks 以外の置き場所は上書きせずに止める", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")]), 1);
  g(["config", "core.hooksPath", hooksBefore]);
  // グローバルの core.hooksPath は、setup がローカルに設定して上書きする（止まらない）。
  const globalCfg = path.join(root, "global.gitconfig");
  fs.writeFileSync(globalCfg, "[core]\n\thooksPath = ~/.git-hooks\n");
  g(["config", "--local", "--unset", "core.hooksPath"]);
  expect("setup: グローバルに別の core.hooksPath がある", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")], { env: { GIT_CONFIG_GLOBAL: globalCfg } }), 0);
  expect("setup: グローバルがあってもローカルに設定した", g(["config", "--local", "--get", "core.hooksPath"]) === hooksBefore, true);
  // include で読み込んだ別の値が効いていれば、setup は「設定した」で終わらずに止める。
  const incCfg = path.join(root, "inc.gitconfig");
  fs.writeFileSync(incCfg, "[core]\n\thooksPath = C:/elsewhere/hooks\n");
  g(["config", "--local", "include.path", incCfg.replace(/\\/g, "/")]);
  expect("setup: include で別の値が効いている", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")]), 1);
  g(["config", "--local", "--unset", "include.path"]);
  // ローカルに値が複数あれば、スタックトレースを出さずに止める。
  g(["config", "--local", "--add", "core.hooksPath", hooksBefore]);
  expect("setup: ローカルに値が複数ある", run("node", [path.join(repo, "tools", "review-gate", "setup.mjs")]), 1);
  g(["config", "--local", "--unset-all", "core.hooksPath"]);
  g(["config", "--local", "core.hooksPath", hooksBefore]);
  expect("check-setup: フックが有効で push", check("git push -u origin work"), 0);
  expect("check-setup: git stash push", check("git stash push -m x"), 0);
  expect("check-setup: --no-verify", check("git push --no-verify origin work"), 2);
  expect("check-setup: 引用符で囲んだ --no-verify", check('git push "--no-verify"'), 2);
  expect("check-setup: --no-verify の省略形", check("git push origin work --no-veri"), 2);
  expect("check-setup: core.hooksPath の上書き", check("git -c core.hooksPath=/x push"), 2);
  expect("check-setup: 別の区切りで core.hooksPath を変える", check("git config core.hooksPath /dev/null; git push"), 2);
  expect("check-setup: CLAUDECODE の上書き", check("CLAUDECODE+=x git push"), 2);
  expect("check-setup: 別の区切りで CLAUDECODE を消す", check("unset CLAUDECODE; git push"), 2);
  expect("check-setup: env -i", check("env -i PATH=/usr/bin git push"), 2);
  expect("check-setup: env --ignore-environment", check("env --ignore-environment git push"), 2);
  expect("check-setup: env の省略形・まとめ書き", check("env -iu FOO git push"), 2);
  expect("check-setup: exec -c", check("bash -c 'exec -c git push'"), 2);
  expect("check-setup: include.path", check("git -c include.path=/x push"), 2);
  expect("check-setup: 作業ツリーのルートを変える --git-dir", check("cd sub && git --git-dir=../.git push origin work"), 2);
  expect("check-setup: 作業ツリーのルートを変える GIT_DIR", check("GIT_DIR=../.git git push origin work"), 2);
  expect("check-setup: ヒアドキュメントの後ろの --no-verify", check("git commit -F - <<'EOF' && git push --no-verify\nmsg\nEOF"), 2);
  expect("check-setup: メッセージの中の語（止める側に倒す）", check('git commit -m "git push で --no-verify を使わない"'), 2);
  expect("check-setup: メッセージはファイルで渡す", check("git commit -F msg.txt"), 0);
  fs.renameSync(path.join(repo, ".githooks"), path.join(repo, ".githooks-off"));
  expect("check-setup: .githooks が無いチェックアウト", check("git push"), 2);
  fs.renameSync(path.join(repo, ".githooks-off"), path.join(repo, ".githooks"));

  // ---- approve.mjs ----
  expect("approve: start より前の done", run("node", [approve, "done", "x"]), 1);
  fs.mkdirSync(path.join(repo, ".git", "review-gate"), { recursive: true });
  fs.writeFileSync(path.join(repo, ".git", "review-gate", "review-started.json"), "null");
  expect("approve: 開始の記録が null", run("node", [approve, "done", "x"]), 1);
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
  expect("push: タグからブランチへ（承認していない内容）", push(["origin", "refs/tags/v1:refs/heads/fromtag"]), 1);
  expect("push: HEAD をタグへ（承認していない内容）", push(["origin", "HEAD:refs/tags/v2"]), 0);
  expect("push: .git の中から（承認していない内容）", spawnPush(path.join(repo, ".git"), ["push", "origin", "HEAD:refs/heads/ingit"]), 1);
  fs.mkdirSync(path.join(repo, "sub"), { recursive: true });
  expect("push: --git-dir で作業ツリーのルートを変える（承認していない内容）", spawnPush(path.join(repo, "sub"), ["--git-dir=../.git", "push", "origin", "HEAD:refs/heads/gitdir"]), 1);
  expect("push: 送り元の式に空白を含む（承認していない内容）", push(["origin", "HEAD@{0 seconds ago}:refs/heads/spaced"]), 1);
  expect("push: 大文字小文字を変えたブランチの書き方（承認していない内容）", push(["origin", "HEAD:refs/Heads/upper"]), 1);
  expect("pre-push: 形の合わない入力", run("node", [prePush, "origin", "x"], { input: "garbage\n", env: { CLAUDECODE: "1" } }), 1);
  fs.writeFileSync(path.join(repo, ".git", "review-gate", "approved.json"), "{ broken");
  expect("push: 記録が壊れている", push(["origin", "HEAD:work2"]), 1);
  fs.writeFileSync(path.join(repo, ".git", "review-gate", "approved.json"), "{}");
  expect("push: 記録に tree が無い", push(["origin", "HEAD:work2"]), 1);
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
