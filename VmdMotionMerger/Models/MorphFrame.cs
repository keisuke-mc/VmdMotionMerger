namespace VmdMotionMerger.Models;

/// <summary>
/// VMD のモーフ（表情）モーションフレーム 1件分（バイナリ上は23バイト固定長）。
/// </summary>
public sealed class MorphFrame
{
    /// <summary>モーフ名（Shift-JIS, 15バイト固定長領域）</summary>
    public string MorphName { get; set; } = string.Empty;

    /// <summary>フレーム番号（0起算）</summary>
    public uint FrameNumber { get; set; }

    /// <summary>ウェイト値（0.0〜1.0）</summary>
    public float Weight { get; set; }

    public MorphFrame Clone(uint newFrameNumber)
    {
        return new MorphFrame
        {
            MorphName = MorphName,
            FrameNumber = newFrameNumber,
            Weight = Weight
        };
    }
}
