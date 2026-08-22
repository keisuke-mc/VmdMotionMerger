using System.Collections.Generic;
using System.Linq;

namespace VmdMotionMerger.Models;

/// <summary>
/// 1つのVMDファイルから読み込んだ内容（初期版の対応範囲：ボーン＋モーフのみ）。
/// カメラ／照明／セルフシャドウ／IK・表示切替 は仕様上、初期版の結合対象外のため保持しない。
/// </summary>
public sealed class VmdFile
{
    /// <summary>ファイル先頭30バイトのヘッダー文字列（バージョン判定用）</summary>
    public string HeaderSignature { get; set; } = string.Empty;

    /// <summary>モデル名領域のバイト長（Ver.1=10 / Ver.2=20）</summary>
    public int ModelNameByteLength { get; set; } = 20;

    /// <summary>モデル名（Shift-JIS デコード済み）</summary>
    public string ModelName { get; set; } = string.Empty;

    public List<BoneFrame> BoneFrames { get; set; } = new();

    public List<MorphFrame> MorphFrames { get; set; } = new();

    /// <summary>
    /// このVMD内で使用されている最大フレーム番号（ボーン・モーフ双方を対象）。
    /// フレームが1件も無い場合は 0 を返す。
    /// </summary>
    public uint GetMaxFrameNumber()
    {
        uint max = 0;
        for (int i = 0; i < BoneFrames.Count; i++)
        {
            if (BoneFrames[i].FrameNumber > max) max = BoneFrames[i].FrameNumber;
        }
        for (int i = 0; i < MorphFrames.Count; i++)
        {
            if (MorphFrames[i].FrameNumber > max) max = MorphFrames[i].FrameNumber;
        }
        return max;
    }
}
