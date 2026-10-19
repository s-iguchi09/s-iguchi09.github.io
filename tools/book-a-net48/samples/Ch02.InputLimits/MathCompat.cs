namespace Ch02.InputLimits;

/// <summary>
/// 第 2 章のサンプル（CustomerViewModel）は .NET Core 2.0 以降の <c>Math.Clamp</c> を使う。
/// サンプルのソースを書き換えずに .NET Framework 4.8 で測るため、同じ名前空間に同名の型を置き、
/// サンプルが使う 2 つのメソッドを .NET 10 と同じ意味で用意する（名前空間内の型が using の System.Math より優先される）。
/// </summary>
internal static class Math
{
    /// <summary>.NET の Math.Clamp と同じく、min 未満は min、max 超は max、それ以外はそのまま返す。</summary>
    public static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    public static double Round(double value, MidpointRounding mode) => System.Math.Round(value, mode);
}
