// review-gate の各スクリプトが共有する処理。
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

/** git を実行して標準出力を返す。失敗したら例外。 */
export function git(args, options = {}) {
  return execFileSync("git", args, { encoding: "utf8", maxBuffer: 256 * 1024 * 1024, ...options }).trim();
}

/** リポジトリのルート。 */
export function repoRoot(cwd = process.cwd()) {
  return git(["rev-parse", "--show-toplevel"], { cwd });
}

/** 承認の記録や生成物を置くディレクトリ（.git の中なので、コミットされない）。 */
export function gateDir(cwd = process.cwd()) {
  const dir = path.resolve(cwd, git(["rev-parse", "--git-path", "review-gate"], { cwd }));
  fs.mkdirSync(dir, { recursive: true });
  return dir;
}

/**
 * 作業ツリーの今の内容（未追跡のファイルを含み、.gitignore の対象は除く）を、
 * コミットしたときと同じ規則でツリーにしたハッシュを返す。
 * 本物のインデックスは変えないよう、一時のインデックスを使う。
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
  }
}

/** コミット済みの HEAD のツリーのハッシュ。 */
export function headTreeHash(cwd = process.cwd()) {
  return git(["rev-parse", "HEAD^{tree}"], { cwd });
}

/** 承認の記録ファイルのパス。 */
export function approvalFile(cwd = process.cwd()) {
  return path.join(gateDir(cwd), "approved.json");
}
