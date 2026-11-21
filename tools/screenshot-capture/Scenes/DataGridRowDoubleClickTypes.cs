using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using BookA.Mvvm;

// 記事「wpf-datagrid-row-doubleclick-command-mvvm」のシーン（DataGridRowDoubleClickScene）が XAML から参照する型。
// XamlReader で読むため public にし、ほかのシーンの型と名前がぶつからないよう名前空間を分ける。
namespace ScreenshotCapture.Scenes.DataGridRowDoubleClick;

/// <summary>一覧に並ぶ顧客。</summary>
public sealed class Customer(string name, string city)
{
    public string Name { get; set; } = name;

    public string City { get; set; } = city;
}

/// <summary>記事のビューモデル。OpenCommand で詳細を開く（計測では、開いた顧客の名前を記録する）。</summary>
public sealed class CustomersViewModel : ObservableObject
{
    public CustomersViewModel(IEnumerable<Customer> customers)
    {
        Customers = new ObservableCollection<Customer>(customers);
        OpenCommand = new RelayCommand<Customer>(Open);
    }

    public ObservableCollection<Customer> Customers { get; }

    public ICommand OpenCommand { get; }

    /// <summary>OpenCommand が実行されるたびに、渡された顧客の名前を足す。</summary>
    public List<string> Opened { get; } = [];

    private void Open(Customer customer) => Opened.Add(customer.Name);
}

/// <summary>
/// 記事の添付プロパティ。DataGridRow に付けると、その行の MouseDoubleClick で Command を実行する。
/// 渡すのは行のアイテム（DataGridRow.Item）で、SelectedItem ではない。
/// </summary>
public static class RowDoubleClick
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(RowDoubleClick),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGridRow row)
        {
            return;
        }

        row.MouseDoubleClick -= OnMouseDoubleClick;
        if (e.NewValue is not null)
        {
            row.MouseDoubleClick += OnMouseDoubleClick;
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // MouseDoubleClick は右ボタンのダブルクリックでも発生するため、左ボタンだけを拾う。
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var row = (DataGridRow)sender;
        ICommand? command = GetCommand(row);
        object item = row.Item;
        if (command?.CanExecute(item) == true)
        {
            command.Execute(item);
        }
    }
}

/// <summary>
/// 記事のコンバーター。InvokeCommandAction の EventArgsConverter に指定し、
/// ダブルクリックされた要素から DataGridRow をさかのぼって、その行のアイテムを返す。行の外なら null を返す。
/// </summary>
public sealed class RowItemFromEventArgsConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // MouseDoubleClick は右ボタンのダブルクリックでも発生するため、左ボタン以外は null（実行しない）にする。
        if (value is not MouseButtonEventArgs { ChangedButton: MouseButton.Left } args)
        {
            return null;
        }

        var current = args.OriginalSource as DependencyObject;
        while (current is not null and not DataGridRow)
        {
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return (current as DataGridRow)?.Item;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
