// 独立レビューの前に、機械で確かめられることを確かめて報告にまとめる。
// 対象は、比べる相手（既定 origin/main）とこのブランチの分岐点から、今の作業ツリーまでの差分
// （未コミット・未追跡を含む）で、追加・変更した行だけ。main 側で後から入った変更は含めない。
//
//   (a) 断定語（必ず・すべて・常に など）を含む行。Markdown の行と、C#・JavaScript の行頭がコメントの行が対象
//   (b) 記事の、比率・比較を含む行と、その記事が読み込む表（数値の照合はレビュアーが行う。ここは材料をそろえるまで）
//   (c) 記事のコード片: C# は 1 つずつ別のプロジェクトでコンパイルできるか（包み方を順に試す）、XAML は XML として読めるか
//   (d) 変更した C# のメソッドと JavaScript の関数のうち、説明コメントがあるものの割合。
//       CodeRabbit の Docstring Coverage と数え方が違うので、ずれることがある参考値（報告の (d) に違いを書く）
//
// 使い方（リポジトリのルートで実行する）:
//   node tools/review-gate/article-check.mjs [比べる相手（既定 origin/main）]
// 出力: `git rev-parse --git-path review-gate` の場所（通常は .git/review-gate/）の article-check.md（コミットされない）。パスと要約を標準出力に書く。
// 前提: 比べる相手が <remote>/<branch> の形なら、それを fetch できること。(c) には .NET 10 SDK が要る（無い・ビルドできないときは「確かめていない」と書く）。
//       Windows 以外でも WPF を参照してコンパイルできるよう EnableWindowsTargeting を付けているが、動作を確かめたのは Windows だけ。
// 終了コード: 報告を作れたら 0（見つかった問題の有無にかかわらない）。差分を取れなければ 1。
import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { gateDir, git, repoRoot, untrackedFiles } from "./lib.mjs";

const base = process.argv[2] ?? "origin/main";
const root = repoRoot();
process.chdir(root);

let mergeBase;
try {
  // 比べる相手が <remote>/<branch> の形なら、その remote と branch を取得してから分岐点を求める。
  const [remote, ...branch] = base.split("/");
  if (branch.length && git(["remote"]).split(/\r?\n/).includes(remote)) {
    git(["fetch", remote, branch.join("/")]);
  }
  mergeBase = git(["merge-base", base, "HEAD"]);
} catch (e) {
  console.error(`比べる相手（${base}）との分岐点を取れなかった（fetch の失敗を含む）。止める。\n${e.message}`);
  process.exit(1);
}

// ---- 差分の取得（ファイルごとの追加・変更行） ----
// ユーザーの git の設定（接頭辞・パスの引用・外部 diff）に左右されないよう、明示して取る。
const changed = new Map(); // path -> Set(行番号, 1 始まり)
const diff = git([
  "-c", "core.quotePath=false", "diff", "--ignore-cr-at-eol", "-U0", "--no-color", "--no-ext-diff", "--no-textconv",
  "--no-renames", "--src-prefix=a/", "--dst-prefix=b/", mergeBase, "--", ".",
]);
let current = null;
let inHeader = false;
for (const line of diff.split("\n")) {
  if (line.startsWith("diff --git ")) {
    inHeader = true;
    current = null;
    continue;
  }
  if (inHeader) {
    // 名前に空白を含むファイルは、git が末尾に TAB を付けるので取り除く。
    const file = line.match(/^\+\+\+ b\/(.+?)\t?$/);
    if (file) {
      current = file[1];
      changed.set(current, changed.get(current) ?? new Set());
    }
    if (line.startsWith("@@")) {
      inHeader = false;
    } else {
      continue;
    }
  }
  const hunk = line.match(/^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@/);
  if (hunk && current) {
    const start = Number(hunk[1]);
    const count = hunk[2] === undefined ? 1 : Number(hunk[2]);
    for (let i = 0; i < count; i++) {
      changed.get(current).add(start + i);
    }
  }
}
for (const file of untrackedFiles()) {
  try {
    const n = fs.readFileSync(file, "utf8").split(/\r?\n/).length;
    changed.set(file, new Set(Array.from({ length: n }, (_, i) => i + 1)));
  } catch {
    // 読めないファイル（バイナリの大きなファイルなど）は対象外。
  }
}
for (const [file] of changed) {
  if (!fs.existsSync(file)) {
    changed.delete(file);
  }
}

/** ファイルを行の配列として読む。 */
const readLines = (file) => fs.readFileSync(file, "utf8").split(/\r?\n/);
/** 記事（日英）の Markdown か。 */
const isArticle = (file) => /^_articles_(ja|en)\/.+\.md$/.test(file);
/** C#・JavaScript の、行頭がコメントの行か。 */
const isCommentLine = (file, t) =>
  (/\.(?:cs)$/.test(file) && /^\s*\/\//.test(t)) ||
  (/\.(?:mjs|cjs|js|ts)$/.test(file) && /^\s*(?:\/\/|\/\*|\*)/.test(t));
const out = [];
const summary = [];

// ---- (a) 断定語 ----
const absolute = /必ず|すべて|全て|常に|たびに|絶対(?!パス|値|参照|座標)|一切|決して|どんな場合も|例外なく|以降は|\balways\b|\bnever\b|\bevery\b|\ball of\b|\bguarantee/i;
const aHits = [];
for (const [file, lines] of changed) {
  const prose = file.endsWith(".md");
  if (!prose && !/\.(?:cs|mjs|cjs|js|ts)$/.test(file)) continue;
  const text = readLines(file);
  for (const n of [...lines].sort((x, y) => x - y)) {
    const t = text[n - 1] ?? "";
    if ((prose || isCommentLine(file, t)) && absolute.test(t) && !t.trim().startsWith("|---")) {
      aHits.push(`- \`${file}:${n}\` ${t.trim().slice(0, 200)}`);
    }
  }
}
out.push("## (a) 断定語を含む追加行（Markdown と、C#・JavaScript のコメント）", "", "測った条件・件数・環境の範囲に収まる言い方かを確かめる。", "");
out.push(...(aHits.length ? aHits : ["（なし）"]), "");
summary.push(`(a) 断定語 ${aHits.length} 行`);

// ---- (b) 比率・比較の文と表 ----
const ratio = /倍|分の\s*\d|半分|以上|以下|未満|超え|最も|最長|最短|最大|最小|中央値|\d+\s*%|\btimes\b|\bhalf\b|\bthird\b|more than|less than|\bunder\b|\bover\b|\bmedian\b|\blongest\b|\bshortest\b|\bfastest\b|\bslowest\b/i;
const bHits = [];
const tablesToShow = new Set();
for (const [file, lines] of changed) {
  if (!isArticle(file)) continue;
  const text = readLines(file);
  let fileHasHit = false;
  for (const n of [...lines].sort((x, y) => x - y)) {
    const t = text[n - 1] ?? "";
    if (ratio.test(t) && !t.trim().startsWith("{%") && !t.trim().startsWith("|---")) {
      bHits.push(`- \`${file}:${n}\` ${t.trim().slice(0, 300)}`);
      fileHasHit = true;
    }
  }
  if (fileHasHit) {
    for (const t of text) {
      const inc = t.match(/\{%\s*include\s+(tables\/\S+\.md)\s*%\}/);
      if (inc) tablesToShow.add(path.join("_includes", inc[1]).replace(/\\/g, "/"));
    }
  }
}
out.push("## (b) 記事の、比率・比較を含む追加行と、その記事が読み込む表", "");
out.push("各文の比率・大小・「どの回でも」などが、表の値（中央値と各回の範囲）で成り立つかを計算して確かめる。中央値だけで成り立つ比較には「中央値で」と書いてあるかも見る。", "");
out.push(...(bHits.length ? bHits : ["（なし）"]), "");
for (const t of [...tablesToShow].sort()) {
  if (!fs.existsSync(t)) continue;
  out.push(`### ${t}`, "", ...readLines(t).filter((l) => l.trim()), "");
}
summary.push(`(b) 比率・比較 ${bHits.length} 行、表 ${tablesToShow.size} 個`);

// ---- (c) コード片 ----
const snippets = [];
for (const [file, lines] of changed) {
  if (!isArticle(file)) continue;
  const text = readLines(file);
  // すべてのフェンス（言語名を問わない）の開閉を追い、C#・XAML のものだけを取り出す。ほかのフェンスの中身は読み飛ばす。
  for (let i = 0; i < text.length; i++) {
    const open = text[i].match(/^\s*(`{3,}|~{3,})\s*([^\s`]*).*$/);
    if (!open) continue;
    const fence = open[1];
    let j = i + 1;
    while (j < text.length && !new RegExp(`^\\s*${fence[0] === "`" ? "`" : "~"}{${fence.length},}\\s*$`).test(text[j])) j++;
    const lang = /^(?:csharp|cs|c#)$/i.test(open[2]) ? "csharp" : /^(?:xml|xaml)$/i.test(open[2]) ? "xaml" : null;
    const touched = [...Array(j - i + 1).keys()].some((k) => lines.has(i + 1 + k));
    if (lang && touched) {
      snippets.push({ file, line: i + 2, lang, code: text.slice(i + 1, j).join("\n") });
    }
    i = j;
  }
}
out.push("## (c) 記事のコード片（追加・変更した行を含むもの）", "");
const cResults = [];

// 抜粋の外で宣言された名前や型を参照しているだけのことが多い番号。
const outsideRef = new Set(["CS0103", "CS0246", "CS0234"]);
// 存在しないメンバーを呼んでいる番号（実在しない API の可能性が高いが、同じ記事のほかのコード片で宣言した拡張メソッドのこともある）。
const missingMember = new Set(["CS1061", "CS0117"]);

const xamlNamespaces = {
  "": "http://schemas.microsoft.com/winfx/2006/xaml/presentation",
  x: "http://schemas.microsoft.com/winfx/2006/xaml",
  d: "http://schemas.microsoft.com/expression/blend/2008",
  mc: "http://schemas.openxmlformats.org/markup-compatibility/2006",
  i: "http://schemas.microsoft.com/xaml/behaviors",
  b: "http://schemas.microsoft.com/xaml/behaviors",
  prism: "http://prismlibrary.com/",
  sys: "clr-namespace:System;assembly=System.Runtime",
  local: "clr-namespace:App",
};

const xamlCheckerSource = `using System.Xml;

// 一時ディレクトリのパスに ASCII 以外の文字があっても、node 側で utf8 として読めるようにする。
Console.OutputEncoding = System.Text.Encoding.UTF8;
foreach (string file in args)
{
    try
    {
        using var reader = XmlReader.Create(file);
        while (reader.Read()) { }
        Console.WriteLine($"OK\\t{file}");
    }
    catch (XmlException ex)
    {
        Console.WriteLine($"NG\\t{file}\\t{ex.LineNumber}:{ex.LinePosition}\\t{ex.Message}");
    }
}
`;

/** XAML を XML として読めるか確かめるプログラム。生成内容のハッシュごとに置き場に置いて使い回し、古いものは消す。 */
function xamlChecker() {
  const hash = createHash("sha256").update(xamlCheckerSource).digest("hex").slice(0, 12);
  const name = `xaml-checker-${hash}`;
  for (const old of fs.readdirSync(gateDir())) {
    if (old.startsWith("xaml-checker") && old !== name) {
      try {
        fs.rmSync(path.join(gateDir(), old), { recursive: true, force: true });
      } catch {
        // 使用中などで消せなければ残す。
      }
    }
  }
  const dir = path.join(gateDir(), name);
  // OS によって実行ファイルの名前が違うので、`dotnet <dll>` で起動する。
  const dll = path.join(dir, "bin", "Release", "net10.0", "XamlChecker.dll");
  if (fs.existsSync(dll)) return dll;
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(
    path.join(dir, "XamlChecker.csproj"),
    `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
`,
  );
  fs.writeFileSync(path.join(dir, "Program.cs"), xamlCheckerSource);
  const build = spawnSync("dotnet", ["build", "-nologo", "-v", "q", "-c", "Release", "-nodeReuse:false"], { cwd: dir, encoding: "utf8", timeout: 300000 });
  return !build.error && fs.existsSync(dll) ? dll : null;
}

const usingRe = /^\s*(?:global\s+)?using\s+(?:static\s+)?[\w.]+(?:\s*=\s*[\w.<>, ]+)?\s*;\s*(?:\/\/.*)?$/;
// 戻り値の型の無い `修飾子 名前(` の宣言（コンストラクター）。名前を取り出す。
const ctorDeclRe = /^\s*(?:public|private|protected|internal|static)\s+(?:(?:public|private|protected|internal|static)\s+)*([A-Z]\w*)\s*\(/m;
// 拡張メソッド（最初の引数に this が付いた静的メソッド）。
const extensionRe = /\bstatic\b[^\n(]*\(\s*this\s/;
const typeDeclRe = /^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|sealed|static|partial|abstract|readonly|file)\s+)*(?:class|record|struct|interface|enum|namespace)\s+\w/m;

/**
 * C# のコード片を、指定した包み方で 1 つのソースにする。using の行は、元の行番号を付けて先頭に移す。
 * kind: type（そのまま）/ toplevel（実行形式のトップレベル）/ members（クラスで包む）/ ctor（コンストラクターの名前のクラスで包む）/
 *       extensions（静的クラスで包む）/ statements（メソッドの本体で包む）/ iterator（反復子の本体で包む）/
 *       asyncIterator（非同期の反復子の本体で包む）
 */
function csharpSource(code, kind) {
  const lines = code.split("\n");
  const head = [];
  const body = lines.map((l, i) => {
    if (usingRe.test(l)) {
      head.push(`#line ${i + 1} "snippet"`, l.trim());
      return "";
    }
    return l;
  });
  head.push("#line default");
  const b = body.join("\n");
  if (kind === "type" || kind === "toplevel") {
    return `${head.join("\n")}\n#line 1 "snippet"\n${b}\n`;
  }
  if (kind === "members") {
    return `${head.join("\n")}\npublic partial class SnippetHolder\n{\n#line 1 "snippet"\n${b}\n#line default\n}\n`;
  }
  if (kind === "ctor") {
    // 戻り値の型の無い `修飾子 名前(` の宣言（コンストラクター）の名前で、クラスを包む。
    const name = (b.match(ctorDeclRe) || [])[1] ?? "SnippetHolder";
    return `${head.join("\n")}\npublic partial class ${name}\n{\n#line 1 "snippet"\n${b}\n#line default\n}\n`;
  }
  if (kind === "extensions") {
    return `${head.join("\n")}\npublic static partial class SnippetExtensions\n{\n#line 1 "snippet"\n${b}\n#line default\n}\n`;
  }
  if (kind === "iterator") {
    return `${head.join("\n")}\npublic partial class SnippetHolder\n{\n    public System.Collections.IEnumerable SnippetBody()\n    {\n#line 1 "snippet"\n${b}\n#line default\n    }\n}\n`;
  }
  if (kind === "asyncIterator") {
    return `${head.join("\n")}\npublic partial class SnippetHolder\n{\n    public async System.Collections.Generic.IAsyncEnumerable<object?> SnippetBody()\n    {\n#line 1 "snippet"\n${b}\n#line default\n        await System.Threading.Tasks.Task.CompletedTask;\n    }\n}\n`;
  }
  return `${head.join("\n")}\npublic partial class SnippetHolder\n{\n    public async System.Threading.Tasks.Task<object?> SnippetBody()\n    {\n#line 1 "snippet"\n${b}\n#line default\n        await System.Threading.Tasks.Task.CompletedTask;\n        return null;\n    }\n}\n`;
}

/** 1 つのコード片を、1 つの包み方で、専用のプロジェクトとしてコンパイルし、エラーを返す。 */
function compileSnippet(baseDir, idx, code, kind) {
  const dir = path.join(baseDir, `s${idx}-${kind}`);
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(
    path.join(dir, "Snippet.csproj"),
    `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>${kind === "toplevel" ? "Exe" : "Library"}</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <!-- Windows 以外（Web 実行環境の Linux など）でも、WPF を参照してコンパイルだけはできるようにする -->
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);CS8618;CS0169;CS0414;CS1998;CS8602;CS8604;CS0168;CS0219;CS0162;CS8321</NoWarn>
  </PropertyGroup>
</Project>
`,
  );
  const globalUsings = [
    "System.Collections.ObjectModel", "System.Collections.Specialized", "System.ComponentModel",
    "System.Windows", "System.Windows.Controls", "System.Windows.Data", "System.Windows.Input",
    "System.Windows.Media", "System.Windows.Threading",
  ];
  fs.writeFileSync(path.join(dir, "GlobalUsings.cs"), globalUsings.map((u) => `global using ${u};`).join("\n") + "\n");
  fs.writeFileSync(path.join(dir, "Snippet.cs"), csharpSource(code, kind));
  const build = spawnSync("dotnet", ["build", "-nologo", "-v", "q", "-clp:NoSummary", "-nodeReuse:false"], {
    cwd: dir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024, timeout: 300000,
  });
  if (build.error) {
    return { ok: false, failed: `.NET SDK を起動できなかったか時間切れになった: ${build.error.message}` };
  }
  const errors = [];
  const seen = new Set();
  for (const l of `${build.stdout}\n${build.stderr}`.split(/\r?\n/)) {
    // #line で付けた名前（snippet）のエラーはコード片の行。Snippet.cs のエラーは包むために足した部分。
    const m = l.match(/[\\/](snippet|Snippet\.cs)\((\d+),(\d+)\): error (CS\d+): (.+?)(?: \[.*\])?$/);
    if (!m) continue;
    const inSnippet = m[1] === "snippet";
    const key = `${inSnippet}:${m[2]}:${m[4]}:${m[5]}`;
    if (seen.has(key)) continue;
    seen.add(key);
    errors.push({ inSnippet, line: inSnippet ? Number(m[2]) : null, code: m[4], msg: m[5] });
  }
  if (build.status !== 0 && errors.length === 0) {
    return { ok: false, failed: `ビルドが失敗したが、エラーを読み取れなかった: ${`${build.stdout}`.split(/\r?\n/).slice(0, 3).join(" / ")}` };
  }
  return { ok: true, errors };
}

/** XAML のコード片を、宣言の無い接頭辞を補ったうえで、読める形の XML にする。 */
function wrapXaml(code) {
  const trimmed = code.trim();
  if (/^<\?xml|^<[\w:.]+[^>]*\sxmlns=/.test(trimmed)) {
    return { xml: code, lineOffset: 0 };
  }
  const declared = new Set([...code.matchAll(/xmlns:(\w+)=/g)].map((m) => m[1]));
  const used = new Set([...code.matchAll(/<\/?(\w+):[\w.]+|\s(\w+):[\w.]+\s*=/g)].map((m) => m[1] ?? m[2]).filter((p) => p && p !== "xmlns" && p !== "xml"));
  const attrs = [`xmlns="${xamlNamespaces[""]}"`];
  for (const p of new Set([...Object.keys(xamlNamespaces).filter(Boolean), ...used])) {
    if (declared.has(p)) continue;
    attrs.push(`xmlns:${p}="${xamlNamespaces[p] ?? `urn:snippet:${p}`}"`);
  }
  return { xml: `<SnippetRoot ${attrs.join(" ")}>\n${code}\n</SnippetRoot>`, lineOffset: 1 };
}

const kindLabel = {
  type: "そのまま",
  toplevel: "トップレベルの文として",
  members: "クラスのメンバーとして包んで",
  statements: "メソッドの本体として包んで",
  iterator: "反復子のメソッドの本体として包んで",
  asyncIterator: "非同期の反復子のメソッドの本体として包んで",
  ctor: "コンストラクターの名前のクラスで包んで",
  extensions: "静的クラスで包んで",
};

if (snippets.length === 0) {
  out.push("（対象なし）", "");
} else {
  const baseDir = fs.mkdtempSync(path.join(os.tmpdir(), "article-snippets-"));
  const xamlDir = fs.mkdtempSync(path.join(os.tmpdir(), "article-xaml-"));
  try {
    const xamlFiles = [];
    snippets.forEach((s, idx) => {
      if (s.lang !== "xaml") return;
      const w = wrapXaml(s.code);
      s.lineOffset = w.lineOffset;
      const f = path.join(xamlDir, `snippet${idx}.xml`);
      fs.writeFileSync(f, w.xml);
      xamlFiles.push([f, idx]);
    });
    const xamlResults = new Map();
    if (xamlFiles.length) {
      const dll = xamlChecker();
      if (!dll) {
        out.push("XAML のチェック用プログラムをビルドできなかったので、XAML は確かめていない。", "");
      } else {
        const run = spawnSync("dotnet", [dll, ...xamlFiles.map(([f]) => f)], { encoding: "utf8", timeout: 60000 });
        for (const l of (run.stdout ?? "").split(/\r?\n/)) {
          const [st, f, pos, msg] = l.split("\t");
          const hit = xamlFiles.find(([x]) => x === f);
          if (hit) xamlResults.set(hit[1], st === "OK" ? null : { pos, msg });
        }
      }
    }

    snippets.forEach((s, idx) => {
      const where = `\`${s.file}:${s.line}\`（${s.lang}）`;
      if (s.lang === "xaml") {
        const r = xamlResults.get(idx);
        if (r === undefined) {
          cResults.push("SKIP");
          out.push(`- ${where}: 確かめていない`);
        } else if (r === null) {
          cResults.push("OK");
          out.push(`- ${where}: XML として読めた`);
        } else {
          cResults.push("NG");
          const [ln, col] = r.pos.split(":").map(Number);
          const inLine = ln - s.lineOffset;
          const at = inLine >= 1 && inLine <= s.code.split("\n").length ? `コード片の ${inLine} 行目 ${col} 文字目` : "包むために足した部分";
          out.push(`- ${where}: **XML として読めない**（${at}。メッセージ中の行番号は包んだ後の行番号）: ${r.msg}`);
        }
        return;
      }

      // 包み方を順に試し、エラーの無いものを採る。無ければ、(構文・型の誤りの件数, 存在しないメンバーの件数,
      // 抜粋の外の参照の件数) の順に少ないものを採る（件数の合計だけで選ぶと、外の参照だけのものより誤りのあるものを選んでしまう）。
      const body = s.code.split("\n").filter((l) => !usingRe.test(l)).join("\n");
      const hasYield = /\byield\s+(?:return|break)\b/.test(body);
      const hasAwait = /\bawait\b/.test(body);
      let kinds;
      if (typeDeclRe.test(body)) kinds = ["type", "toplevel", "members"];
      else if (extensionRe.test(body)) kinds = ["extensions", "members"];
      else if (hasYield) kinds = [hasAwait ? "asyncIterator" : "iterator", "members", "toplevel"];
      else kinds = ["statements", "members", "ctor", "toplevel"];
      /** エラーの重さを、比べられる数の組にする。 */
      const weight = (errors) => [
        errors.filter((e) => !outsideRef.has(e.code) && !missingMember.has(e.code)).length,
        errors.filter((e) => missingMember.has(e.code)).length,
        errors.filter((e) => outsideRef.has(e.code)).length,
      ];
      /** 重さの組を辞書順に比べ、a のほうが軽ければ true。 */
      const lighter = (a, b) => {
        for (let k = 0; k < a.length; k++) if (a[k] !== b[k]) return a[k] < b[k];
        return false;
      };
      /** 構文・型の誤り（外の参照と存在しないメンバーを除くエラー）なら true。 */
      const isHard = (e) => !outsideRef.has(e.code) && !missingMember.has(e.code);
      let best = null;
      // 構文・型の誤りがコード片の中に出た包み方のうち、最も軽いもの（包み方によっては、同じ誤りが包むために足した部分に出るため）。
      let inSnippetHard = null;
      let failed = null;
      for (const kind of kinds) {
        const r = compileSnippet(baseDir, idx, s.code, kind);
        if (!r.ok) {
          failed = r.failed;
          break;
        }
        if (!best || lighter(weight(r.errors), weight(best.errors))) best = { kind, errors: r.errors };
        if (r.errors.some((e) => isHard(e) && e.inSnippet) && (!inSnippetHard || lighter(weight(r.errors), weight(inSnippetHard.errors)))) {
          inSnippetHard = { kind, errors: r.errors };
        }
        if (r.errors.length === 0) break;
      }
      // 途中で SDK の起動や時間切れで止まった場合は、すべての包み方を試していない。
      const tried = failed ? "試した包み方では" : "どの包み方でも";
      if (failed && !best) {
        cResults.push("SKIP");
        out.push(`- ${where}: 確かめていない（${failed}）`);
        return;
      }
      let errs = best.errors;
      if (errs.length === 0) {
        cResults.push("OK");
        out.push(`- ${where}: ${kindLabel[best.kind]}コンパイルできた`);
        return;
      }
      let hard = errs.filter(isHard);
      if (hard.length && hard.every((e) => !e.inSnippet) && inSnippetHard) {
        // 最も軽い包み方では誤りが包むために足した部分にだけ出たが、ほかの包み方ではコード片の中に出た。後者を報告する。
        best = inSnippetHard;
        errs = best.errors;
        hard = errs.filter(isHard);
      }
      const api = errs.filter((e) => missingMember.has(e.code));
      if (hard.length && hard.every((e) => !e.inSnippet) && !api.length) {
        cResults.push("SKIP");
        out.push(`- ${where}: 包み方で確かめられない（${tried}、誤りが包むために足した部分にだけ出た。抜粋の形をレビュアーが確かめる）`);
      } else if (hard.length && hard.some((e) => e.inSnippet)) {
        cResults.push("NG");
        out.push(`- ${where}: **構文・型の誤りの可能性**（${tried}エラーが残った。報告するのは${kindLabel[best.kind]}コンパイルしたとき）`);
      } else if (api.length) {
        cResults.push("NG");
        out.push(`- ${where}: **存在しないメンバーを呼んでいる（実在しない API の可能性。同じ記事のほかのコード片で宣言した拡張メソッドなら要確認）**`);
      } else {
        cResults.push("REF");
        out.push(`- ${where}: 抜粋の外で宣言された名前を参照しているだけの可能性（要確認）`);
      }
      for (const e of errs.slice(0, 8)) {
        out.push(`  - ${e.inSnippet ? `コード片の ${e.line} 行目` : "包むために足した部分"} ${e.code}: ${e.msg}`);
      }
    });
    out.push("");
    out.push(
      "C# のコード片は 1 つずつ別のプロジェクトでコンパイルした（.NET 10・WPF、Toolkit や Prism などのパッケージは参照しない）。" +
        "包み方は、型の宣言を含むものは「そのまま → トップレベルの文 → クラスのメンバー」、拡張メソッドは「静的クラス → クラスのメンバー」、" +
        "yield を含むものは「反復子（await も含めば非同期の反復子）のメソッド → クラスのメンバー → トップレベルの文」、" +
        "それ以外は「メソッドの本体 → クラスのメンバー → コンストラクター → トップレベルの文」の順に試し、" +
        "エラーの無いものを採った。無ければ、構文・型の誤り → 存在しないメンバー → 抜粋の外の参照の順に件数を比べて、最も軽いものを採った" +
        "（ただし、採った包み方では構文・型の誤りが包むために足した部分にだけ出て、ほかの包み方ではコード片の中に出た場合は、後者を報告する。" +
        "そのため、かっこの数が合わない抜粋は、多くても少なくても「構文・型の誤りの可能性」になる）。" +
        "宣言の無い名前の参照（CS0103・CS0246・CS0234）は「抜粋の外を参照しているだけの可能性」に分けた。" +
        "宣言や構文の誤りがあると、コンパイラがメソッドの本体の検査まで進まず、ほかの誤りが報告されないことがある。" +
        "XAML は、名前空間の宣言が無いものを WPF の既定の名前空間で包み、宣言の無い接頭辞は仮の名前空間で補って読んだ。",
      "",
    );
  } finally {
    for (const d of [baseDir, xamlDir]) {
      try {
        fs.rmSync(d, { recursive: true, force: true });
      } catch {
        // ビルドのプロセスがファイルを掴んでいて消せないことがある。一時ディレクトリなので残しても害は無い。
      }
    }
  }
}
summary.push(`(c) コード片 ${snippets.length} 個（誤りの可能性 ${cResults.filter((r) => r === "NG").length}、要確認 ${cResults.filter((r) => r === "REF").length}、確かめていない ${cResults.filter((r) => r === "SKIP").length}）`);

// ---- (d) 説明コメントの割合 ----
const methodDecl = /^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|new|partial|unsafe|extern|readonly)\s+)+(?:[\w<>[\],.?()]+\s+)?(\w+)\s*(?:<[^>()]*>)?\s*\(/;
const notMethod = /^\s*(?:return|new|if|while|for|foreach|switch|catch|using|lock)\b/;
let total = 0;
let documented = 0;
const missing = [];

/** 宣言の直前にある説明コメントの開始行（無ければ宣言の行）を返す。属性の行は読み飛ばす。 */
function docStart(text, d, isDocLine) {
  let p = d - 1;
  while (p >= 0 && /^\s*\[/.test(text[p])) p--;
  let start = d;
  while (p >= 0 && isDocLine(text[p], p)) {
    start = p;
    p--;
  }
  return start;
}

for (const [file, lines] of changed) {
  if (!file.endsWith(".cs")) continue;
  const text = readLines(file);
  // 波かっこの深さを追い、型の本体の直下にある宣言だけをメンバーとして数える（ローカル関数は数えない）。
  const decls = [];
  const typeDepths = [];
  let depth = 0;
  let pendingType = false;
  text.forEach((t, i) => {
    const code = t.replace(/'(?:[^'\\]|\\.)'/g, "' '").replace(/"(?:[^"\\]|\\.)*"/g, '""').replace(/\/\/.*$/, "");
    while (typeDepths.length && depth < typeDepths[typeDepths.length - 1]) typeDepths.pop();
    const isMember = typeDepths.length > 0 && depth === typeDepths[typeDepths.length - 1];
    if (isMember && methodDecl.test(t) && !notMethod.test(t) && !/=\s*>?\s*$/.test(t.split("(")[0])) decls.push(i);
    if (/\b(?:class|record|struct|interface)\s+\w/.test(code)) pendingType = true;
    for (const ch of code) {
      if (ch === "{") {
        depth++;
        if (pendingType) {
          typeDepths.push(depth);
          pendingType = false;
        }
      } else if (ch === "}") {
        depth--;
      }
    }
    if (pendingType && /;\s*$/.test(code)) pendingType = false;
  });
  decls.forEach((d, k) => {
    const start = docStart(text, d, (l) => /^\s*\/\/\//.test(l));
    const nextStart = k + 1 < decls.length ? docStart(text, decls[k + 1], (l) => /^\s*\/\/\//.test(l)) : text.length;
    let touched = false;
    for (let n = start + 1; n <= nextStart; n++) if (lines.has(n)) touched = true;
    if (!touched) return;
    total++;
    if (start < d) documented++;
    else missing.push(`- \`${file}:${d + 1}\` ${text[d].trim().slice(0, 150)}`);
  });
}

// JavaScript の関数は、function 宣言と、名前を付けて変数に入れたアロー関数・関数式を、入れ子も含めて数える。
// 直前に JSDoc（/** で始まるコメント）があるかで判定する。クラスのメソッドと無名の関数（即時実行など）は数えない。
const jsFunctionDecl =
  /^\s*(?:export\s+)?(?:async\s+)?function\s*\*?\s*\w+\s*\(|^\s*(?:export\s+)?(?:const|let|var)\s+\w+\s*=\s*(?:async\s+)?(?:function\b|\((?!\()[^)]*\)\s*=>|\w+\s*=>)/;
for (const [file, lines] of changed) {
  if (!/\.(?:mjs|cjs|js|ts)$/.test(file)) continue;
  const text = readLines(file);
  const decls = [];
  text.forEach((t, i) => {
    if (jsFunctionDecl.test(t)) decls.push(i);
  });
  /** 宣言の直前の JSDoc の開始行（無ければ宣言の行）を返す。 */
  const jsdocStart = (d) => {
    if (d === 0 || !/\*\/\s*$/.test(text[d - 1])) return d;
    let p = d - 1;
    while (p >= 0 && !/\/\*/.test(text[p])) p--;
    return p >= 0 && /\/\*\*/.test(text[p]) ? p : d;
  };
  decls.forEach((d, k) => {
    const start = jsdocStart(d);
    const nextStart = k + 1 < decls.length ? jsdocStart(decls[k + 1]) : text.length;
    let touched = false;
    for (let n = start + 1; n <= nextStart; n++) if (lines.has(n)) touched = true;
    if (!touched) return;
    total++;
    if (start < d) documented++;
    else missing.push(`- \`${file}:${d + 1}\` ${text[d].trim().slice(0, 150)}`);
  });
}
const pct = total ? Math.round((documented / total) * 1000) / 10 : 100;
out.push("## (d) 変更した C# のメソッドと JavaScript の関数の説明コメント", "");
out.push(
  `変更を含むメソッド・関数 ${total} 個のうち、説明コメント（C# は ///、JavaScript は /** */）があるもの ${documented} 個（${pct}%）。` +
    "CodeRabbit の Docstring Coverage は、変更した関数のうち循環的複雑度 2 以上だけを数えて 80% を基準にする（PR #174 の pre-merge check の説明による）。" +
    "ここは数え方が違う（複雑度で絞らない。C# は型の直下の修飾子の付いたメソッドだけで、ローカル関数は数えない。" +
    "JavaScript は function 宣言と名前付きのアロー関数・関数式で、クラスのメソッドと無名の関数は数えない）ので、CodeRabbit の値とずれることがある参考値である。",
  "",
);
out.push(...(missing.length ? ["説明コメントが無いもの:", ...missing] : ["（すべてにある）"]), "");
summary.push(`(d) 説明コメント ${pct}%（${documented}/${total}）`);

const report = path.join(gateDir(), "article-check.md");
fs.writeFileSync(
  report,
  ["# 機械で確かめた結果", "", `比べる相手: ${base} とこのブランチの分岐点（${mergeBase.slice(0, 7)}）から、今の作業ツリーまで`, "", ...out].join("\n"),
);
console.log(report);
console.log(summary.join(" / "));
