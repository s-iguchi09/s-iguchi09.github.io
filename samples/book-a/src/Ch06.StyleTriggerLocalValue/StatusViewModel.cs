using BookA.Mvvm;

namespace Ch06.StyleTriggerLocalValue;

/// <summary>
/// 入力の状態を持つビューモデル。HasError が true の間、ビューの枠の背景を薄い赤にしたい。
/// 再現版と修正版で共通に使い、違いはビューの XAML（背景の既定値をどこに書くか）だけである。
/// </summary>
public sealed class StatusViewModel : ObservableObject
{
    private bool _hasError;

    public StatusViewModel()
    {
        ShowErrorCommand = new RelayCommand(() => HasError = true);
        ClearErrorCommand = new RelayCommand(() => HasError = false);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public RelayCommand ShowErrorCommand { get; }

    public RelayCommand ClearErrorCommand { get; }
}
