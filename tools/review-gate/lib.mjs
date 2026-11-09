// review-gate の各スクリプトが共有する処理。
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

/** git を実行して標準出力を返す。失敗したら例外。 */
export function git(args, options = {}) {
  return execFileSync("git", args, {
    encoding: "utf8",
    maxBuffer: 256 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
    ...options,
  }).trim();
}

/** リポジトリのルート。 */
export function repoRoot(cwd = process.cwd()) {
  return git(["rev-parse", "--show-toplevel"], { cwd });
}

/**
 * 承認の記録や生成物を置くディレクトリ（.git の中なので、コミットされない）。
 * create が false なら、パスを求めるだけでディレクトリは作らない。
 */
export function gateDir(cwd = process.cwd(), create = true) {
  const dir = path.resolve(cwd, git(["rev-parse", "--git-path", "review-gate"], { cwd }));
  if (create) {
    fs.mkdirSync(dir, { recursive: true });
  }
  return dir;
}

/** 承認の記録ファイルのパス。 */
export function approvalFile(cwd = process.cwd(), create = true) {
  return path.join(gateDir(cwd, create), "approved.json");
}

/** レビュアーを起動した時点の内容の記録ファイルのパス。 */
export function pendingFile(cwd = process.cwd(), create = true) {
  return path.join(gateDir(cwd, create), "review-started.json");
}

/**
 * 作業ツリーの今の内容（未追跡のファイルを含み、.gitignore の対象は除く）を、
 * コミットしたときと同じ規則でツリーにしたハッシュを返す。
 * 本物のインデックスは変えないよう、コピーした一時のインデックスを使う。
 */
export function workingTreeHash(cwd = process.cwd()) {
  // 呼び出し元の GIT_INDEX_FILE を引き継ぐと、本物のインデックスとは別のファイルを読むので外す。
  const baseEnv = Object.fromEntries(Object.entries(process.env).filter(([k]) => k !== "GIT_INDEX_FILE"));
  const real = path.resolve(cwd, git(["rev-parse", "--git-path", "index"], { cwd, env: baseEnv }));
  // 一時のインデックスは、この呼び出しだけの専用ディレクトリに置く（名前の衝突を避ける）。
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "review-gate-index-"));
  const tmp = path.join(dir, "index");
  try {
    if (fs.existsSync(real)) {
      fs.copyFileSync(real, tmp);
    }
    const env = { ...baseEnv, GIT_INDEX_FILE: tmp };
    git(["add", "-A"], { cwd, env });
    return git(["write-tree"], { cwd, env });
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

/** パスを比べられる形にする（Windows では大文字小文字と区切りの違いを無視する）。 */
export function normPath(p) {
  let r = path.resolve(p);
  try {
    // ジャンクションやシンボリックリンクを経由したパスも、実体のパスで比べる。
    r = fs.realpathSync.native(r);
  } catch {
    // 無いパスはそのまま比べる。
  }
  r = r.replace(/\\/g, "/");
  return process.platform === "win32" ? r.toLowerCase() : r;
}

/** dir が属するリポジトリの共通の .git の絶対パス（normPath 済み）。取れなければ null。 */
export function commonDir(dir) {
  try {
    return normPath(git(["rev-parse", "--path-format=absolute", "--git-common-dir"], { cwd: dir }));
  } catch {
    return null;
  }
}

/**
 * hooksPath が、cwd と同じリポジトリのチェックアウト（別の worktree を含む）のルート直下の .githooks で、
 * pre-push がある絶対パスなら null、そうでなければ理由を返す。
 */
export function hooksPathProblem(hooksPath, cwd = process.cwd()) {
  if (!hooksPath) return "core.hooksPath が設定されていない";
  if (!path.isAbsolute(hooksPath)) return `core.hooksPath（${hooksPath}）が絶対パスでない`;
  if (path.basename(hooksPath) !== ".githooks") return `core.hooksPath（${hooksPath}）が .githooks を指していない`;
  if (!fs.existsSync(path.join(hooksPath, "pre-push"))) return `core.hooksPath（${hooksPath}）に pre-push が無い`;
  const parent = path.dirname(hooksPath);
  let top = null;
  try {
    top = repoRoot(parent);
  } catch {
    // チェックアウトの中でなければ、下で理由を返す。
  }
  if (!top || normPath(top) !== normPath(parent)) return `core.hooksPath（${hooksPath}）がチェックアウトのルート直下の .githooks でない`;
  if (commonDir(parent) !== commonDir(cwd)) return `core.hooksPath（${hooksPath}）が別のリポジトリを指している`;
  return null;
}

/** 追跡されていない（.gitignore の対象でもない）ファイルの一覧。 */
export function untrackedFiles(cwd = process.cwd()) {
  return git(["-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard", "-z"], { cwd })
    .split("\0")
    .filter(Boolean);
}
