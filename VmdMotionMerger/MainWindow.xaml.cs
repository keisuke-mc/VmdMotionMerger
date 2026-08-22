using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using VmdMotionMerger.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

namespace VmdMotionMerger;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        Title = "VMD Motion Merger";
        ViewModel = new MainViewModel(DispatcherQueue);

        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ViewModel.SetWindowHandle(hwnd);

        // 初期ウィンドウサイズ（デザインのモックアップに合わせて縦長めに設定）
        AppWindow.Resize(new SizeInt32(920, 760));

        // ログの追加に合わせて末尾へ自動スクロールする
        ViewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
    }

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (ViewModel.LogEntries.Count == 0) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            LogListView.ScrollIntoView(ViewModel.LogEntries[^1]);
        });
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride != null)
        {
            e.DragUIOverride.Caption = "VMDファイルを追加";
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            var storageItems = await e.DataView.GetStorageItemsAsync();
            var paths = storageItems.OfType<StorageFile>().Select(f => f.Path).ToList();
            if (paths.Count > 0)
            {
                await ViewModel.LoadFilesAsync(paths);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }
}
