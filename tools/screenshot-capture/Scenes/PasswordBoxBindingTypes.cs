using System.Security;
using System.Windows;
using System.Windows.Controls;
using BookA.Mvvm;

// 記事「wpf-passwordbox-binding-mvvm」のシーン（PasswordBoxBindingScene）が XAML から参照する型。
// XamlReader で読むため public にし、ほかのシーンの型と名前がぶつからないよう名前空間を分ける。
namespace ScreenshotCapture.Scenes.PasswordBoxBinding;

/// <summary>記事の添付プロパティ。Attach で PasswordChanged を購読し、BoundPassword とビューモデルの string を同期する。</summary>
public static class PasswordBoxAssist
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached("Attach", typeof(bool), typeof(PasswordBoxAssist),
            new PropertyMetadata(false, OnAttachChanged));

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxAssist),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);
    public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);
    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue) box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        string value = (string?)e.NewValue ?? string.Empty;

        // 入力で書き戻した値と同じなら設定し直さない。Password を設定すると、次の入力が先頭に入る。
        if (box.Password != value) box.Password = value;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}

/// <summary>流通している書き方の 1: 変更コールバックの中で購読する（初期値が既定値と同じだと呼ばれない）。</summary>
public static class NaivePasswordAssist
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(NaivePasswordAssist),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        string value = (string?)e.NewValue ?? string.Empty;
        if (box.Password != value) box.Password = value;
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}

/// <summary>流通している書き方の 2: 購読はするが、値を比べずに Password を設定し直す（次の入力が先頭に入る）。</summary>
public static class UncheckedPasswordAssist
{
    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached("Attach", typeof(bool), typeof(UncheckedPasswordAssist),
            new PropertyMetadata(false, OnAttachChanged));

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(UncheckedPasswordAssist),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static bool GetAttach(DependencyObject d) => (bool)d.GetValue(AttachProperty);
    public static void SetAttach(DependencyObject d, bool value) => d.SetValue(AttachProperty, value);
    public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        if ((bool)e.NewValue) box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;
        box.PasswordChanged -= OnPasswordChanged;
        box.Password = (string?)e.NewValue ?? string.Empty;
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}

/// <summary>記事の方法 1 のビューモデル。パスワードを string のプロパティで持つ。</summary>
public sealed class LoginViewModel : ObservableObject
{
    private string _password = string.Empty;

    public LoginViewModel() => SignInCommand = new RelayCommand(SignIn, () => Password.Length > 0);

    public RelayCommand SignInCommand { get; }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value)) SignInCommand.RaiseCanExecuteChanged();
        }
    }

    private void SignIn()
    {
        // 認証の処理は計測に関係しないので省く。終わったら空にする。
        Password = string.Empty;
    }
}

/// <summary>
/// 記事の方法 2 のビューモデル。CommandParameter で受け取ったものを記録する。
/// SecurePassword をバインドした場合は SecureString が、PasswordBox を渡した場合は PasswordBox が届く。
/// </summary>
public sealed class CommandParameterViewModel
{
    public CommandParameterViewModel(bool requireInput)
    {
        SignInCommand = new RelayCommand<object>(SignIn,
            p => !requireInput || p is not PasswordBox box || HasInput(box));
    }

    public RelayCommand<object> SignInCommand { get; }

    /// <summary>直前の実行で受け取ったパスワードの長さ。実行されていなければ -1。</summary>
    public int LastLength { get; private set; } = -1;

    /// <summary>直前の実行で受け取ったものの型名。</summary>
    public string LastType { get; private set; } = "(none)";

    private static bool HasInput(PasswordBox box)
    {
        using SecureString password = box.SecurePassword;
        return password.Length > 0;
    }

    private void SignIn(object parameter)
    {
        LastType = parameter?.GetType().Name ?? "null";
        switch (parameter)
        {
            case string text:
                LastLength = text.Length;
                break;
            case SecureString bound:
                LastLength = bound.Length;
                break;
            case PasswordBox box:
                using (SecureString password = box.SecurePassword)
                {
                    LastLength = password.Length;
                }

                break;
        }
    }
}

/// <summary>記事の方法 2 のビューモデル（記事のコードと同じ）。計測のため、受け取った長さだけを記録する。</summary>
public sealed class SignInViewModel
{
    public SignInViewModel() => SignInCommand = new RelayCommand<PasswordBox>(SignIn);

    public RelayCommand<PasswordBox> SignInCommand { get; }

    /// <summary>計測用。直前の実行で読んだ SecurePassword の長さ。</summary>
    public int LastLength { get; private set; } = -1;

    private void SignIn(PasswordBox box)
    {
        using SecureString password = box.SecurePassword;

        // 認証の処理に password を渡す。
        LastLength = password.Length;
    }
}
