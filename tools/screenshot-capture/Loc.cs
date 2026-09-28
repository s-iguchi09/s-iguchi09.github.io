namespace ScreenshotCapture;

/// <summary>
/// 表のセルや見出しの、英語と日本語の組。
///
/// 記事は日英で別の表を include するため、見出しと説明の言葉は記事の言語で書く。
/// 識別子や値だけのセル（<c>MaxLength=5</c>、<c>"abcde"</c>、<c>2026-04-05</c> など）は
/// 文字列から暗黙に変換でき、両方の言語で同じ文字になる。
/// SVG の表は英語（<see cref="En"/>）で描く。
/// </summary>
internal readonly record struct Loc(string En, string Ja)
{
    public static implicit operator Loc(string text) => new(text, text);

    /// <summary>英語と日本語の組を作る。</summary>
    public static Loc Of(string en, string ja) => new(en, ja);

    /// <summary>指定した言語（<c>en</c> / <c>ja</c>）の文字。</summary>
    public string In(string language) => language == "ja" ? Ja : En;

    public override string ToString() => En;
}
