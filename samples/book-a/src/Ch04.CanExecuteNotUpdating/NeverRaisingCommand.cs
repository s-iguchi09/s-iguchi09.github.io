using System.Windows.Input;

namespace Ch04.CanExecuteNotUpdating;

/// <summary>
/// 再現版のコマンド。よく見かける手書きの RelayCommand で、<see cref="CanExecuteChanged"/> を宣言しているが一度も発行しない。
/// ボタンは最初に CanExecute を問い合わせた結果のまま変わらない。
/// </summary>
public sealed class NeverRaisingCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public NeverRaisingCommand(Action execute, Func<bool> canExecute)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    // 宣言しただけで、どこからも発行しない。コンパイルは通るが、ボタンは問い合わせ直さない。
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter) => _execute();
}
