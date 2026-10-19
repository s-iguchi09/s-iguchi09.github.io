using BookA.Mvvm;

namespace Ch10.UnhandledExceptionRoutes;

/// <summary>
/// 修正版のビューモデル。AsyncRelayCommand の中で Task.Run を await し、try / catch で受ける。
/// 例外は await の時点で UI スレッドへ戻ってくるので、ここで受けて画面に出せる。
/// catch しなかった例外は、AsyncRelayCommand（async void）から DispatcherUnhandledException に届く。
/// </summary>
public sealed class ImportViewModel : ObservableObject
{
    private string _errorMessage = "";

    public ImportViewModel()
    {
        StartImportCommand = new AsyncRelayCommand(ImportAsync);
    }

    public AsyncRelayCommand StartImportCommand { get; }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private async Task ImportAsync()
    {
        ErrorMessage = "";
        try
        {
            await Task.Run(Importer.Import);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
