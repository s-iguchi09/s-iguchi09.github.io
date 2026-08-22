using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で RadioButton を enum にバインドすると選択が反映されない問題」の図。
/// 2 つの列挙体のグループを同じ親に並べたとき、GroupName の有無で初期選択の表示が
/// どう変わるかを示す。ViewModel の値は両方とも同じで、違うのは GroupName だけである。
/// </summary>
internal sealed class RadioButtonEnumBindingScene : IScene
{
    private const string LocalNamespace = "clr-namespace:ScreenshotCapture.Scenes;assembly=ScreenshotCapture";

    public string Slug => "wpf-radiobutton-enum-binding";

    public Task CaptureAsync(SceneContext context)
    {
        Window window = DemoLayout.BuildPanelWindow(
            "RadioButton + enum",
            [
                new DemoLayout.Panel("GroupName = \"\"", BuildGroup(withGroupName: false)),
                new DemoLayout.Panel("GroupName = \"quality\" / \"pageLayout\"", BuildGroup(withGroupName: true)),
            ]);

        return context.ShootAsync(window, "radiobutton-enum-groupname.png");
    }

    /// <summary>
    /// 記事に載せた XAML と同じ構成でパネルを組む。GroupName 属性の有無だけが 2 つの差である。
    /// </summary>
    private static UIElement BuildGroup(bool withGroupName)
    {
        // XAML と補間文字列はどちらも波かっこを使うため、GroupName の差し替えは
        // 補間ではなくプレースホルダーの置換で行う。
        const string Template =
            """
            <StackPanel>
              <StackPanel.Resources>
                <local:EnumToBooleanConverter x:Key="EnumToBoolean" />
              </StackPanel.Resources>
              <RadioButton Content="Draft" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Draft}}" />
              <RadioButton Content="Standard" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Standard}}" />
              <RadioButton Content="Fine" QUALITY_GROUP IsChecked="{Binding Quality, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:Quality.Fine}}" />
              <RadioButton Content="Single" LAYOUT_GROUP IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Single}}" />
              <RadioButton Content="Dual" LAYOUT_GROUP IsChecked="{Binding PageLayout, Converter={StaticResource EnumToBoolean}, ConverterParameter={x:Static local:PageLayout.Dual}}" />
            </StackPanel>
            """;

        string xaml = Template
            .Replace("QUALITY_GROUP", withGroupName ? """GroupName="quality" """ : string.Empty)
            .Replace("LAYOUT_GROUP", withGroupName ? """GroupName="pageLayout" """ : string.Empty);

        var panel = SceneContext.LoadXaml<StackPanel>(xaml, ("local", LocalNamespace));
        var viewModel = new PrintSettingsViewModel();
        panel.DataContext = viewModel;

        // ViewModel が保持している値を、ラジオボタンの表示と並べて見えるようにする。
        panel.Children.Add(new TextBlock
        {
            Text = $"Quality = {viewModel.Quality}\nPageLayout = {viewModel.PageLayout}",
            FontFamily = new FontFamily("Consolas, Courier New"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x3D, 0x4D)),
            Margin = new Thickness(0, 10, 0, 0),
        });

        return panel;
    }
}

/// <summary>記事の例と同じ、印刷品質を表す列挙体。</summary>
public enum Quality
{
    Draft,
    Standard,
    Fine,
}

/// <summary>記事の例と同じ、面付けを表す列挙体。</summary>
public enum PageLayout
{
    Single,
    Dual,
}

/// <summary>初期値を既定値以外に置き、初期選択が表示されるかどうかが分かるようにする。</summary>
public sealed class PrintSettingsViewModel : INotifyPropertyChanged
{
    private Quality _quality = Quality.Standard;
    private PageLayout _pageLayout = PageLayout.Single;

    public Quality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value)
            {
                return;
            }

            _quality = value;
            Raise(nameof(Quality));
        }
    }

    public PageLayout PageLayout
    {
        get => _pageLayout;
        set
        {
            if (_pageLayout == value)
            {
                return;
            }

            _pageLayout = value;
            Raise(nameof(PageLayout));
        }
    }

    private void Raise(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>記事に載せたコンバーターと同じ実装。</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter : Binding.DoNothing;
}
