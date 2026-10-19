using System.Collections.ObjectModel;
using System.Windows;
using BookA.Mvvm;

namespace Ch01.BindingModeOmitted;

/// <summary>
/// フォルダーの木と、左の列の幅を持つ画面のビューモデル。再現版と修正版のビューで共通に使う。
/// 両者の違いはビューの XAML（バインドに Mode=TwoWay を書くかどうか）だけである。
/// </summary>
public sealed class FolderBrowserViewModel : ObservableObject
{
    /// <summary>左の列の幅の初期値。</summary>
    public static readonly GridLength DefaultNavigationWidth = new(100);

    private GridLength _navigationWidth = DefaultNavigationWidth;

    public FolderBrowserViewModel()
    {
        Folders =
        [
            new FolderViewModel("Documents", new FolderViewModel("Reports"), new FolderViewModel("Drafts")),
            new FolderViewModel("Pictures", new FolderViewModel("2026")),
        ];
        CollapseAllCommand = new RelayCommand(CollapseAll);
        ResetWidthCommand = new RelayCommand(() => NavigationWidth = DefaultNavigationWidth);
    }

    public ObservableCollection<FolderViewModel> Folders { get; }

    /// <summary>
    /// 左の列の幅。ColumnDefinition.Width にバインドするため、型は GridLength にする。
    /// </summary>
    public GridLength NavigationWidth
    {
        get => _navigationWidth;
        set => SetProperty(ref _navigationWidth, value);
    }

    /// <summary>すべてのノードをビューモデルから閉じる。</summary>
    public RelayCommand CollapseAllCommand { get; }

    /// <summary>左の列の幅をビューモデルから初期値に戻す。</summary>
    public RelayCommand ResetWidthCommand { get; }

    private void CollapseAll()
    {
        foreach (FolderViewModel folder in Folders)
        {
            folder.Collapse();
        }
    }
}
