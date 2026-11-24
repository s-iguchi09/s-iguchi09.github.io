using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;

// 記事「wpf-ivalueconverter-visibility-enum-display-name」のシーン（ValueConverterScene）が XAML から参照する型。
// XamlReader で読むため public にし、ほかのシーンの型と名前がぶつからないよう名前空間を分ける。
namespace ScreenshotCapture.Scenes.ValueConverter;

/// <summary>ビューモデルの最小の基底。</summary>
public abstract class ObservableObjectForConverters : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>記事の列挙型。Shipped には Description 属性を付けない。</summary>
public enum OrderStatus
{
    [Description("Awaiting payment")]
    AwaitingPayment,

    [Description("In progress")]
    InProgress,

    Shipped,
}

/// <summary>記事のビューモデル。</summary>
public sealed class OrderViewModel : ObservableObjectForConverters
{
    private bool _isBusy;
    private OrderStatus _status;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public OrderStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public IReadOnlyList<OrderStatus> Statuses { get; } = Enum.GetValues<OrderStatus>();

    public string StatusText => EnumDescriptionConverter.Describe(Status);
}

/// <summary>StatusText の変更を通知しない、記事の落とし穴のビューモデル。</summary>
public sealed class SilentOrderViewModel : ObservableObjectForConverters
{
    private OrderStatus _status;

    public OrderStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusText => EnumDescriptionConverter.Describe(Status);
}

/// <summary>どんな値でも入れられるソース。null や定義の無い列挙値をコンバーターに渡すために使う。</summary>
public sealed class ValueHolder : ObservableObjectForConverters
{
    private object? _value;

    public object? Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>記事の BoolToVisibilityConverter。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public Visibility TrueValue { get; set; } = Visibility.Visible;

    public Visibility FalseValue { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>ConverterParameter で反転を指定する、記事の落とし穴の実装。</summary>
public sealed class ParameterBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value is true;
        if (parameter is true)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>ConverterParameter を文字列の "True" と比べる、記事の直し方の実装。</summary>
public sealed class StringParameterBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value is true;
        if (string.Equals(parameter as string, "True", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>記事の EnumDescriptionConverter。</summary>
public sealed class EnumDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum e ? Describe(e) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    public static string Describe(Enum value)
    {
        string name = value.ToString();
        FieldInfo? field = value.GetType().GetField(name);
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }
}

/// <summary>GetField と GetCustomAttribute の結果を確かめない、記事の落とし穴の実装。</summary>
public sealed class UncheckedEnumDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value!.GetType().GetField(value.ToString()!)!.GetCustomAttribute<DescriptionAttribute>()!.Description;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>受け取った parameter の型と値を「型|値」の文字列で返す計測用のコンバーター。</summary>
public sealed class ParameterRecorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter is null ? "null|null" : $"{parameter.GetType().FullName}|{parameter}";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
