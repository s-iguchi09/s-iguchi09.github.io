---
layout: article-ja
title: "INotifyPropertyChanged を CallerMemberName で簡潔に実装する方法"
date: 2026-07-16
category: C#
excerpt: "INotifyPropertyChanged の実装でプロパティ名を文字列で渡す冗長さを、CallerMemberName 属性で解消する方法を整理する。nameof との違い、SetProperty ヘルパー、算出プロパティ通知の限界まで実装例とともに解説する。"
---

## 概要

`INotifyPropertyChanged` は WPF・MVVM でビューモデルの変更をビューへ通知するための標準インターフェイスである。
実装自体は `PropertyChanged` イベントを発行するだけだが、素朴に書くとプロパティ名を文字列リテラルで渡すことになり、記述が冗長でタイプミスにも弱い。
本記事では、`CallerMemberName` 属性を用いて通知処理を簡潔にする手法を、従来の書き方との比較を交えて整理する。
`nameof` 演算子との使い分け、比較・代入・通知を 1 行にまとめる `SetProperty` ヘルパー、そして算出プロパティ通知における限界までを扱う。

---

## 前提・対象環境

- フレームワーク／言語: .NET Framework 4.5 以降 / .NET Core / .NET 5 以降、C# 5.0 以降
- 対象機能: `System.ComponentModel.INotifyPropertyChanged`
- アーキテクチャ: MVVM（ビューモデルでの変更通知）
- 使用する属性: `System.Runtime.CompilerServices.CallerMemberName`

`CallerMemberName` 属性は C# 5.0（.NET Framework 4.5）で追加された。
そのため、それ以前の環境では後述の文字列リテラルまたは式木を用いる手法に限られる。

---

## 文字列リテラルによる実装と課題

最初に、`CallerMemberName` を使わない素朴な実装を確認する。
以下は `PropertyChanged` を発行する基底クラスと、その利用側のプロパティである。

```csharp
using System.ComponentModel;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged("Name");
        }
    }
}
```

`OnPropertyChanged("Name")` のようにプロパティ名を文字列で直接指定している。
この方式はコンパイル時に名前の正しさを検証できないため、`"Nmae"` のようなタイプミスがあっても実行時まで気付けない。
さらにプロパティ名をリネームしても文字列は自動追従せず、通知が無言で壊れる原因になる。

---

## nameof 演算子による実装

C# 6.0 で追加された `nameof` 演算子を使うと、文字列リテラルをコンパイル時に検証されるシンボル参照へ置き換えられる。
プロパティ名を変更した場合も、リネーム操作が `nameof` の対象に追従する。

```csharp
public string Name
{
    get => _name;
    set
    {
        _name = value;
        OnPropertyChanged(nameof(Name));
    }
}
```

`nameof(Name)` はコンパイル時に文字列 `"Name"` へ展開されるため、実行時のリフレクションは発生しない。
タイプミスや存在しない名前はコンパイルエラーになり、リネームにも追従する点が文字列リテラルより優れている。
ただし、プロパティを設定するたびに `nameof(自身のプロパティ名)` を明示的に記述する必要は残る。

---

## CallerMemberName による通知

`CallerMemberName` 属性を発行メソッドの省略可能引数に付与すると、呼び出し元のメンバー名がコンパイル時に補完される。
プロパティのセッター内から引数なしで呼び出すだけで、そのプロパティ名が自動的に渡る。

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }
}
```

`OnPropertyChanged()` を引数なしで呼ぶと、コンパイラがセッターのプロパティ名 `"Name"` を実引数として埋め込む。
補完はコンパイル時に確定するため、`nameof` と同様に実行時のリフレクションは発生せず、性能面のコストもない。
プロパティ側の記述量が減り、名前の重複記述が不要になる点が最大の利点である。

---

## SetProperty ヘルパーによる集約

実務では、値が変化したときだけ通知する冗長判定を含めることが多い。
`CallerMemberName` を比較・代入・通知をまとめたヘルパーメソッドに適用すると、セッターの実装をさらに短くできる。

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class PersonViewModel : ViewModelBase
{
    private string _firstName;
    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }
}
```

この `ViewModelBase` は `OnPropertyChanged` と `SetProperty` の両方を持ち、以降の節でもそのまま利用できる完成形である。
`SetProperty` は現在値と新しい値を `EqualityComparer<T>.Default` で比較し、等しければ通知せず `false` を返す。
値が変化した場合のみフィールドを更新して `OnPropertyChanged` を呼び、`true` を返す。
戻り値の `bool` は、後述する依存プロパティの追加通知を条件分岐で行う際に利用できる。
この形は `CommunityToolkit.Mvvm` の `ObservableObject` などが提供する `SetProperty` と同じ考え方であり、自作の基底クラスでも容易に再現できる。

---

## 算出プロパティへの変更通知

`CallerMemberName` が補完するのは、あくまで呼び出し元自身のメンバー名である。
別のプロパティ（他のプロパティから算出される計算プロパティなど）へ通知する場合は、その名前を明示的に渡す必要がある。
なお、ここでの「算出プロパティ」は他プロパティから値を導出する読み取り専用プロパティを指し、WPF の依存関係プロパティ（`DependencyProperty`）とは別概念である。

```csharp
public class PersonViewModel : ViewModelBase
{
    private string _firstName;
    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    public string FullName => $"{FirstName} {LastName}";

    private string _lastName;
    public string LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }
}
```

`FullName` は `FirstName` と `LastName` から算出されるため、いずれかが変わったら `FullName` の変更も通知する必要がある。
この通知には `CallerMemberName` を利用できないため、`nameof(FullName)` で対象名を明示する。
`CallerMemberName` と `nameof` は排他ではなく、自身の通知は `CallerMemberName`、算出プロパティの通知は `nameof` と、役割を分けて併用するのが実務上の定石である。

---

## 注意点

- `CallerMemberName` が有効なのは省略可能引数を持つメソッドに限られ、呼び出し側で引数を明示するとその値が優先される。
- 補完される名前は呼び出し箇所を包含するメンバー名である。セッター外（フィールド初期化子や、別メンバーへ渡したデリゲート内など）で評価されると、意図したプロパティ名にならない。
- 算出プロパティやインデクサーへの通知は自動化されないため、依存関係は自前で管理する。
- 全プロパティを一括で無効化したい場合は、`PropertyChangedEventArgs` に空文字列または `null` を渡す挙動を利用するが、これは `CallerMemberName` の補完とは別の明示的な指定になる。

---

## まとめ

`INotifyPropertyChanged` の通知は、実装対象と C# バージョンに応じて手法を選ぶ。

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| 文字列リテラル | 実装が単純 | タイプミスを検出できず、リネームに追従しない | 非推奨（C# 5.0 未満の互換目的のみ） |
| nameof 演算子 | コンパイル時検証・リネーム追従 | 呼び出しごとに名前の記述が必要 | 算出プロパティなど他プロパティへの通知 |
| CallerMemberName | 呼び出し側の記述不要・リフレクション不要 | 自身のメンバー名しか補完できない | セッターからの通常の通知 |
| SetProperty ヘルパー | 比較・代入・通知を 1 行に集約 | 基底クラスの用意が必要 | MVVM 全般の標準実装 |

C# 5.0 以降であれば、通常のプロパティ通知は `CallerMemberName` を用いた `SetProperty` ヘルパーへ集約するのが最も簡潔で保守しやすい。
一方、算出プロパティへの通知だけは `CallerMemberName` で自動化できないため、`nameof` を併用して対象名を明示する。
この 2 つを役割で使い分けることで、記述量を抑えつつリネームにも強い変更通知を実現できる。
