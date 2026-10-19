using System.Windows;

namespace Ch08.UserControlDependencyProperty;

/// <summary>
/// 再現版のカード。見出しを外から受け取る依存関係プロパティ Title を持つ。
/// 依存関係プロパティの宣言は、再利用する部品（コントロール）としてのコードで、画面の処理ではない。
/// </summary>
public partial class ReproInfoCard
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ReproInfoCard), new PropertyMetadata(string.Empty));

    public ReproInfoCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
