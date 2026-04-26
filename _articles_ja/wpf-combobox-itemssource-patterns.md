---
layout: article-ja
title: "WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法"
date: 2026-04-26
category: WPF
excerpt: "ItemsSource に渡すデータ構造によって、DisplayMemberPath・SelectedItem・SelectedValue の設定方法が変わります。文字列リスト、オブジェクトリスト、Enum の各パターンを整理します。"
---

## 概要

WPF の `ComboBox` は `ItemsSource` に渡すデータ構造に応じて、表示内容の制御方法と選択値の取得方法が変わる。具体的には `DisplayMemberPath`、`ItemTemplate`、`SelectedItem`、`SelectedValue`、`SelectedValuePath` の組み合わせが、バインドするコレクションの型によって異なる。本記事では代表的な実装パターンを整理し、それぞれの選択に関するプロパティの使い分けを示す。

---

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12
- 対象コントロール: WPF ComboBox
- アーキテクチャ: MVVM（DataContext 経由のバインド）
- 前提知識: WPF バインド基礎、`INotifyPropertyChanged`

---

## 問題

`ComboBox` の `ItemsSource` に渡すコレクションの要素型が変わると、選択値の取得方法も変わる。たとえば文字列リストを渡す場合と、ID と名称を持つオブジェクトのリストを渡す場合では、ViewModel にバインドするプロパティの型と設定が異なる。この違いを把握していないと、選択しても値が反映されない、または初期値が表示されないといった問題が発生する。

---

## 原因・背景

`ComboBox` の選択関連プロパティは以下の 3 種類がある。

| プロパティ | 返す値 | 主な用途 |
|---|---|---|
| `SelectedItem` | `ItemsSource` の要素そのもの | オブジェクト全体を ViewModel に渡す |
| `SelectedValue` | `SelectedValuePath` で指定したプロパティ値 | ID など特定フィールドだけを取得する |
| `SelectedIndex` | 選択行のインデックス（0 始まり） | 位置だけを管理する場合 |

文字列リストの場合、要素そのものが文字列であるため `SelectedItem` は `string` を返す。オブジェクトのリストを使い `SelectedValuePath` を指定すると、`SelectedValue` には指定プロパティの値が返る。どのプロパティをバインドするかはデータ構造に依存するため、構造ごとに設定を合わせる必要がある。

---

## 解決方法

実装に入る前に、`ItemsSource` の要素型に応じて選択関連プロパティを使い分ける。

- 要素が `string` や `enum` のような単純値なら、要素そのものを受け取る `SelectedItem` を使う。
- 要素がオブジェクトで、選択されたオブジェクト全体を扱いたいなら `SelectedItem` を使う。
- 要素がオブジェクトで、ID など特定の値だけを ViewModel に持たせたいなら `SelectedValue` と `SelectedValuePath` を使う。
- 表示用の名称と保持したい値を分けたい場合は、`DisplayMemberPath` と `SelectedValuePath` を組み合わせる。
- `SelectedIndex` は表示順そのものに意味がある場合に限って使い、通常は値やオブジェクトを直接扱う設定を優先する。

この方針で選べば、ViewModel 側のプロパティ型と `ComboBox` の設定を対応させやすくなり、初期選択や選択変更の反映漏れを防ぎやすい。

---
## 実装例

### パターン A：文字列リスト

ItemsSource が `ObservableCollection<string>` のとき、`DisplayMemberPath` は不要であり、`SelectedItem` に `string` 型のプロパティをバインドするだけで機能する。

```xml
<ComboBox ItemsSource="{Binding Regions}"
          SelectedItem="{Binding SelectedRegion}" />
```

対応する ViewModel 側の実装は次のとおりである。
```csharp
public ObservableCollection<string> Regions { get; } = new()
{
    "東北", "関東", "中部", "近畿", "九州"
};

private string? _selectedRegion;
public string? SelectedRegion
{
    get => _selectedRegion;
    set { _selectedRegion = value; OnPropertyChanged(); }
}
```

`SelectedItem` に `string` 以外の型（例: `int`）をバインドすると型の不一致でバインドエラーとなる。

---

### パターン B：オブジェクトリスト ＋ DisplayMemberPath ＋ SelectedItem

ItemsSource が `ObservableCollection<T>` で、表示する文字列と選択して取得するオブジェクトが同じ型の場合、`DisplayMemberPath` で表示プロパティを指定し、`SelectedItem` にオブジェクト全体をバインドする。

```xml
<ComboBox ItemsSource="{Binding Departments}"
          DisplayMemberPath="Name"
          SelectedItem="{Binding SelectedDepartment}" />
```

```csharp
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public ObservableCollection<Department> Departments { get; } = new()
{
    new Department { Id = 1, Name = "営業部" },
    new Department { Id = 2, Name = "開発部" },
    new Department { Id = 3, Name = "総務部" },
};

private Department? _selectedDepartment;
public Department? SelectedDepartment
{
    get => _selectedDepartment;
    set { _selectedDepartment = value; OnPropertyChanged(); }
}
```

選択後は `SelectedDepartment.Id` や `SelectedDepartment.Name` で任意のフィールドにアクセスできる。ViewModel にオブジェクト全体を保持するため、後から複数フィールドを参照しやすい。

---

### パターン C：オブジェクトリスト ＋ DisplayMemberPath ＋ SelectedValuePath

表示は `Name` で行い、選択値として `Id`（数値や文字列のキー）だけを取得したい場合に使う。`SelectedValuePath` に取得したいプロパティ名を指定し、`SelectedValue` にそのプロパティの型でバインドする。

```xml
<ComboBox ItemsSource="{Binding Departments}"
          DisplayMemberPath="Name"
          SelectedValuePath="Id"
          SelectedValue="{Binding SelectedDepartmentId}" />
```

```csharp
private int _selectedDepartmentId;
public int SelectedDepartmentId
{
    get => _selectedDepartmentId;
    set { _selectedDepartmentId = value; OnPropertyChanged(); }
}
```

`SelectedValue` の型と `SelectedValuePath` で指定するプロパティの型が一致していないと、選択値が反映されない。また `SelectedItem` と `SelectedValue` は同時に利用できるが、一方を変更するともう一方も自動で更新される。

---

### パターン D：ItemTemplate を使ったカスタム表示

1 行の表示に複数フィールドを含めたい場合や、アイコン付きの選択肢を実装したい場合は `ItemTemplate` を使う。`DisplayMemberPath` と `ItemTemplate` は両方を設定できるが、表示には `ItemTemplate` が優先されて `DisplayMemberPath` は無視される。そのため、カスタム表示が必要なときは `ItemTemplate` を使用し、通常は `DisplayMemberPath` を併用しない。

```xml
<ComboBox ItemsSource="{Binding Employees}"
          SelectedItem="{Binding SelectedEmployee}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding Id}" Width="40" Foreground="Gray"/>
                <TextBlock Text="{Binding Name}" />
            </StackPanel>
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

```csharp
public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

`ItemTemplate` を使う場合、ドロップダウンを閉じた状態（選択済み表示）と展開した一覧で異なるレイアウトを出したいときは `ItemTemplate` の代わりに `ItemContainerStyle` と `ContentTemplate` を組み合わせる。

---

### パターン E：Enum リスト

選択肢が固定の列挙体の場合、`ObjectDataProvider` を使って XAML から Enum 値を列挙する方法と、ViewModel でコレクションを用意する方法がある。保守性を考えると ViewModel でコレクションを生成する方法が安全である。

ViewModel 側で `Enum.GetValues` を使って選択肢を生成する。

```csharp
public enum Priority { 低, 中, 高 }

public IEnumerable<Priority> Priorities { get; }
    = (Priority[])Enum.GetValues(typeof(Priority));

private Priority _selectedPriority = Priority.中;
public Priority SelectedPriority
{
    get => _selectedPriority;
    set { _selectedPriority = value; OnPropertyChanged(); }
}
```

```xml
<ComboBox ItemsSource="{Binding Priorities}"
          SelectedItem="{Binding SelectedPriority}" />
```

`SelectedItem` の型は `Priority`（Enum 型）になる。`SelectedValue` と `SelectedValuePath` を使って Enum の数値（基底値）だけを取得することも可能だが、明示的に `(int)SelectedPriority` でキャストした方が意図が明確である。

---

## 注意点

- **`DisplayMemberPath` と `ItemTemplate` の同時指定は無効**  
  両方を指定すると `ItemTemplate` が優先されるが、予期しない表示になる場合がある。いずれか一方のみを使用する。

- **`SelectedValue` の初期値を正しく設定する**  
  `SelectedValuePath` を使う場合、ViewModel 側の初期値が `ItemsSource` 内に存在しない値だと選択状態が空になる。`ItemsSource` がセットされる前に `SelectedValue` が設定されるとバインドが空振りすることがある。`ItemsSource` を先に設定してから選択値を設定する順序を守る。

- **`SelectedItem` の同一性判定は参照比較**  
  `SelectedItem` は参照の同一性で一致を判定する。別インスタンスだが同じ内容のオブジェクトを初期値として設定しても選択状態にならない。`IEquatable<T>` の実装か `SelectedValuePath` を使う方法で回避する。

- **`null` の扱い**  
  選択肢として「未選択」を表す `null` を含める場合、ComboBox は空欄として表示する。ViewModel 側で `null` を許容する型（例: `string?`, `int?`）を使う。

---

## 代替案・比較

| パターン | ItemsSource の型 | 選択値の取得 | 適するケース |
|---|---|---|---|
| A: 文字列リスト | `ObservableCollection<string>` | `SelectedItem`（`string`） | 選択肢が単純なラベルのみ |
| B: オブジェクト ＋ SelectedItem | `ObservableCollection<T>` | `SelectedItem`（`T`） | 選択後に複数フィールドを参照する |
| C: オブジェクト ＋ SelectedValuePath | `ObservableCollection<T>` | `SelectedValue`（指定プロパティ型） | ID など特定フィールドだけ必要 |
| D: ItemTemplate | `ObservableCollection<T>` | `SelectedItem`（`T`） | 複数フィールドを 1 行に表示する |
| E: Enum | `IEnumerable<TEnum>` | `SelectedItem`（`TEnum`） | 固定の列挙値から選択する |

---

## まとめ

`ComboBox` の実装パターンは `ItemsSource` に渡す型によって決まる。

- 文字列リストのみの場合は `SelectedItem` に `string` 型をバインドするだけで十分である。
- オブジェクトリストでオブジェクト全体が必要な場合は `DisplayMemberPath` ＋ `SelectedItem` を使う。
- オブジェクトリストで特定フィールド（ID など）だけ必要な場合は `SelectedValuePath` ＋ `SelectedValue` を使う。
- カスタムレイアウトが必要な場合は `ItemTemplate` を使い、`DisplayMemberPath` との同時指定は避ける。
- Enum は ViewModel でコレクション化し、`SelectedItem` に Enum 型をバインドする。

初期値が反映されない問題の多くは、参照比較の不一致か `ItemsSource` のセット順序に起因する。`SelectedValuePath` を使うか、同一インスタンスを参照するよう設計することで回避できる。

---

<!-- 関連記事 -->
<!-- - [WPF DataGrid の並び替えを実装する方法](/articles/wpf-datagrid-sorting) -->
