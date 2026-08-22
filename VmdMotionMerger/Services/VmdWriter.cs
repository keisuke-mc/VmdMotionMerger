using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VmdMotionMerger.Models;

namespace VmdMotionMerger.Services;

/// <summary>
/// 結合結果を正しいVMDバイナリ形式で書き出す。
/// カメラ・照明・セルフシャドウ・IK表示切替の各ブロックは、初期版では結合対象外のため
/// 件数0として出力する（他のMMD系ソフトが読み込む際に構造上問題が出ないようにするため）。
/// </summary>
public static class VmdWriter
{
    private const int HeaderTotalLength = 30;
    private const int BoneNameLength = 15;
    private const int MorphNameLength = 15;

    private static readonly Encoding ShiftJis = Encoding.GetEncoding("shift_jis");

    public static void Write(
        string filePath,
        string modelName,
        int modelNameByteLength,
        IReadOnlyList<BoneFrame> boneFrames,
        IReadOnlyList<MorphFrame> morphFrames,
        Action<double>? progress = null)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            using FileStream stream = File.Create(filePath);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

            string headerText = modelNameByteLength >= 20
                ? "Vocaloid Motion Data 0002"
                : "Vocaloid Motion Data file";

            writer.Write(VmdBinaryUtil.WriteFixedString(headerText, HeaderTotalLength, Encoding.ASCII));
            writer.Write(VmdBinaryUtil.WriteFixedString(modelName, modelNameByteLength, ShiftJis));

            writer.Write((uint)boneFrames.Count);

            long totalFrames = boneFrames.Count + morphFrames.Count;
            long processed = 0;
            const int progressReportInterval = 2000;

            for (int i = 0; i < boneFrames.Count; i++)
            {
                BoneFrame b = boneFrames[i];
                writer.Write(VmdBinaryUtil.WriteFixedString(b.BoneName, BoneNameLength, ShiftJis));
                writer.Write(b.FrameNumber);
                writer.Write(b.PositionX);
                writer.Write(b.PositionY);
                writer.Write(b.PositionZ);
                writer.Write(b.RotationX);
                writer.Write(b.RotationY);
                writer.Write(b.RotationZ);
                writer.Write(b.RotationW);
                writer.Write(b.Interpolation, 0, 64);

                processed++;
                if (progress != null && processed % progressReportInterval == 0 && totalFrames > 0)
                {
                    progress((double)processed / totalFrames);
                }
            }

            writer.Write((uint)morphFrames.Count);
            for (int i = 0; i < morphFrames.Count; i++)
            {
                MorphFrame m = morphFrames[i];
                writer.Write(VmdBinaryUtil.WriteFixedString(m.MorphName, MorphNameLength, ShiftJis));
                writer.Write(m.FrameNumber);
                writer.Write(m.Weight);

                processed++;
                if (progress != null && processed % progressReportInterval == 0 && totalFrames > 0)
                {
                    progress((double)processed / totalFrames);
                }
            }

            // 初期版では非対応の後続ブロック（カメラ／照明／セルフシャドウ／表示枠）を
            // 件数0として書き出し、VMDとしての構造を保つ。
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);

            progress?.Invoke(1.0);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new VmdFormatException(
                "VMDファイルを保存できませんでした。\n保存先のアクセス権を確認してください。", ex);
        }
        catch (IOException ex)
        {
            throw new VmdFormatException(
                "VMDファイルを保存できませんでした。\n保存先のアクセス権を確認してください。", ex);
        }
    }
}
