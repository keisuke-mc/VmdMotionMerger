namespace VmdMotionMerger.Models;

/// <summary>
/// VMD のボーンモーションフレーム 1件分（バイナリ上は111バイト固定長）。
/// </summary>
public sealed class BoneFrame
{
    /// <summary>ボーン名（Shift-JIS, 15バイト固定長領域）</summary>
    public string BoneName { get; set; } = string.Empty;

    /// <summary>フレーム番号（0起算）</summary>
    public uint FrameNumber { get; set; }

    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }

    /// <summary>回転（クォータニオン x, y, z, w）</summary>
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }

    /// <summary>補間曲線パラメータ（64バイト、内容は無編集でそのまま維持する）</summary>
    public byte[] Interpolation { get; set; } = new byte[64];

    public BoneFrame Clone(uint newFrameNumber)
    {
        return new BoneFrame
        {
            BoneName = BoneName,
            FrameNumber = newFrameNumber,
            PositionX = PositionX,
            PositionY = PositionY,
            PositionZ = PositionZ,
            RotationX = RotationX,
            RotationY = RotationY,
            RotationZ = RotationZ,
            RotationW = RotationW,
            // 補間曲線データは配列なので参照ではなく複製する
            Interpolation = (byte[])Interpolation.Clone()
        };
    }
}
