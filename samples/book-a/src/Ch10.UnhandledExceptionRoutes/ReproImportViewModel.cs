using BookA.Mvvm;

namespace Ch10.UnhandledExceptionRoutes;

/// <summary>
/// 再現版のビューモデル。取り込みを Task.Run で別スレッドに渡し、その Task を待たない。
/// Task の中の例外は Task に閉じ込められ、DispatcherUnhandledException にも AppDomain.UnhandledException にも届かない。
/// 失敗したことが画面にもログにも出ず、取り込みが終わったように見える。
/// </summary>
public sealed class ReproImportViewModel : ObservableObject
{
    private string _errorMessage = "";

    public ReproImportViewModel()
    {
        StartImportCommand = new RelayCommand(() => _ = Task.Run(Importer.Import));
    }

    public RelayCommand StartImportCommand { get; }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }
}
