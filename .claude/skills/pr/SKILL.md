---
name: pr
description: PR を作成し、レビュー(Copilot・CodeRabbit 等)の Webhook 通知を待って指摘を修正し、再レビューを依頼する。指摘が無くなるまで「修正 → 再レビュー依頼」を繰り返し、指摘がゼロになったらマージする。「PR を出して」「レビューを回してマージまで」などの依頼で使用する。
---

# PR 作成・レビュー対応・マージ ワークフロー

作業ブランチ上の変更に対して **PR 作成 → レビュー待ち → 指摘修正 → 再レビュー依頼(ループ)→ マージ** を実行するスキル。
記事作成は含まない。既にコミット済み(または本セッションでコミットできる)変更があり、それを PR にして公開まで持っていく場面で使う。

> **言語ルール:** ユーザーへの発信(質問・進捗報告・PR 説明文・コミットメッセージなど)はすべて **日本語** で行う(`CLAUDE.md`)。
>
> **参考:** レビュー対応・マージの手順は `article-workflow` スキルの Phase 4・5 に準拠する。
> 本スキルはその PR 以降の部分を単体で実行するためのもの。

---

## Phase 1. コミット & プッシュ

1. **変更をコミットする。** 未コミットの変更があればコミットする。
   追加の変更が無い場合はコミットを省略する(`nothing to commit` で止めない)。

2. **プッシュする。** `git push -u origin "<branch>"` で作業ブランチへ push する。
   - ブランチ名は必ずクォートして渡す(シェルのメタ文字によるコマンドインジェクションを防ぐ)。
   - ネットワークエラー時は 2s/4s/8s/16s の指数バックオフで最大 4 回リトライする。
   - **push に成功してから Phase 2 へ進む。** 4 回の再試行をすべて失敗した場合、またはネットワーク以外の
     エラー(認証・権限・非 fast-forward 等)が発生した場合は Phase 2 へ進まず、処理を中止してユーザーへ報告する。
     リモートに最新コミットが無いまま古い内容で PR を作成・再利用しないためのガードである。
   - 未 push のコミットが残っていれば push してから Phase 2 へ進む。

---

## Phase 2. PR 作成

1. **既存の PR を確認する(重複作成の防止)。** PR 作成前に、現在のブランチから `main` 宛ての
   オープンな PR が既に存在しないかを `mcp__github__list_pull_requests`(`head` にブランチ、`base` に `main` を指定)で検索する。
   - **ベースが `main` の PR だけを対象にする。** `head` だけで絞ると、同じブランチから別ベース向けに
     作られた PR を誤って再利用しかねない。`base` でも絞るか、返却結果の `base.ref == "main"` を必ず確認する。
   - **既にオープンな `main` 宛て PR があれば、それを再利用する。** `create_pull_request` は再実行せず、その PR 番号で
     手順 3(購読)へ進む。Phase 3 のフォールバックからセッションを再開した場合など、重複した PR や
     購読を作らないためのガードである。
   - 該当するオープンな PR が無い場合のみ、手順 2 で新規作成する。

2. **PR を作成する。** GitHub MCP(`mcp__github__create_pull_request`)で `main` 宛の PR を作成する。
   - リポジトリに PR テンプレート(`.github/pull_request_template.md` 等)があれば、その見出し構成に沿って本文を埋める。
   - タイトル・本文は日本語で記述する。変更内容を要約し、テンプレートの各節を埋める。

3. **PR を購読する。** `subscribe_pr_activity` で PR を購読し、以降のイベント
   (CI 結果・レビューコメント)を `<github-webhook-activity>` として受け取れるようにする。

4. **購読直後に PR 状態を再取得して同期する。** PR 作成から購読までの間に CI や自動レビューが
   始まる/完了することがあり、その分の Webhook を取りこぼす可能性がある。購読直後に
   `mcp__github__pull_request_read`(`get_status` / `get_check_runs` / `get_review_comments`)で
   状態を再取得し、既に発生済みの CI 結果・レビュー指摘を Phase 3 に持ち込む。

---

## Phase 3. レビュー待ち → 指摘修正 → 再レビュー依頼(指摘ゼロまでループ)

**指摘が無くなるまで**、以下 1〜5 を繰り返す。

1. **レビューの Webhook 通知を待つ。** PR 作成後、Copilot や CodeRabbit 等の自動レビューが始まる。
   - **`sleep` でのポーリングはしない。** レビューコメントや CI 結果は
     `<github-webhook-activity>` イベントとしてこのセッションに届く。イベントの到着を待つ。
   - **`send_later` でのセルフチェックイン予約は行わない。** CI 成功・新規 push・マージコンフリクトの
     遷移は Webhook で届かないことがあるが、`sleep` ポーリングも `send_later` 予約もしない。
     長時間反応が無い場合はユーザーへ指示を仰がず、`mcp__github__pull_request_read`
     (`get_status` / `get_check_runs` / `get_review_comments`)で状況を確認し、Phase 3 を続行する。

2. **自動レビューボットの完了を必ず待つ。** `coderabbitai` などはレビュー要求後に処理が走り、
   完了まで数分かかる(CodeRabbit は `auto_incremental_review: false` のため push だけでは再レビューされず、
   `@coderabbitai review` の要求で処理が始まる)。**「review in progress」「Currently processing…」などの
   処理中表示が出ている間は、絶対に次へ進まない(マージしない)。**
   - CI がグリーンでも、walkthrough や「Pre-merge checks passed」だけを根拠に指摘ゼロと判断しない。
     これらは中間シグナルで、後から actionable な指摘が追加されることがある。
   - 判断が付かない場合は、次の Webhook イベントの到着を待つか、`mcp__github__pull_request_read` で
     状況を確認して進める(ユーザーへ指示を仰がない)。

3. **指摘・CI 失敗を修正する。** レビュー指摘または CI 失敗があるたびに:
   - 原因を診断してコード・記事・設定を修正する。
   - コミット & プッシュし、必要なら CI を再実行(`mcp__github__actions_run_trigger` や再 push)する。
   - 修正の解釈に曖昧さがある場合や大規模改修が必要な場合は、`AskUserQuestion` で確認する
     (破壊的・不可逆な判断はユーザー確認を優先する)。
   - 指摘は件数を絞らない。見つけたものはすべて解消する。

4. **対応を終えたレビュースレッドは解決済みにする。**
   - **修正で対応した指摘** — 修正をコミット・プッシュしたうえで、`mcp__github__resolve_review_thread` で
     該当スレッドを解決済み(resolved)にする。
   - **反論・見送りで対応を終えた指摘**(誤検知・方針上採用しない等) — 理由を
     `mcp__github__add_reply_to_pull_request_comment` で **1 回だけ** 簡潔に返信し、
     対応方針が確定した時点でスレッドを解決済みにする。
   - スレッド ID は `mcp__github__pull_request_read`(`method: "get_review_comments"`)の各スレッドの `id` で取得する。
     修正で行が変わると `is_outdated` になるが、`is_resolved` は手動で解決するまで `false` のままである点に注意する。

5. **再レビューを依頼する。** 修正して push したら、**再レビュー依頼はレビュアーに対してのみ**行えばよい。
   ただし **MarkdownLint(`Lint Markdown` CI)も併せて再レビュー(再実行結果の確認)する**。
   - **Copilot** — push では自動再レビューされない(PR 作成時に 1 回走るだけ)。
     `mcp__github__request_copilot_review` で再レビューを要求する。
   - **CodeRabbit** — `.coderabbit.yaml` で `auto_incremental_review: false` としているため、
     push だけでは自動再レビューされない。PR に `@coderabbitai review` とコメントして明示的に再レビューを要求する。
   - **MarkdownLint(`Lint Markdown` CI)** — `pull_request` トリガーで push のたびに自動再実行される。
     レビュアーの再レビューと併せて、この CI の再実行結果(グリーン)も必ず確認する。
   - 再レビューで新たな指摘が来たら、手順 3〜5 を繰り返す。
   - **新たな指摘が無くなるまで**、「修正 push → 再レビュー依頼 → 対応・解決」を繰り返す
     (直近コミットに対する未解決スレッドがゼロになるまで)。

6. **マージコンフリクトを検知したら、マージのブロッカーとして解消する。** `main` が先行して
   PR が競合状態(`mcp__github__pull_request_read`(`get`)の `mergeable_state` が `dirty` 等)になったら、
   マージ不可のブロッカーとして扱い、解消するまで Phase 4 へ進まない。
   - `git fetch origin main` で最新の `main` を取得し、作業ブランチへ取り込む(`git merge origin/main`
     または `git rebase origin/main`)。
   - 競合を解消してコミットし、`git push`(rebase した場合は `--force-with-lease`)する。
   - push により CI は自動再実行される。必要なら Copilot 等へ再レビューを要求する。
   - 解消後は手順 1〜5 のレビュー・CI ループへ戻り、再び指摘ゼロ・グリーンを確認する。

> 画面から手動で行う場合は、PR の「Reviewers」欄にある Copilot 横の 🔄(Re-request review)から再レビューを要求できる。

---

## Phase 4. マージ

以下のマージ直前チェックリストを **すべて満たしてから** マージする。1 つでも満たさなければマージしない。

- [ ] 最新コミットに対する CI(`Lint Markdown` 等)が success。
- [ ] Copilot 再レビューが完了し、未解決スレッドがゼロ。
- [ ] CodeRabbit 等の全自動レビューボットの再レビューが **完了**(処理中表示なし)し、actionable な未解決指摘がゼロ。
- [ ] **全レビュースレッドが解決済みである。** `get_review_comments` で取得した **すべての** スレッドについて、
      作成者(Copilot・CodeRabbit・人間レビュアー・その他の自動ボットを問わず)に関係なく `is_resolved == true` を要求する。
      未解決スレッドが 1 件でもあればマージしない。
- [ ] **`CHANGES_REQUESTED` のレビューが残っていない。** `get_reviews` で、dismiss されていないレビュー提出に
      `state == CHANGES_REQUESTED` が 1 件も無いことを確認する。**本文だけで紐づくスレッドが無い
      `CHANGES_REQUESTED` レビューでもマージをブロックする**(スレッド解決の確認だけでは漏れるため)。
      該当レビューは、修正のうえレビュアーに再レビューを依頼して承認/取り下げを得るか、正当な理由で dismiss されるまで解消しない。
      この条件はマージ直前の再取得後にも再検証する。
- [ ] **事前スナップショットを記録する。** マージ判定を始める前に、差分比較の基準として次を控える:
      現在の HEAD SHA、直近レビューコメントの ID、各 check run の ID と **status/conclusion**、
      各レビュースレッドの ID と **is_resolved**、
      各レビュー提出(`get_reviews`)の ID と **state**(`APPROVED` / `CHANGES_REQUESTED` / `DISMISSED` 等)。
- [ ] **マージ実行の直前に最新状態を再取得し、スナップショットと照合する。**
      `mcp__github__pull_request_read`(`get_status` / `get_check_runs` / `get_review_comments` / `get_reviews`)で再取得し、
      各対象(check run・レビュースレッド・レビュー提出)の **完全な ID セット** を突き合わせる。
      以下のいずれかが 1 件でもあれば **マージを中止してチェックリストを最初からやり直す**:
      新しい push・コミット・レビューコメント・check run の **追加または削除**、**既存 check run の status/conclusion の変化**、
      **既存レビュースレッドの追加・削除・is_resolved の変化**、
      **既存レビュー提出の追加・削除・state の変化**(`APPROVED` / `CHANGES_REQUESTED` / dismissal。新規コメントや check run を
      伴わず、同じ ID のまま state だけが変わる場合や、スナップショットにあった項目が消えた場合も含む)。
      差分が無い場合のみ次へ進む(TOCTOU 回避)。
- [ ] **サーバー側ガードが無い場合は自動マージしない。** `main` にブランチ保護(必須ステータスチェック・
      Require branches to be up to date)が構成されておらず、厳密な保証が必要な場合は、
      `merge_pull_request` を呼ばずに中止してユーザーへ報告し、判断を仰ぐ。

> **マージ認可について:** 本スキルは `article-workflow` と同様、上記チェックリストをすべて満たしたら
> **マージ直前の追加確認なしで自動マージする** 設計である。スキルの起動そのものがマージの認可にあたる
> (ユーザーがこのワークフローの実行を指示した時点で、チェックリスト充足後のマージまでを許可している)。
> ただしチェックリストの各項目(特に「サーバー側ガードが無い場合は自動マージしない」)や、
> 修正方針に重大な曖昧さがある場合の `AskUserQuestion` は引き続き適用され、無条件の autonomy ではない。

チェックリストを満たしたら:

1. **マージする。** `mcp__github__merge_pull_request` で PR を `main` にマージする。
2. **マージ成功を確認する。** 応答の `merged: true` とマージコミット SHA を確認し、続けて
   `mcp__github__pull_request_read`(`get`)で PR 状態が `MERGED`、かつマージ済み head が事前スナップショットの
   HEAD SHA と一致することを確認する。想定と異なる head がマージされていたら続行せずユーザーへ報告する。
3. **リモートブランチ削除は GitHub の自動削除に任せる(手動削除コマンドは実行しない)。**
   本リポジトリは「Automatically delete head branches」が有効なため、マージすると head ブランチは自動削除される。
   > ⚠️ Web 実行環境ではリモートブランチの手動削除はできない(`git push origin --delete` も REST API も 403 で失敗する)。試みない。
4. **ローカルの作業ブランチ**は不要なら削除してよい(`git branch -d "<branch>"`。ブランチ名はクォートして渡す)。
5. **購読を解除する。** `unsubscribe_pr_activity` で PR の監視を終了する。
6. 完了をユーザーへ日本語で報告する(PR 番号・マージ済みである旨)。

---

## 全体フロー要約

```text
Phase 1  コミット & プッシュ
Phase 2  PR 作成 → subscribe_pr_activity で購読
Phase 3  レビュー待ち(Webhook)→ 指摘修正 → 再レビュー依頼  ── 指摘ゼロまでループ
           (修正 push 後はレビュアー(Copilot / CodeRabbit)へ再レビューを要求し、MarkdownLint も併せて再確認、
            対応済みスレッドは解決済みにする)
Phase 4  マージ直前チェックリスト → マージ →(head ブランチは GitHub が自動削除)→ 購読解除 → 完了報告
```

## 判断の原則

- **指摘は件数を絞らない。** 見つけたものはすべて解消する。
- **`sleep` でポーリングせず、`send_later` での予約もしない。** レビュー・CI の結果は Webhook イベントで
  届くので、届くものは到着を待つ。CI 成功・新規 push・マージコンフリクトの遷移など **Webhook で届かない
  イベント** は、購読直後の再取得(Phase 2 手順 3)や、長時間反応が無い場合の `mcp__github__pull_request_read`
  での状況確認で補う(ユーザーへ指示を仰がず自分で確認して進める)。
- **対応を終えたレビュー指摘はスレッドを解決済みにする。** マージ前に未解決スレッドを残さない。
- **処理中の自動レビューボットがある間はマージしない。** 中間シグナルでマージ判断をしない。
- **リモートブランチの手動削除コマンドは実行しない**(Web 環境では 403 で必ず失敗する)。head ブランチの削除は自動削除に任せる。
- 修正方針に重大な曖昧さがある場合や不可逆操作の前は `AskUserQuestion` でユーザーに確認する。
