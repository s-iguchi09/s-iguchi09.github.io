namespace Ch07.DataTemplateParentBinding;

/// <summary>
/// 一覧の 1 行。行は削除のコマンドも単位も持たない（どちらも親のビューモデルにある）。
///
/// record にしないのは、ObservableCollection.Remove が「等しい最初の要素」を消すためである。
/// 値で比べる record だと、同じ内容の行が 2 つあるとき、クリックした行ではなく先の行が消える。
/// </summary>
public sealed class MeasurementViewModel
{
    public MeasurementViewModel(string name, int value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public int Value { get; }
}
