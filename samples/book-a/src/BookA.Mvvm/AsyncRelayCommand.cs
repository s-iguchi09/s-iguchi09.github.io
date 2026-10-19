using System.Windows.Input;

namespace BookA.Mvvm;

/// <summary>
/// 非同期の処理を await で待つコマンド。実行中は CanExecute が false になり、ボタンが無効になる（二重に実行させない）。
///
/// <see cref="Execute"/> は async void である。ICommand.Execute は戻り値を返せないため、イベントハンドラと同じく
/// async void を使ってよい数少ない場所に当たる。処理の中で捕まえなかった例外は、UI スレッドの
/// Dispatcher（Application.DispatcherUnhandledException）に届く。処理ごとにエラーを扱うなら、渡す処理の中で try / catch する。
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>処理を実行している最中か。</summary>
    public bool IsRunning => _isRunning;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>実行できるかどうかの条件が変わったことを、コマンドにバインドしたコントロールへ知らせる。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
