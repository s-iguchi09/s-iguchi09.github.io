using System.Windows;
using System.Windows.Controls;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF の Label でアンダーバーが消える理由と回避方法」の図。
/// 本文の表と実装例に対応する実際の描画結果を取得する。
/// </summary>
internal sealed class LabelUnderscoreScene : IScene
{
    public string Slug => "wpf-label-underscore-issue";

    public async Task CaptureAsync(SceneContext context)
    {
        await context.ShootAsync(BuildSymptomWindow(), "label-underscore-rendering.png");
        await context.ShootAsync(BuildAffectedControlsWindow(), "label-underscore-affected-controls.png");
        await context.ShootAsync(BuildWorkaroundWindow(), "label-underscore-workarounds.png");
    }

    /// <summary>
    /// 同じ文字列を各コントロールへ与え、アンダーバーが消える範囲が
    /// <c>Label</c> に限らないことを示す。
    /// 既定テンプレートの <c>ContentPresenter.RecognizesAccessKey</c> が
    /// <c>True</c> かどうかで結果が分かれる。
    /// </summary>
    private static Window BuildAffectedControlsWindow()
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                """<Label Content="my_var" />""",
                SceneContext.LoadXaml<Label>("""<Label Content="my_var" Padding="0" />""")),
            new DemoLayout.Row(
                """<Button Content="my_var" />""",
                SceneContext.LoadXaml<Button>("""<Button Content="my_var" Padding="8,2" />""")),
            new DemoLayout.Row(
                """<CheckBox Content="my_var" />""",
                SceneContext.LoadXaml<CheckBox>("""<CheckBox Content="my_var" />""")),
            new DemoLayout.Row(
                """<GroupBox Header="my_var" />""",
                SceneContext.LoadXaml<GroupBox>("""<GroupBox Header="my_var" Width="150" Height="44" />""")),
            new DemoLayout.Row(
                """<ListBoxItem Content="my_var" />""",
                SceneContext.LoadXaml<ListBoxItem>("""<ListBoxItem Content="my_var" Padding="0" />""")),
            new DemoLayout.Row(
                """<TextBlock Text="my_var" />""",
                SceneContext.LoadXaml<TextBlock>("""<TextBlock Text="my_var" />""")),
        };

        return DemoLayout.BuildComparisonWindow("RecognizesAccessKey", rows);
    }

    /// <summary>
    /// 本文「原因・背景」の表に対応する 3 例を、実際の Label で描画する。
    /// </summary>
    private static Window BuildSymptomWindow()
    {
        var rows = new[]
        {
            Row("""<Label Content="_File" />"""),
            Row("""<Label Content="my_var" />"""),
            Row("""<Label Content="name_" />"""),
        };

        return DemoLayout.BuildComparisonWindow("AccessText", rows);

        static DemoLayout.Row Row(string markup) => new(
            markup,
            SceneContext.LoadXaml<Label>(markup.Replace(" />", """ Padding="0" />""")));
    }

    /// <summary>
    /// 本文「実装例」の 4 つの回避方法が、いずれも my_variable と表示されることを示す。
    /// </summary>
    private static Window BuildWorkaroundWindow()
    {
        var rows = new[]
        {
            new DemoLayout.Row(
                """<Label Content="my__variable" />""",
                SceneContext.LoadXaml<Label>("""<Label Content="my__variable" Padding="0" />""")),
            new DemoLayout.Row(
                """<TextBlock Text="my_variable" />""",
                SceneContext.LoadXaml<TextBlock>("""<TextBlock Text="my_variable" />""")),
            new DemoLayout.Row(
                """<Label ContentTemplate="{TextBlock}" />""",
                SceneContext.LoadXaml<Label>(
                    """
                    <Label Content="my_variable" Padding="0">
                      <Label.ContentTemplate>
                        <DataTemplate>
                          <TextBlock Text="{Binding}" />
                        </DataTemplate>
                      </Label.ContentTemplate>
                    </Label>
                    """)),
            // 差し替えた ControlTemplate のうち、結果を決めている要素だけを見出しに出す。
            new DemoLayout.Row(
                """<ContentPresenter RecognizesAccessKey="False" />""",
                SceneContext.LoadXaml<Label>(
                    """
                    <Label Content="my_variable" Padding="0">
                      <Label.Template>
                        <ControlTemplate TargetType="Label">
                          <ContentPresenter RecognizesAccessKey="False" />
                        </ControlTemplate>
                      </Label.Template>
                    </Label>
                    """)),
        };

        return DemoLayout.BuildComparisonWindow("Label / TextBlock", rows);
    }
}
