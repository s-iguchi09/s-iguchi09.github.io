using BookA.Mvvm;

namespace Ch08.UserControlDependencyProperty;

/// <summary>
/// カードを置く画面のビューモデル。カードの見出し（Title）にバインドする文字列を持つ。
/// 再現版と修正版で共通に使い、違いはカード（UserControl）の中の XAML だけである。
/// </summary>
public sealed class PageViewModel : ObservableObject
{
    private string _headerText = "from PageViewModel";

    public string HeaderText
    {
        get => _headerText;
        set => SetProperty(ref _headerText, value);
    }
}
