using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenshotCapture;

/// <summary>
/// 図に使うサンプルデータ。図は日英で共有するため、値は言語に依存しないものにする。
/// </summary>
internal static class SampleData
{
    // 元の並びが名前の昇順・降順のどちらとも一致しないようにしている。
    // 一致していると、未ソート状態の図がソート済みの図と見分けられなくなる。
    public static ObservableCollection<Product> Products() =>
    [
        new Product { Name = "Monitor", Price = 21800, Category = "Display" },
        new Product { Name = "Mouse", Price = 2480, Category = "Input" },
        new Product { Name = "Keyboard", Price = 4980, Category = "Input" },
    ];

    public static string[] Categories() => ["Input", "Display", "Audio"];
}

/// <summary>
/// 記事の例と同じく、編集用の選択肢を DataGrid の DataContext 側に持たせるための型。
/// </summary>
public sealed class ProductListContext
{
    public IReadOnlyList<string> Categories { get; } = SampleData.Categories();
}

/// <summary>
/// バインディングのタイミングを示す図で使う ViewModel。
/// </summary>
public sealed class UserNameViewModel : INotifyPropertyChanged
{
    private string _userName = "suzuki";

    public string UserName
    {
        get => _userName;
        set
        {
            if (_userName == value)
            {
                return;
            }

            _userName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserName)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// 書式化の図で使う値。記事の例に合わせた価格と日付を持つ。
/// </summary>
public sealed class FormatSample
{
    public decimal Price { get; } = 1234.5m;

    public DateTime OrderDate { get; } = new(2026, 7, 17);
}

internal sealed class Product : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private int _price;
    private string _category = string.Empty;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public int Price
    {
        get => _price;
        set => Set(ref _price, value);
    }

    public string Category
    {
        get => _category;
        set => Set(ref _category, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
