# Article Writing Guidelines

Rules for technical articles published on this site.  
Both Japanese (`_articles_ja/`) and English (`_articles_en/`) articles follow these guidelines.

---

## 1. Scope

These rules apply to all technical articles. Each article must address a single, clearly defined technical topic.

---

## 2. Article Structure

Use the following section order. Omit sections that are genuinely not applicable, but do not skip Overview, Implementation, or Summary.

| # | Section | Purpose |
|---|---|---|
| 1 | Overview | State what the article covers and what problem it solves |
| 2 | Prerequisites / Environment | Framework, language version, architecture assumptions |
| 3 | Problem | Describe the situation where the issue occurs |
| 4 | Cause / Background | Explain why the problem occurs |
| 5 | Solution | Summarize the approach taken |
| 6 | Implementation | Code examples with surrounding explanation |
| 7 | Notes | Constraints, edge cases, pitfalls |
| 8 | Alternatives / Comparison | Other approaches with trade-off table |
| 9 | Summary | Final recommendation with selection criteria |

---

## 3. Writing Style

### 3.1 Register

- Use declarative, descriptive sentences.
- Write in third-person or impersonal constructions; avoid addressing the reader directly.
- Prefer formal written forms over colloquial ones.

**Japanese examples:**

| Avoid | Use instead |
|---|---|
| はい、対応できます。 | 対応可能である。 |
| 結論としては、 | （削除して断定文にする） |
| つまり、 | このため / 問題の本質は / したがって |
| 〜してみましょう | 〜を実施する |
| 〜するとよいでしょう | 〜が適切である / 〜が適する |
| 便利です | 有効である |
| わかりやすいです | 把握しやすい |
| 〜になります | 〜となる / 〜である |

**English examples:**

| Avoid | Use instead |
|---|---|
| Let's try… | To implement… |
| It's easy to… | The approach is… |
| You can just… | This can be achieved by… |
| It's useful | This is effective for… |
| As you can see… | The result shows… |

### 3.2 Headings

- Use noun phrases or action phrases, not questions.
- Avoid Q&A-style headings.

| Avoid | Use instead |
|---|---|
| なぜ起きるのか？ | 原因 |
| 対応できるか？ | 対応方法 |
| Shift 範囲選択にも対応できるか | Shift 範囲選択への対応 |

### 3.3 One Article, One Topic

An article must cover one clearly scoped technical subject. Do not combine multiple independent topics into a single article.

---

## 4. Code Snippets

Every code block must be preceded and followed by explanatory text:

- **Before the code:** state what the code does and why this approach is used.
- **After the code:** describe any constraints, important points, or expected behavior.

An article consisting primarily of code with minimal prose is not acceptable.

---

## 5. Required Content Elements

Each article should include:

- **Prerequisites / Environment** — specify the framework, version, and architecture (e.g., MVVM, code-behind).
- **Explanation of cause or background** — not just "what to do" but "why."
- **Decision criteria** — state conditions under which each approach is appropriate.
- **Limitations** — document what the solution does not handle or where it may break.
- **Summary with recommendations** — conclude with which approach to choose and under what conditions.

---

## 6. AdSense Compatibility

Articles are published with Google AdSense. The following practices are required:

- The article must provide original, substantive content beyond what public documentation already covers.
- Minimum effective length: approximately 700–1,500 characters (Japanese) or 400–900 words (English). Quality and density take priority over word count.
- Include at least one of: comparison table, pitfall/edge case, practical selection guidance.
- Avoid thin content — do not publish an article that merely restates official documentation.

---

## 7. Internal Navigation

Where relevant, include links to:

- Related articles on this site
- The corresponding Japanese or English counterpart article

---

## 8. Titles

Titles must make the technical subject immediately clear.

| Avoid | Use instead |
|---|---|
| DataGrid を便利に使う | WPF DataGrid の並び替えを実装する方法 |
| WPF の小技 | WPF ListBox の仮想化環境における選択状態の管理 |
| Useful Tips for WPF | Implementing Column Sorting in WPF DataGrid |
