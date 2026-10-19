using BookA.Mvvm;

namespace Ch02.InputLimits;

/// <summary>
/// 再現版のビューモデル。顧客コードの長さも音量の範囲も確かめず、制限はビューの MaxLength や Slider に任せている。
/// 保存済みのデータを読み込むと、制限を超えた値がそのまま画面に出て、そのまま保存される。
/// </summary>
public sealed class ReproCustomerViewModel : ObservableObject
{
    private string _code = "";
    private double _volume = 50;
    private string _lastSaved = "（まだ保存していない）";

    public ReproCustomerViewModel()
    {
        LoadOverLimitCommand = new RelayCommand(() => Load(SavedRecords.OverLimit));
        LoadOffStepCommand = new RelayCommand(() => Load(SavedRecords.OffStep));
        SaveCommand = new RelayCommand(Save);
    }

    /// <summary>顧客コード。ビューの TextBox は MaxLength=5 だが、ここでは長さを確かめない。</summary>
    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    /// <summary>音量。ビューの Slider は 0〜100・10 刻みだが、ここでは範囲も刻みも確かめない。</summary>
    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    /// <summary>最後に保存した内容。保存処理に渡った値を画面で確かめるために出す。</summary>
    public string LastSaved
    {
        get => _lastSaved;
        private set => SetProperty(ref _lastSaved, value);
    }

    /// <summary>上限を超えた保存済みのデータ（顧客コード 8 文字・音量 150）を読み込む。</summary>
    public RelayCommand LoadOverLimitCommand { get; }

    /// <summary>刻みに合わない保存済みのデータ（音量 23.4）を読み込む。</summary>
    public RelayCommand LoadOffStepCommand { get; }

    /// <summary>保存する。再現版では、いつでも保存できる。</summary>
    public RelayCommand SaveCommand { get; }

    private void Load(SavedRecord record)
    {
        Code = record.Code;
        Volume = record.Volume;
    }

    private void Save() => LastSaved = $"顧客コード \"{Code}\"、音量 {Volume}";
}
