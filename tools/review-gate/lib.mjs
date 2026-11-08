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
  const tmp = path.join(os.tmpdir(), `review-gate-index-${process.pid}-${Date.now()}`);
  const real = path.resolve(cwd, git(["rev-parse", "--git-path", "index"], { cwd }));
  try {
    if (fs.existsSync(real)) {
      fs.copyFileSync(real, tmp);
    }
    const env = { ...process.env, GIT_INDEX_FILE: tmp };
    git(["add", "-A"], { cwd, env });
    return git(["write-tree"], { cwd, env });
  } finally {
    fs.rmSync(tmp, { force: true });
    fs.rmSync(`${tmp}.lock`, { force: true });
  }
}

/** 追跡されていない（.gitignore の対象でもない）ファイルの一覧。 */
export function untrackedFiles(cwd = process.cwd()) {
  return git(["-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard", "-z"], { cwd })
    .split("\0")
    .filter(Boolean);
}
