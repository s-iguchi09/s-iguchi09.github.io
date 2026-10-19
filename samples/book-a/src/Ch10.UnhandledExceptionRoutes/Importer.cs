namespace Ch10.UnhandledExceptionRoutes;

/// <summary>取り込みの処理。サンプルでは、取り込み元が見つからずに必ず失敗する。</summary>
public static class Importer
{
    public const string FailureMessage = "取り込み元のファイルが見つからない。";

    public static void Import() => throw new InvalidOperationException(FailureMessage);
}
