using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ScreenshotCapture.Scenes.DataGridSortReset;

/// <summary>
/// 記事「WPFのDataGridのソートを初期化する方法」の <c>TriStateSort</c> と同じコード。
/// 記事のコードを変えたら、ここも同じに変える。
/// </summary>
public static class TriStateSort
{
    // DataGrid の Sorting イベントから呼ぶ。降順の列をもう一度クリックしたときだけ、標準の処理の代わりに解除する
    public static void OnSorting(DataGrid dataGrid, DataGridSortingEventArgs e)
    {
        if (e.Column.SortDirection != ListSortDirection.Descending) return;

        e.Handled = true; // 標準処理をキャンセル

        SortDescriptionCollection sorts = dataGrid.Items.SortDescriptions;
        int groupingCount = CountGroupingSorts(dataGrid.Items);

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            // Shift + クリック: 対象列の条件だけを外す。他の列の条件と矢印は残る
            for (int i = groupingCount; i < sorts.Count; i++)
            {
                if (sorts[i].PropertyName == e.Column.SortMemberPath)
                {
                    sorts.RemoveAt(i);
                    break;
                }
            }
        }
        else
        {
            // 通常のクリック: 他の列の矢印は、DataGrid がこのイベントの前に消している。
            // 矢印の無い条件が残らないよう、グループ化用を除く条件をすべて外す
            while (sorts.Count > groupingCount)
            {
                sorts.RemoveAt(sorts.Count - 1);
            }
        }

        e.Column.SortDirection = null;
    }

    // グループ化していると、DataGrid は並び替えの条件の先頭に、グループ化用の昇順の条件を入れる。その数を数える
    private static int CountGroupingSorts(ItemCollection items)
    {
        int count = 0;
        foreach (GroupDescription group in items.GroupDescriptions)
        {
            if (group is not PropertyGroupDescription property
                || property.Converter != null
                || property.StringComparison != StringComparison.Ordinal)
            {
                continue;
            }

            if (count < items.SortDescriptions.Count
                && items.SortDescriptions[count].PropertyName == property.PropertyName
                && items.SortDescriptions[count].Direction == ListSortDirection.Ascending)
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }
}

/// <summary>
/// 記事の以前の版の Sorting ハンドラー。対象列と同じ PropertyName の最初の条件だけを外す。
/// 改めた版との違いを測るために残す。
/// </summary>
public static class FirstMatchSort
{
    public static void OnSorting(DataGrid dataGrid, DataGridSortingEventArgs e)
    {
        if (e.Column.SortDirection == ListSortDirection.Descending)
        {
            e.Handled = true;

            var target = dataGrid.Items.SortDescriptions
                .FirstOrDefault(sd => sd.PropertyName == e.Column.SortMemberPath);

            if (!string.IsNullOrEmpty(target.PropertyName))
            {
                dataGrid.Items.SortDescriptions.Remove(target);
            }

            e.Column.SortDirection = null;
            dataGrid.Items.Refresh();
        }
    }
}
