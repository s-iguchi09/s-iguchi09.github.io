namespace ScreenshotCapture;

/// <summary>
/// 英語で組み立てた表を、言い回しの対訳で日英の組（<see cref="Loc"/>）にする。
/// 共有の計測や子プロセスの出力のように、行を英語の文字列で返すものを、計測のコードに手を入れずに
/// 日本語の表へ出すときに使う。対訳は前から順に部分一致で置き換えるので、長い言い回しを先に置く。
/// 計測したデータそのもの（Tag や Title に入れた文字列など）に当たらない、具体的な言い回しだけを書く。
/// </summary>
internal static class LocTable
{
    public static List<IReadOnlyList<Loc>> Translate(IEnumerable<IReadOnlyList<string>> rows, IReadOnlyList<(string En, string Ja)> words) =>
        rows.Select(row => (IReadOnlyList<Loc>)row.Select(cell => Translate(cell, words)).ToList()).ToList();

    public static Loc Translate(string text, IReadOnlyList<(string En, string Ja)> words) =>
        Loc.Of(text, words.Aggregate(text, (current, word) => current.Replace(word.En, word.Ja, StringComparison.Ordinal)));
}
