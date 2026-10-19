using Ch01.BindingModeOmitted;
using Ch02.InputLimits;

namespace BookA.Samples;

/// <summary>
/// 起動用のアプリのビューモデル。各章の再現版と修正版に、それぞれ別のビューモデルを渡す
/// （片方の操作がもう片方に影響しないようにするため）。
/// </summary>
public sealed class MainViewModel
{
    public FolderBrowserViewModel Ch01Repro { get; } = new();

    public FolderBrowserViewModel Ch01Fixed { get; } = new();

    public ReproCustomerViewModel Ch02Repro { get; } = new();

    public CustomerViewModel Ch02Fixed { get; } = new();

    public Ch03.DataGridCommitOnSave.ItemsViewModel Ch03Repro { get; } = new();

    public Ch03.DataGridCommitOnSave.ItemsViewModel Ch03Fixed { get; } = new();

    public Ch04.CanExecuteNotUpdating.ReproSaveViewModel Ch04Repro { get; } = new();

    public Ch04.CanExecuteNotUpdating.SaveViewModel Ch04Fixed { get; } = new();

    public Ch05.ValidationNotDisplayed.ReproAccountViewModel Ch05Repro { get; } = new();

    public Ch05.ValidationNotDisplayed.AccountViewModel Ch05Fixed { get; } = new();

    public Ch06.StyleTriggerLocalValue.StatusViewModel Ch06Repro { get; } = new();

    public Ch06.StyleTriggerLocalValue.StatusViewModel Ch06Fixed { get; } = new();

    public Ch07.DataTemplateParentBinding.MeasurementListViewModel Ch07Repro { get; } = new();

    public Ch07.DataTemplateParentBinding.MeasurementListViewModel Ch07Fixed { get; } = new();
}
