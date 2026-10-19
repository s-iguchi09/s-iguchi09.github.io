using System.Windows.Input;

namespace BookA.Mvvm;

/// <summary>
/// CommandParameter を型付きで受け取るコマンド。一覧の行のボタンから、その行の項目を渡すときに使う。
///
/// パラメーターが <typeparamref name="T"/> でないとき（null を含む）は実行できないものとして扱う。
/// <see cref="CanExecuteChanged"/> は <see cref="RelayCommand"/> と同じく自前のイベントで、条件が変わったら
/// <see cref="RaiseCanExecuteChanged"/> を呼ぶ。
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public void Execute(object? parameter)
    {
        if (parameter is T value)
        {
            _execute(value);
        }
    }

    /// <summary>実行できるかどうかの条件が変わったことを、コマンドにバインドしたコントロールへ知らせる。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
