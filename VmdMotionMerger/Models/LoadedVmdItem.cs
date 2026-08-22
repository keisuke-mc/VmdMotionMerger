using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VmdMotionMerger.Models;

/// <summary>
/// 読み込み済みVMDファイル1件分。ファイルリスト（ListView）の1行に対応する。
/// </summary>
public sealed partial class LoadedVmdItem : ObservableObject
{
    /// <summary>リスト上の表示順（1始まり）。並び替え・追加・削除のたびに再計算される。</summary>
    [ObservableProperty]
    private int _displayIndex;

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>"1. motion_01.vmd" のような表示用ラベル</summary>
    public string DisplayLabel => $"{DisplayIndex}. {FileName}";

    public VmdFile Vmd { get; }

    public int BoneFrameCount => Vmd.BoneFrames.Count;

    public int MorphFrameCount => Vmd.MorphFrames.Count;

    public uint MaxFrameNumber => Vmd.GetMaxFrameNumber();

    public string SummaryText => $"ボーン {BoneFrameCount:N0} / モーフ {MorphFrameCount:N0} フレーム　（モデル: {Vmd.ModelName}）";

    public LoadedVmdItem(string filePath, VmdFile vmd)
    {
        FilePath = filePath;
        Vmd = vmd;
    }

    partial void OnDisplayIndexChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
    }
}
