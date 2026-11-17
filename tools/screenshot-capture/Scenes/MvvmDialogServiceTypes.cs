using System.Collections.ObjectModel;
using System.Windows;
using BookA.Mvvm;
using Microsoft.Win32;

// 記事「wpf-mvvm-dialog-service」のシーン（MvvmDialogServiceScene）が使う型。
// XamlReader で読む XAML からバインドするため public にし、ほかのシーンの型と名前がぶつからないよう名前空間を分ける。
// DialogService・IDialogViewModel・CustomerListViewModel・CustomerEditViewModel は記事のコードと同じ内容にする。
namespace ScreenshotCapture.Scenes.MvvmDialogService;

/// <summary>ビューモデルがダイアログを出すときに使う窓口。記事のコードと同じ。</summary>
public interface IDialogService
{
    bool Confirm(object ownerViewModel, string message, string caption);

    string? OpenFile(object ownerViewModel, string filter);

    bool ShowDialog(object ownerViewModel, IDialogViewModel viewModel);
}

/// <summary>子ウィンドウのビューモデル。閉じたいときに結果を付けて知らせる。記事のコードと同じ。</summary>
public interface IDialogViewModel
{
    event EventHandler<bool>? CloseRequested;
}

/// <summary>記事の DialogService。Owner は、呼び出したビューモデルを DataContext に持つウィンドウにする。</summary>
public sealed class DialogService : IDialogService
{
    private readonly Dictionary<Type, Func<Window>> _windows = new();

    public void Register<TViewModel>(Func<Window> createWindow) where TViewModel : IDialogViewModel =>
        _windows[typeof(TViewModel)] = createWindow;

    public bool Confirm(object ownerViewModel, string message, string caption) =>
        MessageBox.Show(FindWindow(ownerViewModel), message, caption,
            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    public string? OpenFile(object ownerViewModel, string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(FindWindow(ownerViewModel)) == true ? dialog.FileName : null;
    }

    public bool ShowDialog(object ownerViewModel, IDialogViewModel viewModel)
    {
        // 所有者を先に探す。見つからずに例外になったとき、作っただけのウィンドウを Application.Windows に残さないため。
        Window owner = FindWindow(ownerViewModel);
        Window window = _windows[viewModel.GetType()]();
        window.Owner = owner;
        window.DataContext = viewModel;

        void OnCloseRequested(object? sender, bool result) => window.DialogResult = result;
        viewModel.CloseRequested += OnCloseRequested;
        try
        {
            return window.ShowDialog() == true;
        }
        finally
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
    }

    private static Window FindWindow(object viewModel) =>
        Application.Current.Windows.OfType<Window>().First(w => w.DataContext == viewModel);
}

/// <summary>テストで使う IDialogService。決めた答えをすぐに返す。</summary>
public sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; }

    public int ConfirmCount { get; private set; }

    public bool Confirm(object ownerViewModel, string message, string caption)
    {
        ConfirmCount++;
        return ConfirmResult;
    }

    public string? OpenFile(object ownerViewModel, string filter) => null;

    public bool ShowDialog(object ownerViewModel, IDialogViewModel viewModel) => false;
}

public sealed class Customer : ObservableObject
{
    private string _name;

    public Customer(string name) => _name = name;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}

/// <summary>記事の一覧画面のビューモデル。ダイアログは IDialogService を通して出す。</summary>
public sealed class CustomerListViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private Customer? _selected;
    private string? _importPath;

    public CustomerListViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        DeleteCommand = new RelayCommand(Delete, () => Selected is not null);
        EditCommand = new RelayCommand(Edit, () => Selected is not null);
        ImportCommand = new RelayCommand(Import);
    }

    public ObservableCollection<Customer> Customers { get; } = new();

    public RelayCommand DeleteCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand ImportCommand { get; }

    public Customer? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                DeleteCommand.RaiseCanExecuteChanged();
                EditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? ImportPath
    {
        get => _importPath;
        private set => SetProperty(ref _importPath, value);
    }

    private void Delete()
    {
        if (Selected is null) return;
        if (_dialogs.Confirm(this, $"{Selected.Name} を削除しますか。", "削除の確認"))
        {
            Customers.Remove(Selected);
        }
    }

    private void Import()
    {
        string? path = _dialogs.OpenFile(this, "JSON ファイル (*.json)|*.json");
        if (path is not null) ImportPath = path;
    }

    private void Edit()
    {
        if (Selected is null) return;

        // 編集は写しに対して行い、OK のときだけ元へ戻す。
        var editor = new CustomerEditViewModel(Selected.Name);
        if (_dialogs.ShowDialog(this, editor))
        {
            Selected.Name = editor.Name;
        }
    }
}

/// <summary>記事の子ウィンドウのビューモデル。OK・キャンセルで結果を付けて閉じる要求を出す。</summary>
public sealed class CustomerEditViewModel : ObservableObject, IDialogViewModel
{
    private string _name;

    public CustomerEditViewModel(string name)
    {
        _name = name;
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    public event EventHandler<bool>? CloseRequested;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public RelayCommand OkCommand { get; }

    public RelayCommand CancelCommand { get; }
}

/// <summary>
/// 落とし穴の比較用: 一覧の Customer そのものを編集させる子ウィンドウのビューモデル。
/// XAML は CustomerEditViewModel と同じ形にするため、Name を Customer へ素通しする。
/// </summary>
public sealed class SharedCustomerEditViewModel : IDialogViewModel
{
    private readonly Customer _customer;

    public SharedCustomerEditViewModel(Customer customer)
    {
        _customer = customer;
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }

    public event EventHandler<bool>? CloseRequested;

    // INotifyPropertyChanged は持たないが、TextBox から書き込む向きだけを使うので比較には足りる。
    public string Name
    {
        get => _customer.Name;
        set => _customer.Name = value;
    }

    public RelayCommand OkCommand { get; }

    public RelayCommand CancelCommand { get; }
}

/// <summary>比較用: ビューモデルの中で MessageBox.Show を直接呼ぶ書き方。</summary>
public sealed class DirectMessageBoxViewModel : ObservableObject
{
    private Customer? _selected;

    public DirectMessageBoxViewModel() => DeleteCommand = new RelayCommand(Delete);

    public ObservableCollection<Customer> Customers { get; } = new();

    public RelayCommand DeleteCommand { get; }

    public Customer? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private void Delete()
    {
        if (Selected is null) return;
        if (MessageBox.Show($"{Selected.Name} を削除しますか。", "削除の確認",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
        {
            Customers.Remove(Selected);
        }
    }
}

/// <summary>計測用: 中の IDialogService をそのまま呼び、ビューモデルへ返した値を文字列で記録する。</summary>
public sealed class RecordingDialogService(IDialogService inner) : IDialogService
{
    public string LastResult { get; private set; } = "(not called)";

    public bool Confirm(object ownerViewModel, string message, string caption) =>
        Record(inner.Confirm(ownerViewModel, message, caption));

    public string? OpenFile(object ownerViewModel, string filter)
    {
        string? path = inner.OpenFile(ownerViewModel, filter);
        LastResult = path is null ? "null" : $"\"{path}\"";
        return path;
    }

    public bool ShowDialog(object ownerViewModel, IDialogViewModel viewModel) =>
        Record(inner.ShowDialog(ownerViewModel, viewModel));

    private bool Record(bool value)
    {
        LastResult = value ? "true" : "false";
        return value;
    }
}

/// <summary>比較用: ウィンドウを作ってから所有者を探す順序の ShowDialog だけを持つサービス（記事の修正前の形）。</summary>
public sealed class CreateFirstDialogService : IDialogService
{
    private readonly Dictionary<Type, Func<Window>> _windows = new();

    public void Register<TViewModel>(Func<Window> createWindow) where TViewModel : IDialogViewModel =>
        _windows[typeof(TViewModel)] = createWindow;

    public bool Confirm(object ownerViewModel, string message, string caption) => throw new NotSupportedException();

    public string? OpenFile(object ownerViewModel, string filter) => throw new NotSupportedException();

    public bool ShowDialog(object ownerViewModel, IDialogViewModel viewModel)
    {
        Window window = _windows[viewModel.GetType()]();
        window.Owner = Application.Current.Windows.OfType<Window>().First(w => w.DataContext == ownerViewModel);
        window.DataContext = viewModel;
        return window.ShowDialog() == true;
    }
}
