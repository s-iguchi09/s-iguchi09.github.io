using System.ComponentModel;
using BookA.Mvvm;

namespace Ch03.DataGridCommitOnSave;

/// <summary>
/// DataGrid の 1 行。IEditableObject を実装し、行の編集を取り消したときに編集前の値へ戻せるようにする。
/// </summary>
public sealed class ItemViewModel : ObservableObject, IEditableObject
{
    private string _name;
    private int _quantity;
    private (string Name, int Quantity)? _backup;

    public ItemViewModel(string name, int quantity)
    {
        _name = name;
        _quantity = quantity;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    /// <summary>行の編集が始まった。編集前の値を控える。</summary>
    public void BeginEdit() => _backup ??= (Name, Quantity);

    /// <summary>行の編集が確定した。控えた値を捨てる。</summary>
    public void EndEdit() => _backup = null;

    /// <summary>行の編集が取り消された。控えた値に戻す。</summary>
    public void CancelEdit()
    {
        if (_backup is { } backup)
        {
            Name = backup.Name;
            Quantity = backup.Quantity;
            _backup = null;
        }
    }
}
