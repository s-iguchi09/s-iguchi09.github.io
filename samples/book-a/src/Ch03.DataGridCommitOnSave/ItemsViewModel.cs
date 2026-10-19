using System.Collections.ObjectModel;
using BookA.Mvvm;

namespace Ch03.DataGridCommitOnSave;

/// <summary>
/// 一覧を編集して保存する画面のビューモデル。再現版と修正版で共通に使う。
/// 違いは、ビューが <see cref="CommitPendingEdits"/> を設定するかどうか（修正版のビューだけが PendingEdits.Host で設定する）。
/// </summary>
public sealed class ItemsViewModel : ObservableObject, IPendingEditsHost
{
    private string _lastSaved = "（まだ保存していない）";
    private string _statusMessage = "";

    public ItemsViewModel()
    {
        Items = [new ItemViewModel("alpha", 1), new ItemViewModel("beta", 2)];
        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<ItemViewModel> Items { get; }

    /// <summary>メニュー・ツールバー・ツールバーの外のボタン・Ctrl+S のすべてから呼ぶ保存のコマンド。</summary>
    public RelayCommand SaveCommand { get; }

    /// <inheritdoc />
    public Func<bool>? CommitPendingEdits { get; set; }

    /// <summary>最後に保存した 1 行目の値。保存処理が読んだ値を画面で確かめるために出す。</summary>
    public string LastSaved
    {
        get => _lastSaved;
        private set => SetProperty(ref _lastSaved, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void Save()
    {
        // 確定はコマンドの中で行う。どのコントロールから保存しても、フォーカスの動きに左右されずに同じ結果になる。
        if (CommitPendingEdits is not null && !CommitPendingEdits())
        {
            StatusMessage = "入力に誤りがある行は保存できない。";
            return;
        }

        StatusMessage = "";
        ItemViewModel first = Items[0];
        LastSaved = $"Name = {first.Name}、Quantity = {first.Quantity}";
    }
}
