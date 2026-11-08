// 独立レビューの前に、機械で確かめられることを確かめて報告にまとめる。
// 対象は origin/main からの差分（未コミット・未追跡を含む）で追加・変更した行だけ。
//
//   (a) 断定語（必ず・すべて・常に など）を含む行
//   (b) 比率・比較の文と、その記事が読み込む表（数値の照合はレビュアーが行う。材料をそろえるまで）
//   (c) 記事のコード片: C# はコンパイル、XAML は XML として読めるか
//   (d) 変更した C# のメソッドのうち、説明コメント（///）があるものの割合（CodeRabbit の基準は 80%）
//
// 使い方:
//   node tools/review-gate/article-check.mjs [比べる相手（既定 origin/main）]
// 出力: .git/review-gate/article-check.md（コミットされない）。パスと要約を標準出力に書く。
// 前提: git fetch origin main ができること。(c) には .NET 10 SDK が要る（無ければ (c) を飛ばしてそう書く）。
// 終了コード: 報告を作れたら 0（見つかった問題の有無にかかわらない）。差分を取れなければ 1。
import { execFileSync, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { gateDir, git, repoRoot } from "./lib.mjs";

const base = process.argv[2] ?? "origin/main";
const root = repoRoot();
process.chdir(root);

try {
  git(["fetch", "origin", "main"]);
} catch (e) {
  console.error(`git fetch origin main に失敗した。比べる相手の新しさを保証できないので止める。\n${e.message}`);
  process.exit(1);
}

// ---- 差分の取得（ファイルごとの追加行） ----
const changed = new Map(); // path -> Set(行番号, 1 始まり)
const diff = git(["diff", "--ignore-cr-at-eol", "-U0", "--no-color", base, "--", "."]);
let current = null;
for (const line of diff.split("\n")) {
  const file = line.match(/^\+\+\+ b\/(.+)$/);
  if (file) {
    current = file[1];
    changed.set(current, changed.get(current) ?? new Set());
    continue;
  }
  if (line.startsWith("+++ /dev/null")) {
    current = null;
    continue;
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
for (const file of git(["ls-files", "--others", "--exclude-standard"]).split("\n").filter(Boolean)) {
  const n = fs.readFileSync(file, "utf8").split(/\r?\n/).length;
  changed.set(file, new Set(Array.from({ length: n }, (_, i) => i + 1)));
}
for (const [file] of changed) {
  if (!fs.existsSync(file)) {
    changed.delete(file);
  }
}

const readLines = (file) => fs.readFileSync(file, "utf8").split(/\r?\n/);
const isArticle = (file) => /^_articles_(ja|en)\/.+\.md$/.test(file);
const isProse = (file) => file.endsWith(".md");
const out = [];
const summary = [];

// ---- (a) 断定語 ----
const absolute = /必ず|すべて|全て|常に|たびに|絶対|一切|決して|どんな場合も|例外なく|以降は|\balways\b|\bnever\b|\bevery\b|\ball of\b|\bguarantee/i;
const aHits = [];
for (const [file, lines] of changed) {
  if (!isProse(file)) continue;
  const text = readLines(file);
  for (const n of [...lines].sort((x, y) => x - y)) {
    const t = text[n - 1] ?? "";
    if (absolute.test(t) && !t.trim().startsWith("|---")) {
      aHits.push(`- \`${file}:${n}\` ${t.trim().slice(0, 200)}`);
    }
  }
}
out.push("## (a) 断定語を含む追加行", "", "測った条件・件数・環境の範囲に収まる言い方かを確かめる。", "");
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
out.push("## (b) 比率・比較を含む追加行と、その記事が読み込む表", "");
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
    const open = text[i].match(/^\s*```\s*(csharp|cs|c#|xml|xaml)\s*$/i);
    if (!open) continue;
    let j = i + 1;
    while (j < text.length && !/^\s*```\s*$/.test(text[j])) j++;
    const touched = [...Array(j - i + 1).keys()].some((k) => lines.has(i + 1 + k));
    if (touched) {
      const lang = /xml|xaml/i.test(open[1]) ? "xaml" : "csharp";
      snippets.push({ file, line: i + 2, lang, code: text.slice(i + 1, j).join("\n") });
    }
    i = j;
  }
}
out.push("## (c) 記事のコード片（追加・変更した行を含むもの）", "");
const cResults = [];

/** XAML を XML として読めるか確かめるプログラム。一度ビルドしたら .git/review-gate に置いて使い回す。 */
function xamlChecker() {
  const dir = path.join(gateDir(), "xaml-checker");
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
  fs.writeFileSync(
    path.join(dir, "Program.cs"),
    `using System.Xml;

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
`,
  );
  const build = spawnSync("dotnet", ["build", "-nologo", "-v", "q", "-c", "Release"], { cwd: dir, encoding: "utf8" });
  return !build.error && fs.existsSync(exe) ? exe : null;
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
    <NoWarn>$(NoWarn);CS8618;CS0169;CS0414;CS1998;CS8602;CS8604</NoWarn>
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
        const usings = [];
        const body = [];
        for (const l of s.code.split("\n")) {
          if (/^\s*using\s+[\w.]+\s*;\s*$/.test(l)) usings.push(l.trim());
          else body.push(l);
        }
        const hasType = /^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|sealed|static|partial|abstract|file)\s+)*(?:class|record|struct|interface|enum|namespace)\s/m.test(body.join("\n"));
        // 日英の記事に同じ名前の型があってもぶつからないよう、コード片ごとに名前空間を分ける。
        const ns = /^\s*namespace\s/m.test(body.join("\n")) ? "" : `namespace SnippetNs${idx};\n`;
        const content = hasType
          ? `#line 1 "snippet${idx}"\n${body.join("\n")}`
          : `public partial class Snippet${idx}\n{\n#line 1 "snippet${idx}"\n${body.join("\n")}\n}`;
        fs.writeFileSync(path.join(dir, `Snippet${idx}.cs`), `${usings.join("\n")}\n${ns}${content}\n`);
        s.wrapped = !hasType;
      } else {
        const trimmed = s.code.trim();
        const hasRootNs = /^<\?xml|^<[\w:.]+[^>]*\sxmlns=/.test(trimmed);
        const xml = hasRootNs
          ? s.code
          : `<SnippetRoot xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:d="http://schemas.microsoft.com/expression/blend/2008" xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" xmlns:local="clr-namespace:App" xmlns:sys="clr-namespace:System;assembly=System.Runtime">\n${s.code}\n</SnippetRoot>`;
        s.lineOffset = hasRootNs ? 0 : 1;
        const f = path.join(xamlDir, `snippet${idx}.xml`);
        fs.writeFileSync(f, xml);
        xamlFiles.push([f, idx]);
      }
    });

    const errors = new Map(); // idx -> [{ line, code, msg }]
    let csharpChecked = false;
    if (csharpCount > 0) {
      const build = spawnSync("dotnet", ["build", "-nologo", "-v", "q", "-clp:NoSummary"], { cwd: dir, encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
      if (build.error) {
        out.push(`.NET SDK を起動できなかったので C# のコード片を確かめていない: ${build.error.message}`, "");
      } else {
        csharpChecked = true;
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
          out.push("C# のコード片のビルドが失敗したが、コード片のエラーとして読み取れなかった。出力の先頭:", "", "```text", ...`${build.stdout}`.split(/\r?\n/).slice(0, 10), "```", "");
        }
      }
    }

    const xamlResults = new Map();
    if (xamlFiles.length) {
      const exe = xamlChecker();
      if (!exe) {
        out.push("XAML のチェック用プログラムをビルドできなかったので、XAML は確かめていない。", "");
      } else {
        const run = spawnSync(exe, xamlFiles.map(([f]) => f), { encoding: "utf8" });
        for (const l of (run.stdout ?? "").split(/\r?\n/)) {
          const [st, f, pos, msg] = l.split("\t");
          const hit = xamlFiles.find(([x]) => x === f);
          if (hit) xamlResults.set(hit[1], st === "OK" ? null : { pos, msg });
        }
      }
    }

    const outsideRef = new Set(["CS0103", "CS0246", "CS0117", "CS1061", "CS0120", "CS0234", "CS0236", "CS0201"]);
    // 構文の誤り（CS1xxx）がどこかにあると、コンパイラは名前と型を確かめる段階に進まない。
    const syntaxStop = [...errors.values()].flat().some((e) => /^CS1\d{3}$/.test(e.code) && e.code !== "CS1061");
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
          out.push(`- ${where}: コンパイルできた${s.wrapped ? "（クラスで包んだ）" : ""}`);
        } else {
          const hard = list.filter((e) => !outsideRef.has(e.code));
          cResults.push(hard.length ? "NG" : "REF");
          out.push(`- ${where}: ${hard.length ? "**構文・型の誤りの可能性**" : "抜粋の外を参照しているだけの可能性（要確認）"}`);
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
    out.push("抜粋は、宣言の無い名前を参照していることがある（CS0103 などは「抜粋の外を参照しているだけ」に分けた）。分けた結果が正しいかはレビュアーが確かめる。構文の誤りがあると、名前の誤りは報告されないことがある。XAML は、名前空間の宣言が無いものを WPF の既定の名前空間で包んで読んだ。", "");
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
    fs.rmSync(xamlDir, { recursive: true, force: true });
  }
}
summary.push(`(c) コード片 ${snippets.length} 個（誤りの可能性 ${cResults.filter((r) => r === "NG").length}、要確認 ${cResults.filter((r) => r === "REF").length}）`);

// ---- (d) 説明コメントの割合 ----
const methodDecl = /^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|new|partial|unsafe|extern|readonly)\s+)+(?:[\w<>[\],.?()]+\s+)?(\w+)\s*(?:<[^>()]*>)?\s*\(/;
const notMethod = /^\s*(?:return|new|if|while|for|foreach|switch|catch|using|lock)\b/;
let total = 0;
let documented = 0;
const missing = [];
for (const [file, lines] of changed) {
  if (!file.endsWith(".cs")) continue;
  const text = readLines(file);
  // 波かっこの深さを追い、型の本体の直下にある宣言だけをメンバーとして数える（ローカル関数は数えない）。
  const decls = [];
  const typeDepths = [];
  let depth = 0;
  let pendingType = false;
  text.forEach((t, i) => {
    const code = t.replace(/"(?:[^"\\]|\\.)*"/g, '""').replace(/\/\/.*$/, "");
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
    const end = k + 1 < decls.length ? decls[k + 1] : text.length;
    let touched = false;
    for (let n = d + 1; n <= end; n++) if (lines.has(n)) touched = true;
    if (!touched) return;
    total++;
    let p = d - 1;
    while (p >= 0 && (/^\s*\[/.test(text[p]) || text[p].trim() === "")) {
      if (text[p].trim() === "") break;
      p--;
    }
    if (p >= 0 && /^\s*\/\/\//.test(text[p])) documented++;
    else missing.push(`- \`${file}:${d + 1}\` ${text[d].trim().slice(0, 150)}`);
  });
}
// JavaScript の関数宣言は、直前の行が JSDoc（/** ... */）の終わりかで数える。
for (const [file, lines] of changed) {
  if (!/\.(?:mjs|cjs|js|ts)$/.test(file)) continue;
  const text = readLines(file);
  const decls = [];
  text.forEach((t, i) => {
    if (/^\s*(?:export\s+)?(?:async\s+)?function\s*\*?\s*\w+\s*\(/.test(t)) decls.push(i);
  });
  decls.forEach((d, k) => {
    const end = k + 1 < decls.length ? decls[k + 1] : text.length;
    let touched = false;
    for (let n = d + 1; n <= end; n++) if (lines.has(n)) touched = true;
    if (!touched) return;
    total++;
    if (d > 0 && /\*\/\s*$/.test(text[d - 1])) documented++;
    else missing.push(`- \`${file}:${d + 1}\` ${text[d].trim().slice(0, 150)}`);
  });
}
const pct = total ? Math.round((documented / total) * 1000) / 10 : 100;
out.push("## (d) 変更した C# のメソッドと JavaScript の関数の説明コメント", "");
out.push(`変更を含むメソッド・関数 ${total} 個のうち、説明コメント（C# は ///、JavaScript は /** */）があるもの ${documented} 個（${pct}%）。CodeRabbit の基準は 80%（ローカル関数は数えていない）。`, "");
out.push(...(missing.length ? ["説明コメントが無いもの:", ...missing] : ["（すべてにある）"]), "");
summary.push(`(d) 説明コメント ${pct}%（${documented}/${total}）`);

const report = path.join(gateDir(), "article-check.md");
fs.writeFileSync(report, ["# 機械で確かめた結果", "", `比べる相手: ${base}（${git(["rev-parse", "--short", base])}）`, "", ...out].join("\n"));
console.log(report);
console.log(summary.join(" / "));
