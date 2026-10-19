namespace Ch03.DataGridCommitOnSave;

/// <summary>
/// 保存の前に、ビューの編集中の値を確定する処理を受け取るビューモデル。
/// ビューモデルは DataGrid を知らず、「確定して、できたかどうかを返す関数」だけを受け取る。
/// </summary>
public interface IPendingEditsHost
{
    /// <summary>ビューの編集中の値を確定する。確定できなければ false を返す。ビューが設定しなければ null。</summary>
    Func<bool>? CommitPendingEdits { get; set; }
}
