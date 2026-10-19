using System.Windows;
using System.Windows.Controls;

namespace Ch03.DataGridCommitOnSave;

/// <summary>
/// DataGrid に付ける添付プロパティ。XAML で <c>local:PendingEdits.Host="{Binding}"</c> と書くと、
/// その DataGrid の行を確定する処理をビューモデル（<see cref="IPendingEditsHost"/>）に渡す。
///
/// 記事の実装はこれをウィンドウのコードビハインドで渡していた。添付プロパティにすると、
/// コードビハインドを空のまま保ち、DataContext が替わったときにも渡し直せる。
/// </summary>
public static class PendingEdits
{
    public static readonly DependencyProperty HostProperty = DependencyProperty.RegisterAttached(
        "Host",
        typeof(IPendingEditsHost),
        typeof(PendingEdits),
        new PropertyMetadata(null, OnHostChanged));

    public static IPendingEditsHost? GetHost(DataGrid grid) => (IPendingEditsHost?)grid.GetValue(HostProperty);

    public static void SetHost(DataGrid grid, IPendingEditsHost? value) => grid.SetValue(HostProperty, value);

    private static void OnHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            throw new InvalidOperationException("PendingEdits.Host は DataGrid にだけ付けられる。");
        }

        if (e.OldValue is IPendingEditsHost oldHost)
        {
            oldHost.CommitPendingEdits = null;
        }

        if (e.NewValue is IPendingEditsHost newHost)
        {
            // セルではなく行を確定する。セルの編集中に引数なしの CommitEdit() を呼んでも、行は確定せずソースに届かない（第二の壁）。
            newHost.CommitPendingEdits = () => grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}
