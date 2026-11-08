// 独立レビューの前に、機械で確かめられることを確かめて報告にまとめる。
// 対象は、比べる相手（既定 origin/main）とこのブランチの分岐点から、今の作業ツリーまでの差分
// （未コミット・未追跡を含む）で、追加・変更した行だけ。main 側で後から入った変更は含めない。
//
//   (a) 断定語（必ず・すべて・常に など）を含む行。Markdown の行と、C#・JavaScript のコメント行が対象
//   (b) 記事の、比率・比較を含む行と、その記事が読み込む表（数値の照合はレビュアーが行う。ここは材料をそろえるまで）
//   (c) 記事のコード片: C# はコンパイルできるか（抜粋は型・メンバー・文に応じて包む）、XAML は XML として読めるか
//   (d) 変更した C# のメソッドと JavaScript の関数のうち、説明コメントがあるものの割合。
//       CodeRabbit の Docstring Coverage は、変更した関数のうち循環的複雑度 2 以上だけを数え、80% を基準にする。
//       ここでは複雑度で絞らずに数えるので、近い値の目安にとどまる
//
// 使い方（リポジトリのルートで実行する）:
//   node tools/review-gate/article-check.mjs [比べる相手（既定 origin/main）]
// 出力: .git/review-gate/article-check.md（コミットされない）。パスと要約を標準出力に書く。
// 前提: git fetch origin main ができること。(c) には .NET 10 SDK が要る（無い・ビルドできないときは「確かめていない」と書く）。
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
  git(["fetch", "origin", "main"]);
  mergeBase = git(["merge-base", base, "HEAD"]);
} catch (e) {
  console.error(`比べる相手（${base}）との分岐点を取れなかった（git fetch origin main の失敗を含む）。止める。\n${e.message}`);
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
    const file = line.match(/^\+\+\+ b\/(.+)$/);
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

const readLines = (file) => fs.readFileSync(file, "utf8").split(/\r?\n/);
const isArticle = (file) => /^_articles_(ja|en)\/.+\.md$/.test(file);
const isCommentLine = (file, t) =>
  (/\.(?:cs)$/.test(file) && /^\s*\/\//.test(t)) ||
  (/\.(?:mjs|cjs|js|ts)$/.test(file) && /^\s*(?:\/\/|\/\*|\*)/.test(t));
const out = [];
const summary = [];

// ---- (a) 断定語 ----
const absolute = /必ず|すべて|全て|常に|たびに|絶対|一切|決して|どんな場合も|例外なく|以降は|\balways\b|\bnever\b|\bevery\b|\ball of\b|\bguarantee/i;
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
  for (let i = 0; i < text.length; i++) {
    const open = text[i].match(/^\s*(`{3,}|~{3,})\s*(csharp|cs|c#|xml|xaml)\b.*$/i);
    if (!open) continue;
    const fence = open[1];
    let j = i + 1;
    while (j < text.length && !new RegExp(`^\\s*${fence[0] === "`" ? "`" : "~"}{${fence.length},}\\s*$`).test(text[j])) j++;
    const touched = [...Array(j - i + 1).keys()].some((k) => lines.has(i + 1 + k));
    if (touched) {
      const lang = /xml|xaml/i.test(open[2]) ? "xaml" : "csharp";
      snippets.push({ file, line: i + 2, lang, code: text.slice(i + 1, j).join("\n") });
    }
    i = j;
  }
}
out.push("## (c) 記事のコード片（追加・変更した行を含むもの）", "");
const cResults = [];

// パーサーが出す構文エラーの番号（これがあると、コンパイラは名前と型を確かめる段階に進まないことがある）。
const syntaxCodes = new Set([
  "CS1001", "CS1002", "CS1003", "CS1009", "CS1010", "CS1012", "CS1013", "CS1022", "CS1026", "CS1031", "CS1032",
  "CS1035", "CS1039", "CS1040", "CS1041", "CS1056", "CS1513", "CS1514", "CS1519", "CS1520", "CS1525", "CS1733",
  "CS8124", "CS8641",
]);
// 抜粋の外で宣言された名前や型を参照しているだけのことが多い番号。
const outsideRef = new Set(["CS0103", "CS0246", "CS0234"]);
// 存在しないメンバーを呼んでいる（実在しない API の可能性が高い）番号。
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

/** XAML を XML として読めるか確かめるプログラム。生成内容のハッシュごとに .git/review-gate に置いて使い回す。 */
function xamlChecker() {
  const hash = createHash("sha256").update(xamlCheckerSource).digest("hex").slice(0, 12);
  const dir = path.join(gateDir(), `xaml-checker-${hash}`);
  const exe = path.join(dir, "bin", "Release", "net10.0", "XamlChecker.exe");
  if (fs.existsSync(exe)) return exe;
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
  return !build.error && fs.existsSync(exe) ? exe : null;
}

/** C# のコード片を、型・メンバー・文のどれを含むかに応じて包む。using の行は空行にして行番号を保つ。 */
function wrapCsharp(code, idx) {
  const usingRe = /^\s*(?:global\s+)?using\s+(?:static\s+)?[\w.]+(?:\s*=\s*[\w.<>, ]+)?\s*;\s*$/;
  const usings = [];
  const body = code.split("\n").map((l) => {
    if (usingRe.test(l)) {
      usings.push(l.trim());
      return "";
    }
    return l;
  });
  const joined = body.join("\n");
  const typeDecl = /^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|sealed|static|partial|abstract|readonly|file)\s+)*(?:class|record|struct|interface|enum)\s+\w/m;
  const memberDecl = /^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal|static|async|override|virtual|abstract|readonly|const|event|partial)\s/m;
  const hasNamespace = /^\s*namespace\s/m.test(joined);
  let kind;
  let content;
  if (typeDecl.test(joined) || hasNamespace) {
    kind = "type";
    content = `#line 1 "snippet${idx}"\n${joined}`;
  } else if (memberDecl.test(joined)) {
    kind = "members";
    content = `public partial class Snippet${idx}\n{\n#line 1 "snippet${idx}"\n${joined}\n}`;
  } else {
    kind = "statements";
    content = `public partial class Snippet${idx}\n{\n    public async System.Threading.Tasks.Task SnippetBody()\n    {\n#line 1 "snippet${idx}"\n${joined}\n    await System.Threading.Tasks.Task.CompletedTask;\n    }\n}`;
  }
  // 日英の記事に同じ名前の型があってもぶつからないよう、コード片ごとに名前空間を分ける。
  const ns = hasNamespace ? "" : `namespace SnippetNs${idx};\n`;
  return { source: `${usings.join("\n")}\n${ns}${content}\n`, kind };
}

/** XAML のコード片を、宣言の無い接頭辞を補ったうえで、読める形の XML にする。 */
function wrapXaml(code) {
  const trimmed = code.trim();
  if (/^<\?xml|^<[\w:.]+[^>]*\sxmlns=/.test(trimmed)) {
    return { xml: code, lineOffset: 0 };
  }
  const declared = new Set([...code.matchAll(/xmlns:(\w+)=/g)].map((m) => m[1]));
  const used = new Set([...code.matchAll(/<\/?(\w+):[\w.]+|\s(\w+):[\w.]+\s*=/g)].map((m) => m[1] ?? m[2]).filter((p) => p && p !== "xmlns"));
  const attrs = [`xmlns="${xamlNamespaces[""]}"`];
  for (const p of new Set([...Object.keys(xamlNamespaces).filter(Boolean), ...used])) {
    if (declared.has(p)) continue;
    attrs.push(`xmlns:${p}="${xamlNamespaces[p] ?? `urn:snippet:${p}`}"`);
  }
  return { xml: `<SnippetRoot ${attrs.join(" ")}>\n${code}\n</SnippetRoot>`, lineOffset: 1 };
}

if (snippets.length === 0) {
  out.push("（対象なし）", "");
} else {
  // C# のコード片はライブラリとしてまとめてコンパイルする。XAML のファイルはこのプロジェクトに入れない
  // （拡張子 .xaml のまま置くと、WPF のビルドがページとしてコンパイルしようとして、C# のエラーまで進まない）。
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "article-snippets-"));
  const xamlDir = fs.mkdtempSync(path.join(os.tmpdir(), "article-xaml-"));
  try {
    fs.writeFileSync(
      path.join(dir, "Snippets.csproj"),
      `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);CS8618;CS0169;CS0414;CS1998;CS8602;CS8604;CS0168;CS0219</NoWarn>
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

    const xamlFiles = [];
    let csharpCount = 0;
    snippets.forEach((s, idx) => {
      if (s.lang === "csharp") {
        csharpCount++;
        const w = wrapCsharp(s.code, idx);
        s.kind = w.kind;
        fs.writeFileSync(path.join(dir, `Snippet${idx}.cs`), w.source);
      } else {
        const w = wrapXaml(s.code);
        s.lineOffset = w.lineOffset;
        const f = path.join(xamlDir, `snippet${idx}.xml`);
        fs.writeFileSync(f, w.xml);
        xamlFiles.push([f, idx]);
      }
    });

    const errors = new Map(); // idx -> [{ line, code, msg }]
    let csharpChecked = false;
    if (csharpCount > 0) {
      const build = spawnSync("dotnet", ["build", "-nologo", "-v", "q", "-clp:NoSummary", "-nodeReuse:false"], {
        cwd: dir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024, timeout: 300000,
      });
      if (build.error) {
        out.push(`.NET SDK を起動できなかったか時間切れになったので、C# のコード片を確かめていない: ${build.error.message}`, "");
      } else {
        for (const l of `${build.stdout}\n${build.stderr}`.split(/\r?\n/)) {
          const m = l.match(/[Ss]nippet(\d+)(?:\.cs)?\((\d+),(\d+)\): error (CS\d+): (.+?)(?: \[.*\])?$/);
          if (m) {
            const list = errors.get(Number(m[1])) ?? [];
            const key = `${m[2]}:${m[4]}:${m[5]}`;
            if (!list.some((x) => x.key === key)) list.push({ key, line: Number(m[2]), code: m[4], msg: m[5] });
            errors.set(Number(m[1]), list);
          }
        }
        if (build.status !== 0 && errors.size === 0) {
          out.push("C# のコード片のビルドが失敗したが、コード片のエラーとして読み取れなかったので、C# のコード片は確かめていない。出力の先頭:", "", "```text", ...`${build.stdout}`.split(/\r?\n/).slice(0, 10), "```", "");
        } else {
          csharpChecked = true;
        }
      }
    }

    const xamlResults = new Map();
    if (xamlFiles.length) {
      const exe = xamlChecker();
      if (!exe) {
        out.push("XAML のチェック用プログラムをビルドできなかったので、XAML は確かめていない。", "");
      } else {
        const run = spawnSync(exe, xamlFiles.map(([f]) => f), { encoding: "utf8", timeout: 60000 });
        for (const l of (run.stdout ?? "").split(/\r?\n/)) {
          const [st, f, pos, msg] = l.split("\t");
          const hit = xamlFiles.find(([x]) => x === f);
          if (hit) xamlResults.set(hit[1], st === "OK" ? null : { pos, msg });
        }
      }
    }

    const syntaxStop = [...errors.values()].flat().some((e) => syntaxCodes.has(e.code));
    const kindLabel = { type: "型として", members: "クラスのメンバーとして包んで", statements: "メソッドの本体として包んで" };
    snippets.forEach((s, idx) => {
      const where = `\`${s.file}:${s.line}\`（${s.lang}）`;
      if (s.lang === "csharp") {
        const list = errors.get(idx) ?? [];
        if (!csharpChecked) {
          cResults.push("SKIP");
          out.push(`- ${where}: 確かめていない`);
        } else if (list.length === 0 && syntaxStop) {
          cResults.push("SKIP");
          out.push(`- ${where}: 構文は読めたが、ほかのコード片の構文の誤りでコンパイラが止まったので、名前と型は確かめていない（誤りを直して実行し直す）`);
        } else if (list.length === 0) {
          cResults.push("OK");
          out.push(`- ${where}: ${kindLabel[s.kind]}コンパイルできた`);
        } else {
          const hard = list.filter((e) => !outsideRef.has(e.code) && !missingMember.has(e.code));
          const api = list.filter((e) => missingMember.has(e.code));
          if (hard.length) {
            cResults.push("NG");
            out.push(`- ${where}: **構文・型の誤りの可能性**（${kindLabel[s.kind]}コンパイルした）`);
          } else if (api.length) {
            cResults.push("NG");
            out.push(`- ${where}: **存在しないメンバーを呼んでいる（実在しない API の可能性）**`);
          } else {
            cResults.push("REF");
            out.push(`- ${where}: 抜粋の外で宣言された名前を参照しているだけの可能性（要確認）`);
          }
          for (const e of list.slice(0, 8)) {
            out.push(`  - コード片の ${e.line} 行目 ${e.code}: ${e.msg}`);
          }
        }
      } else {
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
          out.push(`- ${where}: **XML として読めない**（コード片の ${ln - s.lineOffset} 行目 ${col} 文字目）: ${r.msg}`);
        }
      }
    });
    out.push("");
    out.push(
      "C# の抜粋は、型の宣言を含むものはそのまま、メンバーだけのものはクラスで、文だけのものはメソッドの本体で包んでコンパイルした。" +
        "宣言の無い名前の参照（CS0103・CS0246・CS0234）は「抜粋の外を参照しているだけの可能性」に分けた。存在しないメンバーの呼び出し（CS1061・CS0117）は誤りとして扱う。" +
        "構文の誤りがあると、名前の誤りは報告されないことがある。XAML は、名前空間の宣言が無いものを WPF の既定の名前空間で包み、宣言の無い接頭辞は仮の名前空間で補って読んだ。",
      "",
    );
  } finally {
    for (const d of [dir, xamlDir]) {
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

// JavaScript の関数宣言は、直前に JSDoc（/** で始まるコメント）があるかで数える。入れ子の関数も数える。
for (const [file, lines] of changed) {
  if (!/\.(?:mjs|cjs|js|ts)$/.test(file)) continue;
  const text = readLines(file);
  const decls = [];
  text.forEach((t, i) => {
    if (/^\s*(?:export\s+)?(?:async\s+)?function\s*\*?\s*\w+\s*\(/.test(t)) decls.push(i);
  });
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
    "CodeRabbit の Docstring Coverage は、変更した関数のうち循環的複雑度 2 以上だけを数えて 80% を基準にする。ここでは複雑度で絞らないので、近い値の目安にとどまる。" +
    "C# は型の直下の、修飾子の付いたメソッドだけを数える（ローカル関数は数えない）。JavaScript は function 宣言を入れ子も含めて数える。",
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
