namespace Ch11.TaskResultDeadlock;

/// <summary>
/// データの読み込み。I/O の代わりに Task.Delay(300) を待つ。実際のアプリでは HttpClient やデータベースの呼び出しがこの位置に来る。
/// await に ConfigureAwait(false) を付けていないので、await の後は呼び出し元のコンテキスト（UI スレッド）で再開しようとする。
/// </summary>
public static class DataSource
{
    public static async Task<int> LoadAsync()
    {
        await Task.Delay(300);
        return 42;
    }
}
