using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenshotCapture.Scenes;

/// <summary>
/// 記事「WPF で RelayCommand の CanExecute が更新されずボタンが有効にならない問題」の図。
/// 同じ入力に対し、CanExecuteChanged を発火しない実装と
/// CommandManager.RequerySuggested へ委譲する実装でボタンの状態が変わることを示す。
/// </summary>
internal sealed class RelayCommandCanExecuteScene : IScene
{
    private const string TypedName = "taro";

    public string Slug => "wpf-relaycommand-canexecute-not-updating";

    public async Task CaptureAsync(SceneContext context)
    {
        var broken = new NameViewModel(useRequerySuggested: false);
        var fixedUp = new NameViewModel(useRequerySuggested: true);

        Window window = DemoLayout.BuildPanelWindow(
            "RelayCommand.CanExecute",
            [
                new DemoLayout.Panel("CanExecuteChanged  (never raised)", BuildRow(broken)),
                new DemoLayout.Panel("CanExecuteChanged => CommandManager.RequerySuggested", BuildRow(fixedUp)),
            ],
            Orientation.Vertical);

        await context.ShootAsync(window, "relaycommand-canexecute-button-state.png", async _ =>
        {
            broken.Name = TypedName;
            fixedUp.Name = TypedName;

            // ユーザー操作時に WPF が行うのと同じ再問い合わせを促す。
            CommandManager.InvalidateRequerySuggested();
            await Task.Delay(250);
        });
    }

    private static UIElement BuildRow(NameViewModel viewModel)
    {
        var textBox = SceneContext.LoadXaml<TextBox>(
            """<TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}" Width="150" />""");

        var button = SceneContext.LoadXaml<Button>(
            """<Button Content="Save" Command="{Binding SaveCommand}" Padding="14,4" />""");

        TextBlock arrow = DemoLayout.Arrow(new Thickness(14, 0, 14, 0));

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            DataContext = viewModel,
            Children = { textBox, arrow, button },
        };
    }

    /// <summary>記事の例と同じく、名前の入力有無で CanExecute を切り替える。</summary>
    private sealed class NameViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public NameViewModel(bool useRequerySuggested)
        {
            SaveCommand = new DemoRelayCommand(
                () => !string.IsNullOrWhiteSpace(Name),
                useRequerySuggested);
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public ICommand SaveCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// CanExecuteChanged を一切発火しない実装と、
    /// CommandManager.RequerySuggested へ委譲する実装を切り替えられるコマンド。
    /// </summary>
    private sealed class DemoRelayCommand(Func<bool> canExecute, bool useRequerySuggested) : ICommand
    {
        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
        }

        public event EventHandler? CanExecuteChanged
        {
            add
            {
                if (useRequerySuggested)
                {
                    CommandManager.RequerySuggested += value;
                }
            }
            remove
            {
                if (useRequerySuggested)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }
    }
}
