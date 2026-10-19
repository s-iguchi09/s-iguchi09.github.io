using BookA.Mvvm;

namespace Ch09.StaticResourceNotUpdating;

/// <summary>
/// テーマ（明るい・暗い）を持つビューモデル。色そのものはビューのリソースの仕事なので、ここでは状態だけを持つ。
/// 再現版と修正版で共通に使い、違いはビューがリソースを StaticResource と DynamicResource のどちらで参照するかだけである。
/// </summary>
public sealed class ThemeViewModel : ObservableObject
{
    private bool _isDark;

    public ThemeViewModel()
    {
        ToggleThemeCommand = new RelayCommand(() => IsDark = !IsDark);
    }

    public bool IsDark
    {
        get => _isDark;
        set => SetProperty(ref _isDark, value);
    }

    public RelayCommand ToggleThemeCommand { get; }
}
