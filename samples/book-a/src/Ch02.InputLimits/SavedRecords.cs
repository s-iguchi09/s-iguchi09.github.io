namespace Ch02.InputLimits;

/// <summary>保存済みのデータ。旧版で保存した、今の制限に合わない値を想定する。</summary>
public sealed record SavedRecord(string Code, double Volume);

/// <summary>サンプルで読み込む保存済みのデータ。</summary>
public static class SavedRecords
{
    /// <summary>顧客コードが 5 文字を超え、音量が 100 を超えている。</summary>
    public static SavedRecord OverLimit { get; } = new("abcdefgh", 150);

    /// <summary>音量が 10 刻みに合っていない。</summary>
    public static SavedRecord OffStep { get; } = new("abc", 23.4);
}
