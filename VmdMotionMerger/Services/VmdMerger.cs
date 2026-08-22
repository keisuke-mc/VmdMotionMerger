using System;
using System.Collections.Generic;
using System.Linq;
using VmdMotionMerger.Models;

namespace VmdMotionMerger.Services;

/// <summary>結合結果（書き出し前の中間データ）。</summary>
public sealed class VmdMergeResult
{
    public string ModelName { get; init; } = string.Empty;
    public int ModelNameByteLength { get; init; } = 20;
    public List<BoneFrame> BoneFrames { get; init; } = new();
    public List<MorphFrame> MorphFrames { get; init; } = new();
}

/// <summary>
/// 複数のVMDを指定順に連結する。
///
/// 仕様書 27章「推奨初期仕様」に基づく既定動作：
///   ・対象：ボーン＋モーフ
///   ・境界：前のVMDの最終フレームの直後へ次のVMDを配置（フレームをずらして完全連結）
///   ・クロスフェード：なし
///   ・モデル名：一致必須
///   ・重複フレーム：後のVMDを優先
/// </summary>
public static class VmdMerger
{
    public static VmdMergeResult Merge(
        IReadOnlyList<VmdFile> files,
        Action<string>? log = null,
        Action<int, int>? fileProgress = null)
    {
        if (files.Count == 0)
        {
            throw new InvalidOperationException("結合するVMDファイルがありません。");
        }

        // --- モデル名の一致チェック（推奨仕様：一致必須） ---
        string baseModelName = files[0].ModelName.Trim();
        foreach (VmdFile f in files)
        {
            if (f.ModelName.Trim() != baseModelName)
            {
                throw new VmdFormatException("モデル名が一致しないVMDファイルがあります。");
            }
        }

        int modelNameByteLength = files.Max(f => f.ModelNameByteLength);

        // 重複フレーム処理：同じ (名前, 出力フレーム番号) のキーが複数ファイルで発生した場合、
        // 後から結合されるVMD（＝リストで後ろにあるファイル）の値で上書きする。
        var boneMap = new Dictionary<(string Name, uint Frame), BoneFrame>();
        var morphMap = new Dictionary<(string Name, uint Frame), MorphFrame>();

        uint currentOffset = 0;

        for (int fi = 0; fi < files.Count; fi++)
        {
            VmdFile file = files[fi];
            log?.Invoke($"{file.ModelName} のフレームオフセット計算（開始フレーム：{currentOffset}）");

            foreach (BoneFrame b in file.BoneFrames)
            {
                uint newFrame = checked(b.FrameNumber + currentOffset);
                boneMap[(b.BoneName, newFrame)] = b.Clone(newFrame);
            }

            foreach (MorphFrame m in file.MorphFrames)
            {
                uint newFrame = checked(m.FrameNumber + currentOffset);
                morphMap[(m.MorphName, newFrame)] = m.Clone(newFrame);
            }

            uint maxFrame = file.GetMaxFrameNumber();
            currentOffset = checked(currentOffset + maxFrame + 1);

            fileProgress?.Invoke(fi + 1, files.Count);
        }

        List<BoneFrame> mergedBones = boneMap.Values
            .OrderBy(b => b.FrameNumber)
            .ThenBy(b => b.BoneName, StringComparer.Ordinal)
            .ToList();

        List<MorphFrame> mergedMorphs = morphMap.Values
            .OrderBy(m => m.FrameNumber)
            .ThenBy(m => m.MorphName, StringComparer.Ordinal)
            .ToList();

        return new VmdMergeResult
        {
            ModelName = files[0].ModelName,
            ModelNameByteLength = modelNameByteLength,
            BoneFrames = mergedBones,
            MorphFrames = mergedMorphs
        };
    }
}
