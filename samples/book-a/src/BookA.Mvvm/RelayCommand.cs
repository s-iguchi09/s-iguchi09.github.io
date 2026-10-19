using System.Windows.Input;

namespace BookA.Mvvm;

/// <summary>
/// デリゲートで処理を渡すコマンド。
///
/// <see cref="CanExecuteChanged"/> は自前のイベントで、条件が変わったら <see cref="RaiseCanExecuteChanged"/> を呼ぶ。
/// CommandManager.RequerySuggested には委ねない（再評価の時点をビューモデルが決めるため）。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    /// <summary>実行できるかどうかの条件が変わったことを、コマンドにバインドしたコントロールへ知らせる。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
