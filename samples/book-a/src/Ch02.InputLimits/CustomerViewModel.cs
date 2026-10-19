using System.Collections;
using System.ComponentModel;
using BookA.Mvvm;

namespace Ch02.InputLimits;

/// <summary>
/// 修正版のビューモデル。値の範囲はここで保証し、ビューの MaxLength や Slider の設定は入力の補助として残す。
///
/// 顧客コードは長すぎても切り詰めず、INotifyDataErrorInfo のエラーとして持つ（読み込んだデータを黙って変えないため）。
/// エラーがある間は保存できない。音量は setter で 0〜100 に収め、10 刻みに丸める。
/// </summary>
public sealed class CustomerViewModel : ObservableObject, INotifyDataErrorInfo
{
    /// <summary>顧客コードの上限。ビューの TextBox.MaxLength も x:Static でこの値を使う。</summary>
    public const int MaxCodeLength = 5;

    private readonly List<string> _codeErrors = [];
    private string _code = "";
    private double _volume = 50;
    private string _lastSaved = "（まだ保存していない）";

    public CustomerViewModel()
    {
        LoadOverLimitCommand = new RelayCommand(() => Load(SavedRecords.OverLimit));
        LoadOffStepCommand = new RelayCommand(() => Load(SavedRecords.OffStep));
        SaveCommand = new RelayCommand(Save, () => !HasErrors);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>顧客コード。どの経路で入った値も、この setter で長さを確かめる。</summary>
    public string Code
    {
        get => _code;
        set
        {
            if (!SetProperty(ref _code, value))
            {
                return;
            }

            ValidateCode();
        }
    }

    /// <summary>音量。どの経路で入った値も、この setter で 0〜100 に収めて 10 刻みに丸める。</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (double.IsNaN(value))
            {
                return;
            }

            double clamped = Math.Clamp(value, 0, 100);
            _volume = Math.Round(clamped / 10, MidpointRounding.AwayFromZero) * 10;

            // 丸めた結果が元の値と同じでも通知する。Slider が送った値（55 など）と表示を、丸めた値に合わせ直すため。
            OnPropertyChanged();
        }
    }

    public bool HasErrors => _codeErrors.Count > 0;

    public string LastSaved
    {
        get => _lastSaved;
        private set => SetProperty(ref _lastSaved, value);
    }

    public RelayCommand LoadOverLimitCommand { get; }

    public RelayCommand LoadOffStepCommand { get; }

    /// <summary>保存する。顧客コードにエラーがある間は実行できない。</summary>
    public RelayCommand SaveCommand { get; }

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName == nameof(Code) ? _codeErrors : Array.Empty<string>();

    private void ValidateCode()
    {
        _codeErrors.Clear();
        if (_code.Length > MaxCodeLength)
        {
            _codeErrors.Add($"顧客コードは {MaxCodeLength} 文字までです。{MaxCodeLength} 文字以下に直すまで保存できません。");
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Code)));
        OnPropertyChanged(nameof(HasErrors));
        SaveCommand.RaiseCanExecuteChanged();
    }

    private void Load(SavedRecord record)
    {
        Code = record.Code;
        Volume = record.Volume;
    }

    private void Save() => LastSaved = $"顧客コード \"{Code}\"、音量 {Volume}";
}
