using System.Windows;

namespace Ch08.UserControlDependencyProperty;

/// <summary>
/// 修正版のカード。Title は既定で双方向にバインドされるよう登録する（中の TextBox の編集を利用側へ戻すため）。
/// 依存関係プロパティの宣言は、再利用する部品（コントロール）としてのコードで、画面の処理ではない。
/// </summary>
public partial class FixedInfoCard
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(FixedInfoCard),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public FixedInfoCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
