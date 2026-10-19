using System.Windows;
using System.Windows.Media;

namespace Ch09.StaticResourceNotUpdating;

/// <summary>
/// 要素に付ける添付プロパティ。<c>local:ThemeResources.IsDark="{Binding IsDark}"</c> と書くと、
/// 値が変わるたびに、その要素の Resources の "ThemeColor" を明るい色・暗い色のブラシに差し替える。
///
/// 記事の実装例はこれをボタンの Click ハンドラーで行っていた。添付プロパティにすると、
/// テーマの状態はビューモデルが持ち、リソースの差し替え（ビューの仕事）はビューの部品が行う形に分けられる。
/// </summary>
public static class ThemeResources
{
    /// <summary>差し替えるリソースのキー。</summary>
    public const string ThemeColorKey = "ThemeColor";

    public static readonly Color LightColor = Colors.SkyBlue;

    public static readonly Color DarkColor = Colors.DarkSlateGray;

    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.RegisterAttached(
        "IsDark",
        typeof(bool),
        typeof(ThemeResources),
        new PropertyMetadata(false, OnIsDarkChanged));

    public static bool GetIsDark(FrameworkElement element) => (bool)element.GetValue(IsDarkProperty);

    public static void SetIsDark(FrameworkElement element, bool value) => element.SetValue(IsDarkProperty, value);

    private static void OnIsDarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            // エントリそのものを新しいブラシに差し替える。参照している側が追随するかは、参照の仕方で決まる。
            element.Resources[ThemeColorKey] = new SolidColorBrush((bool)e.NewValue ? DarkColor : LightColor);
        }
    }
}
