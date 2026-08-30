using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「UserControl に定義した DependencyProperty へのバインディングが効かない問題」の図。
///
/// 1 枚目は、UserControl の内部から自身の DependencyProperty を参照する 3 通りの書き方
/// （素の <c>{Binding Title}</c> / <c>RelativeSource AncestorType</c> / <c>ElementName</c>）を
/// 同じ 1 つのコントロールへ並べ、素の書き方だけが解決できずに空欄になることを実際の描画で示す。
///
/// 2 枚目は、コンストラクターで <c>DataContext = this</c> を設定したコントロールが、
/// 利用側でリテラルを渡したときは動き、Binding を渡したときだけ空欄になるという
/// 非対称な症状を示す。
/// </summary>
internal sealed class UserControlDependencyPropertyScene : IScene
{
    /// <summary>3 通りの参照方法を並べた内部 XAML。記事に載せる書き方をそのまま使う。</summary>
    private const string ThreeWaysXaml =
        """
        <StackPanel Margin="12,10">
          <StackPanel.Resources>
            <Style x:Key="Label" TargetType="TextBlock">
              <Setter Property="FontFamily" Value="Consolas, Courier New" />
              <Setter Property="FontSize" Value="12" />
              <Setter Property="Foreground" Value="#333D4D" />
              <Setter Property="Margin" Value="0,0,0,4" />
            </Style>
            <Style x:Key="Result" TargetType="Border">
              <Setter Property="BorderBrush" Value="#C3CCDB" />
              <Setter Property="BorderThickness" Value="1" />
              <Setter Property="CornerRadius" Value="4" />
              <Setter Property="Background" Value="#F5F7FB" />
              <Setter Property="Padding" Value="9,4" />
              <Setter Property="MinWidth" Value="150" />
              <Setter Property="HorizontalAlignment" Value="Left" />
              <Setter Property="Margin" Value="0,0,0,14" />
            </Style>
            <Style TargetType="TextBlock">
              <Setter Property="FontSize" Value="13" />
              <Setter Property="MinHeight" Value="17" />
            </Style>
          </StackPanel.Resources>

          <TextBlock Style="{StaticResource Label}" Text="{}{Binding Title}" />
          <Border Style="{StaticResource Result}">
            <TextBlock x:Name="Plain" Text="{Binding Title}" />
          </Border>

          <TextBlock Style="{StaticResource Label}"
                     Text="{}{Binding Title, RelativeSource={RelativeSource AncestorType=UserControl}}" />
          <Border Style="{StaticResource Result}">
            <TextBlock x:Name="ByAncestor"
                       Text="{Binding Title, RelativeSource={RelativeSource AncestorType=UserControl}}" />
          </Border>

          <TextBlock Style="{StaticResource Label}" Text="{}{Binding Title, ElementName=Root}" />
          <Border Style="{StaticResource Result}" Margin="0">
            <TextBlock x:Name="ByElementName" Text="{Binding Title, ElementName=Root}" />
          </Border>
        </StackPanel>
        """;

    /// <summary><c>DataContext = this</c> を設定したコントロールの内部 XAML。</summary>
    private const string SingleValueXaml =
        """
        <Border Background="#F5F7FB" Padding="9,4" MinWidth="150" HorizontalAlignment="Left">
          <TextBlock x:Name="Value" Text="{Binding Title}" FontSize="13" MinHeight="17" />
        </Border>
        """;

    public IReadOnlyList<string> Verifies =>
    [
        "UserControl 内部の要素から見た DataContext が、利用側の ViewModel であること",
        "素の Binding では自身の依存関係プロパティに届かないこと",
        "RelativeSource Self / AncestorType では届くこと",
    ];

    public string Slug => "wpf-usercontrol-dependencyproperty-binding-not-working";

    public async Task CaptureAsync(SceneContext context)
    {
        await CaptureThreeWaysAsync(context);
        await CaptureSelfDataContextAsync(context);
    }

    /// <summary>
    /// 内部から自身の DependencyProperty を参照する 3 通りの書き方を比較する図。
    /// 3 つとも同じコントロールの同じ <c>Title</c> を指しており、差は書き方だけである。
    /// </summary>
    private static async Task CaptureThreeWaysAsync(SceneContext context)
    {
        var card = new DemoCard(ThreeWaysXaml);
        BindingOperations.SetBinding(card, DemoCard.TitleProperty, new Binding("HeaderText"));

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(UsageCaption("<local:InfoCard Title=\"{Binding HeaderText}\" />", first: true));
        root.Children.Add(Frame(card));

        var window = CreateWindow("UserControl DependencyProperty", root);

        await context.ShootAsync(window, "usercontrol-dp-binding.png", async _ =>
        {
            // 図が示す差が、書き方の違いだけによることを撮影前に確認する。
            AssertText(card, "Plain", string.Empty);
            AssertText(card, "ByAncestor", "Report");
            AssertText(card, "ByElementName", "Report");
            if (card.Title != "Report")
            {
                throw new InvalidOperationException("外側から DP へ値が届いている前提が崩れている。");
            }

            await Task.CompletedTask;
        });

        await context.SaveTableAsync(
            "TextBlock inside the UserControl, bound three ways",
            ["binding inside the control", "resulting Text", "its DataContext"],
            await ValidationAndScopeMeasurements.UserControlPropertyScopeAsync(),
            "usercontrol-dp-scope.svg");
    }

    /// <summary>
    /// <c>DataContext = this</c> を設定したコントロールが、リテラルでは動き
    /// Binding では動かないという非対称を示す図。
    /// </summary>
    private static async Task CaptureSelfDataContextAsync(SceneContext context)
    {
        var literal = new DemoCard(SingleValueXaml, selfDataContext: true) { Title = "Report" };

        var bound = new DemoCard(SingleValueXaml, selfDataContext: true);
        BindingOperations.SetBinding(bound, DemoCard.TitleProperty, new Binding("HeaderText"));

        var root = new StackPanel { Margin = new Thickness(18) };
        root.Children.Add(UsageCaption("<local:InfoCard Title=\"Report\" />", first: true));
        root.Children.Add(Frame(literal));
        root.Children.Add(UsageCaption("<local:InfoCard Title=\"{Binding HeaderText}\" />", first: false));
        root.Children.Add(Frame(bound));

        var window = CreateWindow("DataContext = this", root);

        await context.ShootAsync(window, "usercontrol-dp-datacontext-this.png", async _ =>
        {
            AssertText(literal, "Value", "Report");
            AssertText(bound, "Value", string.Empty);
            if (bound.Title.Length != 0)
            {
                throw new InvalidOperationException("Binding 側の DP が既定値のままである前提が崩れている。");
            }

            await Task.CompletedTask;
        });
    }

    private static Window CreateWindow(string title, UIElement content) => new()
    {
        Title = title,
        Content = content,
        // 図の中の値は ViewModel の HeaderText から供給する。
        DataContext = new PageViewModel("Report"),
        SizeToContent = SizeToContent.WidthAndHeight,
        ResizeMode = ResizeMode.CanMinimize,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Background = Brushes.White,
    };

    /// <summary>利用側のマークアップを示す見出し。文言は入れずコードだけで構成する。</summary>
    private static TextBlock UsageCaption(string markup, bool first) => new()
    {
        Text = markup,
        FontFamily = new FontFamily("Consolas, Courier New"),
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x3D, 0x4D)),
        Margin = new Thickness(0, first ? 0 : 16, 0, 8),
    };

    /// <summary>コントロールの範囲が図の上で分かるように枠で囲む。</summary>
    private static Border Frame(UIElement child) => new()
    {
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB2)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = child,
    };

    private static void AssertText(DemoCard card, string name, string expected)
    {
        var block = (TextBlock)card.FindInner(name);
        if (block.Text != expected)
        {
            throw new InvalidOperationException(
                $"{name} の描画結果が想定と異なる: 実際 '{block.Text}' / 想定 '{expected}'");
        }
    }

    /// <summary>図に登場する ViewModel。<c>Title</c> という名前は持たせない。</summary>
    private sealed record PageViewModel(string HeaderText);

    /// <summary>
    /// 記事で扱う UserControl を最小構成で再現したもの。
    /// <c>Title</c> は依存関係プロパティとして公開し、内部 XAML は呼び出し側から差し替える。
    /// </summary>
    private sealed class DemoCard : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(DemoCard), new PropertyMetadata(string.Empty));

        private readonly FrameworkElement inner;

        public DemoCard(string innerXaml, bool selfDataContext = false)
        {
            inner = SceneContext.LoadXaml<FrameworkElement>(innerXaml);
            Content = inner;

            // ElementName=Root で自身を参照できるよう、内部 XAML の名前スコープへ自身を登録する。
            // コンパイル済みの UserControl では、ルート要素の x:Name と内部要素の x:Name が
            // 同じ名前スコープに入る。ここではそれを手作業で再現している。
            inner.RegisterName("Root", this);

            if (selfDataContext)
            {
                DataContext = this;
            }
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public object FindInner(string name) =>
            inner.FindName(name)
            ?? throw new InvalidOperationException($"内部 XAML に {name} が見つからない。");
    }
}
