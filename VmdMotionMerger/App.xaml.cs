using System;
using System.Text;
using Microsoft.UI.Xaml;

namespace VmdMotionMerger;

/// <summary>
/// アプリケーションのエントリポイント。
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        // VMD 内部の文字列（モデル名・ボーン名・モーフ名）は Shift-JIS で格納されているため、
        // .NET の CodePage エンコーディングプロバイダーを登録しておく。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // 想定外の例外はログに残しつつ、アプリの即時クラッシュを防ぐ。
        // （結合処理中の例外は MainViewModel 側で個別にハンドリングしている）
        System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.Exception}");
        e.Handled = true;
    }
}
