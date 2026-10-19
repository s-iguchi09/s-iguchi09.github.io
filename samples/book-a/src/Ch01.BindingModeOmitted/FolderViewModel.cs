using BookA.Mvvm;

namespace Ch01.BindingModeOmitted;

/// <summary>
/// フォルダーの木の 1 ノード。開閉の状態をビューモデルが持つ。
/// </summary>
public sealed class FolderViewModel : ObservableObject
{
    private bool _isExpanded;

    public FolderViewModel(string name, params FolderViewModel[] children)
    {
        Name = name;
        Children = children;
    }

    public string Name { get; }

    public IReadOnlyList<FolderViewModel> Children { get; }

    /// <summary>ノードが開いているか。TreeViewItem.IsExpanded にバインドする。</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>
    /// このノードと子をすべて閉じる。
    ///
    /// 値が既に false でも変更を通知する。ビューの表示がビューモデルとずれていても（片方向のバインドで
    /// ユーザーが開いた後など）、通知を受けたバインドが表示をビューモデルの値に合わせ直すためである。
    /// </summary>
    public void Collapse()
    {
        _isExpanded = false;
        OnPropertyChanged(nameof(IsExpanded));
        foreach (FolderViewModel child in Children)
        {
            child.Collapse();
        }
    }
}
