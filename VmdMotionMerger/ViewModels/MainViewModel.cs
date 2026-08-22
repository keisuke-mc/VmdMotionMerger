using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using VmdMotionMerger.Models;
using VmdMotionMerger.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VmdMotionMerger.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcherQueue;
    private nint _windowHandle;

    public ObservableCollection<LoadedVmdItem> Items { get; } = new();

    public ObservableCollection<string> LogEntries { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private LoadedVmdItem? _selectedItem;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "準備完了";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    private bool _isBusy;

    public MainViewModel(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        Items.CollectionChanged += (_, _) =>
        {
            RenumberItems();
            MergeCommand.NotifyCanExecuteChanged();
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        };
    }

    public void SetWindowHandle(nint handle) => _windowHandle = handle;

    // ------------------------------------------------------------------
    // ファイル追加
    // ------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanAddFiles))]
    private async Task AddFilesAsync()
    {
        if (_windowHandle == 0) return;

        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add(".vmd");
        picker.ViewMode = PickerViewMode.List;

        IReadOnlyList<StorageFile> files;
        try
        {
            files = await picker.PickMultipleFilesAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ファイル選択エラー：{ex.Message}");
            return;
        }

        if (files is { Count: > 0 })
        {
            await LoadFilesAsync(files.Select(f => f.Path).ToList());
        }
    }

    private bool CanAddFiles() => !IsBusy;

    /// <summary>ドラッグ＆ドロップ・ファイル選択の両方から呼び出される共通の読み込み処理。</summary>
    public async Task LoadFilesAsync(IReadOnlyList<string> paths)
    {
        List<string> vmdPaths = paths
            .Where(p => string.Equals(Path.GetExtension(p), ".vmd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int skipped = paths.Count - vmdPaths.Count;
        if (skipped > 0)
        {
            AppendLog($"VMD以外のファイルを{skipped}件、対象から除外しました。");
        }

        if (vmdPaths.Count == 0) return;

        IsBusy = true;
        try
        {
            int total = vmdPaths.Count;
            int done = 0;

            foreach (string path in vmdPaths)
            {
                done++;
                string fileName = Path.GetFileName(path);
                StatusText = $"読み込み中...\n\nファイル：{done} / {total}\n{fileName}";

                VmdFile? vmd = null;
                string? errorMessage = null;

                await Task.Run(() =>
                {
                    try
                    {
                        vmd = VmdParser.Parse(path, line => EnqueueUi(() => AppendLog($"{fileName} {line}")));
                    }
                    catch (VmdFormatException ex)
                    {
                        errorMessage = ex.Message;
                    }
                    catch (FileNotFoundException)
                    {
                        errorMessage = "指定されたファイルが見つかりません。";
                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"VMDファイルを読み込めませんでした。（{ex.Message}）";
                    }
                });

                if (vmd == null)
                {
                    AppendLog($"エラー：{fileName} - {errorMessage}");
                    StatusText = errorMessage ?? "VMDファイルを読み込めませんでした。";
                    continue; // 1件失敗しても残りの読み込みは継続する
                }

                AppendLog($"{fileName} 読み込み完了");
                Items.Add(new LoadedVmdItem(path, vmd));
            }

            StatusText = Items.Count > 0 ? "準備完了" : "読み込めるVMDファイルがありませんでした。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // リスト操作（削除・並び替え）
    // ------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        if (SelectedItem == null) return;
        Items.Remove(SelectedItem);
        SelectedItem = null;
        AppendLog("選択したVMDファイルをリストから削除しました。");
    }

    private bool CanRemoveSelected() => SelectedItem != null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private void ClearAll()
    {
        Items.Clear();
        SelectedItem = null;
        AppendLog("すべてのVMDファイルをリストから削除しました。");
    }

    private bool CanClearAll() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedItem == null) return;
        int index = Items.IndexOf(SelectedItem);
        if (index <= 0) return;
        Items.Move(index, index - 1);
    }

    private bool CanMoveUp() => SelectedItem != null && Items.IndexOf(SelectedItem) > 0 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedItem == null) return;
        int index = Items.IndexOf(SelectedItem);
        if (index < 0 || index >= Items.Count - 1) return;
        Items.Move(index, index + 1);
    }

    private bool CanMoveDown() => SelectedItem != null && Items.IndexOf(SelectedItem) >= 0
        && Items.IndexOf(SelectedItem) < Items.Count - 1 && !IsBusy;

    private void RenumberItems()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].DisplayIndex = i + 1;
        }
    }

    // ------------------------------------------------------------------
    // 出力先選択
    // ------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanBrowseOutput))]
    private async Task BrowseOutputAsync()
    {
        if (_windowHandle == 0) return;

        var picker = new FileSavePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, _windowHandle);
        picker.SuggestedFileName = "combined";
        picker.FileTypeChoices.Add("VMDファイル", new List<string> { ".vmd" });
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

        StorageFile? file;
        try
        {
            file = await picker.PickSaveFileAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"保存先選択エラー：{ex.Message}");
            return;
        }

        if (file != null)
        {
            OutputPath = file.Path;
        }
    }

    private bool CanBrowseOutput() => !IsBusy;

    // ------------------------------------------------------------------
    // 結合処理
    // ------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        if (Items.Count == 0 || string.IsNullOrWhiteSpace(OutputPath)) return;

        IsBusy = true;
        ProgressValue = 0;
        StatusText = "処理中...";
        AppendLog("VMD結合開始");

        List<VmdFile> sourceFiles = Items.Select(i => i.Vmd).ToList();
        string outputPath = OutputPath;

        try
        {
            VmdMergeResult? result = null;
            string? errorMessage = null;

            await Task.Run(() =>
            {
                try
                {
                    result = VmdMerger.Merge(
                        sourceFiles,
                        log: line => EnqueueUi(() => AppendLog(line)),
                        fileProgress: (done, total) => EnqueueUi(() =>
                        {
                            ProgressValue = total > 0 ? (double)done / total * 50.0 : 0;
                            StatusText = $"処理中...\n\nファイル：{done} / {total}";
                        }));
                }
                catch (VmdFormatException ex)
                {
                    errorMessage = ex.Message;
                }
                catch (OverflowException)
                {
                    errorMessage = "フレーム番号が上限を超えました。結合するファイル数またはモーションサイズを減らしてください。";
                }
                catch (OutOfMemoryException)
                {
                    errorMessage = "処理に必要なメモリが不足しています。\nファイル数またはモーションサイズを減らしてください。";
                }
            });

            if (result == null)
            {
                StatusText = errorMessage ?? "結合処理に失敗しました。";
                AppendLog($"エラー：{errorMessage}");
                return;
            }

            AppendLog("フレームオフセット計算完了");
            AppendLog($"結合後ボーンフレーム：{result.BoneFrames.Count:N0}");
            AppendLog($"結合後モーフフレーム：{result.MorphFrames.Count:N0}");

            bool writeSucceeded = true;
            string? writeError = null;

            await Task.Run(() =>
            {
                try
                {
                    VmdWriter.Write(
                        outputPath,
                        result.ModelName,
                        result.ModelNameByteLength,
                        result.BoneFrames,
                        result.MorphFrames,
                        progress: ratio => EnqueueUi(() =>
                        {
                            ProgressValue = 50.0 + ratio * 50.0;
                            StatusText = $"処理中...\n\nフレーム処理：{ratio * 100:F0}%";
                        }));
                }
                catch (VmdFormatException ex)
                {
                    writeSucceeded = false;
                    writeError = ex.Message;
                }
                catch (Exception ex)
                {
                    writeSucceeded = false;
                    writeError = $"VMDファイルを保存できませんでした。\n{ex.Message}";
                }
            });

            if (!writeSucceeded)
            {
                StatusText = writeError ?? "VMDファイルを保存できませんでした。";
                AppendLog($"エラー：{writeError}");
                return;
            }

            ProgressValue = 100;
            AppendLog("出力完了");
            StatusText = $"結合が完了しました。\n\n出力ファイル：\n{outputPath}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanMerge() => Items.Count > 0 && !string.IsNullOrWhiteSpace(OutputPath) && !IsBusy;

    // ------------------------------------------------------------------
    // 共通ヘルパー
    // ------------------------------------------------------------------

    private void EnqueueUi(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }

    private void AppendLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogEntries.Add($"[{timestamp}] {message}");
    }
}
