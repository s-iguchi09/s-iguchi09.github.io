# 技術記事作成 共通ガイドライン

技術記事の**作成・案出し・レビュー**で共通して踏まえる前提をまとめる。
`article-workflow`(記事作成〜公開)や `article-ideas`(記事案出し)など、記事に関わるスキルはこのファイルを参照する。
文体・構成・チェックリストの詳細は各ルールファイル(下記)に委ね、本ファイルはそれらを束ねる共通の土台とする。

> **発信は必ず日本語:** ユーザーへの発信(質問・進捗報告・PR 説明文・コミットメッセージ・記事案など)はすべて **日本語** で行う(`CLAUDE.md`)。記事本文のみ日本語版・英語版の両方を作成する。

---

## 1. リポジトリの前提

- 本リポジトリ(`s-iguchi09.github.io`)は Jekyll / GitHub Pages の**日本語・英語バイリンガル技術ブログ**である。
- 記事は Jekyll Collections で管理する。
  - 英語記事: `_articles_en/<slug>.md`(`layout: article-en`、公開 URL `/articles/<slug>/`)
  - 日本語記事: `_articles_ja/<slug>.md`(`layout: article-ja`、公開 URL `/ja/articles/<slug>/`)
- **日英は同一の `<slug>.md` ファイル名でペアにする。** 原則、日本語版・英語版の両方を作成する。
- 記事一覧ページ(各言語)は、その言語の記事ファイルを置けば自動で反映される。
- `sitemap.xml` は英語記事(`_articles_en/<slug>.md`)を起点に生成され、日本語版(`_articles_ja/<slug>.md`)の有無を判定する。そのため、**日英ペアを揃えて配置すれば**両言語が sitemap に反映される(日本語記事だけを置いても sitemap には載らない)。作成者はいずれの生成物も手で編集する必要はない。
- 各ページには `canonical`(正規 URL を示す link relation)と `hreflang`(言語別の alternate を示す属性)がレイアウト側の共通処理で自動付与される。

---

## 2. テーマの焦点

- 扱う領域は「**.NET / C# / WPF デスクトップ開発**」に特化する。無関係な言語・分野を持ち込まない。
- 記事の主軸は「**標準コントロールのトラブル解決型・実装解説型(how-to / troubleshooting)**」である。
  実務で遭遇する落とし穴・原因・解決策を、原因の背景から解く記事を中心とする。
- 各記事は明確に範囲が定義された**1 つの技術テーマ**を扱う(1 記事 1 テーマ)。

---

## 3. 執筆ルールの参照先

記事の文体・構成・必須要素・チェックは、以下のルールファイルに従う。作業前に必ず読むこと。

- `docs/rules/article/structures.md` — **記事の構成型(骨格の選び方)**
- `docs/rules/article/guidelines.md` — 執筆方針・文体・記事構成(§2 は構成型へ委譲)・必須要素・AdSense 適合性・図とスクリーンショット(§11)・**主張の検証(§12)**・内部リンクの正規 URL(§7.1)
- `docs/rules/article/template-ja.md` / `docs/rules/article/template-en.md` — 記事テンプレート(構成型 1「診断型・単一原因」の雛形)
- `docs/rules/article/review-checklist.md` — 公開前レビューチェックリスト
- `.markdownlint.json` — 有効な lint ルール(`MD060` は無効)

---

## 4. フロントマター規約

各記事のフロントマターには、以下を設定する(詳細は `review-checklist.md`)。

- `layout` — 言語に一致させる(日本語 `article-ja` / 英語 `article-en`)
- `title` — 技術テーマが即座に伝わるもの
- `date` — UTC 日付(`YYYY-MM-DD`)
- `category` — 自由文字列。既存は主に `WPF` / `C#` の 2 値
- `excerpt` — 記事の要約。160 字以内
- `image` — 任意。記事を代表するスクリーンショットのサイト絶対パス(`/images/articles/<slug>/<file>.png`)。構造化データの `image` として出力される(`guidelines.md` §11)

---

## 5. AdSense 適合性

記事には Google AdSense が掲載される。以下を満たす(詳細は `guidelines.md` §6)。

- 公式ドキュメントの言い換えに留まらない**独自性のある内容**を提供する。
- **本リポジトリの推奨文量の目安**(Google が定める固定要件ではない): 日本語 約 700〜1,500 字 / 英語 約 400〜900 語。数値は目安であり、文量より品質・密度を優先する(文量を満たすための水増しはしない)。
- **比較表・落とし穴・実用的な選択ガイダンス**のうち、少なくとも 1 つを含める。

---

## 6. 量産型コンテンツの回避と内部リンク

同一領域で関連記事を複数作ること自体は問題ではない。Google が問題視するのは、各記事に固有の価値がなく付加価値のない大量生成や検索順位の操作である。関連記事を作る際は、各記事に独自の価値を持たせ、付加価値のない量産にならないようにする(`guidelines.md` §10)。

- タイトルは連番(「〜を X 版で」等)にせず、各記事固有のテーマ・対象を主体にする。
- 概要冒頭の導入文をテンプレートとして使い回さない。各記事に固有の背景・注意点・比較を持たせる。
- 関連記事がある場合は**同一言語の記事どうしを内部リンク**で結ぶ。
  **日本語記事と英語記事の対応するカウンターパート間には相互リンクを張らない**(`guidelines.md` §7)。

---

## 7. 既存記事の把握(重複回避)

新しいテーマ・slug を決めるときは、**既存記事を実ファイルから把握して重複を避ける**。ハードコードした一覧に頼らず、その時点のリポジトリ状態を参照する。

- 既存記事の slug は、`_articles_en/` と `_articles_ja/` 配下の Markdown ファイル名(拡張子 `.md` を除いた部分)である。
  以下で列挙できる:

  ```bash
  # 既存記事の Markdown を列挙する（どちらのディレクトリが空でもエラーにならない）:
  find _articles_en _articles_ja -maxdepth 1 -type f -name '*.md'
  # slug（拡張子 .md を除いたファイル名）を一覧する場合:
  find _articles_en _articles_ja -maxdepth 1 -type f -name '*.md' -exec basename {} .md \; | sort -u
  ```

- 各記事のテーマは、必要に応じてファイル先頭のフロントマター(`title` / `category` / `excerpt`)を読んで把握する。
- **新しい slug は上記の既存ファイル名と重複させない。** テーマも既存記事と実質的に重ならないようにする。

### 7.1 構成の重複

テーマが重ならなくても、骨格が既存記事と揃いすぎることがある。構成型（`structures.md`）を選んだうえで、実際の重複を確認する。

```bash
# 日本語版と英語版を別々に数える。対応する翻訳どうしは同じ構成になるため、混ぜると常に 2 件ずつ数えてしまう。
# フェンスは開始記号の文字と長さを覚え、同じ文字で開始以上の長さのものだけを終了として扱う。
# ```` で囲んだ中に ``` があっても、そこでは閉じない。
for dir in _articles_ja _articles_en; do
  echo "--- $dir"
  for f in "$dir"/*.md; do
    awk '
      /^ {0,3}(`{3,}|~{3,})/ {
        line = $0; sub(/^ +/, "", line)
        ch = substr(line, 1, 1); n = 0
        while (substr(line, n + 1, 1) == ch) n++
        if (!inside) { inside = 1; fence_char = ch; fence_len = n }
        else if (ch == fence_char && n >= fence_len) { inside = 0 }
        next
      }
      !inside && /^## /
    ' "$f" | paste -sd'>'
  done | sort | uniq -c | sort -rn | head -5
done
```

- **完全に一致する構成は 5 記事までとする。** 6 記事以上になったら、いずれかの題材が型に合っていない可能性が高い。
- 上限を超えたときは、**見出し名を言い換えるのではなく型の選び直しを検討する**。外形を散らすこと自体が目的ではない（`structures.md`）。

---

## 8. 既存記事の補強

`guidelines.md` §12 の検証ルールは 2026-08-29 に追加したもので、それ以前の記事はこのルールを通っていない。
Search Console で「クロール済み - インデックス未登録」と判定された記事を実測で検証したところ、前提と図でバージョンが食い違う、計測していない性能差を断定している、推奨した実装が期待どおり動かない、といった誤りが見つかった。

同種の記事が残っているため、既存記事を実測で確かめ直す作業が必要になる。
これは**記事作成のワークフローとは別の作業**として、対象を選んで個別に行う。
`article-ideas` / `article-workflow` / `article-auto` は新規記事の作成を担うスキルであり、この作業を兼ねさせない。

本節は、その補強作業を行うときの参照情報である。

### 8.1 補強が必要な記事の把握

**検証済みかどうかは `docs/verification/` を見れば分かる。** 推測する必要はない。

シーンが `IScene.Verifies` を宣言していると、実行時に `docs/verification/<slug>.yml` が
生成される（`guidelines.md` §12.4）。このファイルの有無が、実測で確かめた記録そのものである。

```bash
# 実測で検証済みの記事:
find docs/verification -maxdepth 1 -name '*.yml' -printf '%f\n' 2>/dev/null | sed 's/\.yml$//' | sort

# 未検証の記事:
comm -23 \
  <(find _articles_ja _articles_en -maxdepth 1 -name '*.md' -printf '%f\n' | sed 's/\.md$//' | sort -u) \
  <(find docs/verification -maxdepth 1 -name '*.yml' -printf '%f\n' 2>/dev/null | sed 's/\.yml$//' | sort)
```

`ls` にグロブを渡すと、対象が 1 件も無いディレクトリでエラーになる。`find` はその場合も正常に終了する。

記録の中身を読めば、何をどこまで確かめたかも分かる。

```bash
cat docs/verification/<slug>.yml
```

以前は記事の書式（`検証環境` の行があるか、PNG を参照しているか）から推測していた。
**この方法は使わない。** 書き方の違いで検証済みの記事を未検証と誤判定し、
実際に再検証してしまったことがある。

なお、`docs/verification/` に記録があっても「記事のすべての主張を確かめた」ことにはならない。
記録の `verifies` に並ぶのは、そのシーンが実際に測った内容だけである。
補強の対象を選ぶときは、記事の主張と `verifies` を突き合わせ、測られていない主張が残っていないかを見る。

シーンをまだ持たない記事は、次で列挙できる。

```bash
comm -23 \
  <(find _articles_ja _articles_en -maxdepth 1 -name '*.md' -printf '%f\n' | sed 's/\.md$//' | sort -u) \
  <(grep -rho 'public string Slug => "[^"]*"' tools/screenshot-capture/Scenes/ \
      | sed 's/.*"\(.*\)"/\1/' | sort -u)
```

Search Console で「クロール済み - インデックス未登録」と判定されている記事があれば、それも対象として優先度が高い。

### 8.2 補強の進め方

1. 対象記事の主張を洗い出し、`tools/screenshot-capture` のシーンとして実装して実行する。
2. **実測が記事の記述と食い違った場合は、記事のほうを修正する**(`guidelines.md` §12.1)。
   誤りの訂正は、分量を増やすことより価値が高い。
3. 実測で分かった内容を加筆し、「前提・対象環境」に検証環境を明記する(`guidelines.md` §12.2)。
4. 数値は図に出力し、本文は比率で書く(`guidelines.md` §12.3)。

分量を増やすことは目的ではない。**検証されていない記述を検証済みにすること**が目的である。
