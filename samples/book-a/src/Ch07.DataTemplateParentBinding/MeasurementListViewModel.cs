using System.Collections.ObjectModel;
using BookA.Mvvm;

namespace Ch07.DataTemplateParentBinding;

/// <summary>
/// 計測値の一覧のビューモデル。削除のコマンドと単位は、行ではなくこの親が持つ。
/// 再現版と修正版で共通に使い、違いはビューのテンプレートの中のバインドの書き方だけである。
/// </summary>
public sealed class MeasurementListViewModel
{
    public MeasurementListViewModel()
    {
        Items =
        [
            new MeasurementViewModel("A", 120),
            new MeasurementViewModel("B", 80),
            new MeasurementViewModel("C", 240),
        ];
        DeleteCommand = new RelayCommand<MeasurementViewModel>(item => Items.Remove(item));
    }

    /// <summary>全行に共通の単位。</summary>
    public string Unit => "kg";

    public ObservableCollection<MeasurementViewModel> Items { get; }

    /// <summary>行を削除する。CommandParameter で行の項目を受け取る。</summary>
    public RelayCommand<MeasurementViewModel> DeleteCommand { get; }
}
