using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VmdMotionMerger.Models;

namespace VmdMotionMerger.Services;

/// <summary>
/// VMD（Vocaloid Motion Data）ファイルのバイナリパーサー。
/// 初期版の対応範囲：ヘッダー／モデル名／ボーンフレーム／モーフフレーム。
/// カメラ・照明・セルフシャドウ・IK表示切替 は仕様上の対象外のため読み飛ばす。
/// </summary>
public static class VmdParser
{
    private const int HeaderTotalLength = 30;
    private const int ModelNameLengthV1 = 10; // "Vocaloid Motion Data file" (旧形式)
    private const int ModelNameLengthV2 = 20; // "Vocaloid Motion Data 0002" (現行形式)
    private const int BoneNameLength = 15;
    private const int MorphNameLength = 15;
    private const int BoneFrameRecordSize = 111; // 15 + 4 + 12 + 16 + 64
    private const int MorphFrameRecordSize = 23; // 15 + 4 + 4

    private static readonly Encoding ShiftJis = Encoding.GetEncoding("shift_jis");

    /// <summary>
    /// VMDファイルを解析する。形式異常時は <see cref="VmdFormatException"/> を送出する。
    /// </summary>
    /// <param name="filePath">読み込むVMDファイルのパス</param>
    /// <param name="log">進行ログを1行ずつ受け取るコールバック（任意）</param>
    public static VmdFile Parse(string filePath, Action<string>? log = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("指定されたファイルが見つかりません。", filePath);
        }

        using FileStream stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        long fileSize = stream.Length;
        if (fileSize < HeaderTotalLength + ModelNameLengthV1)
        {
            throw new VmdFormatException(
                "このファイルは有効なVMDファイルではありません。（ファイルサイズが小さすぎます）");
        }
        // 数百GB級の壊れたファイルを誤って読み込まないよう、簡易的な上限チェックを行う。
        const long maxReasonableFileSize = 8L * 1024 * 1024 * 1024; // 8GB
        if (fileSize > maxReasonableFileSize)
        {
            throw new VmdFormatException(
                "このファイルは有効なVMDファイルではありません。（ファイルサイズが異常です）");
        }

        byte[] headerBytes = ReadExact(reader, HeaderTotalLength, "ヘッダー");
        string headerText = VmdBinaryUtil.ReadFixedString(headerBytes, 0, HeaderTotalLength, Encoding.ASCII);

        int modelNameLength;
        if (headerText.StartsWith("Vocaloid Motion Data 0002", StringComparison.Ordinal))
        {
            modelNameLength = ModelNameLengthV2;
        }
        else if (headerText.StartsWith("Vocaloid Motion Data file", StringComparison.Ordinal))
        {
            modelNameLength = ModelNameLengthV1;
        }
        else
        {
            throw new VmdFormatException(
                "このファイルは有効なVMDファイルではありません。（VMDヘッダーが不正です）");
        }

        byte[] modelNameBytes = ReadExact(reader, modelNameLength, "モデル名");
        string modelName = VmdBinaryUtil.ReadFixedString(modelNameBytes, 0, modelNameLength, ShiftJis);

        var vmd = new VmdFile
        {
            HeaderSignature = headerText,
            ModelNameByteLength = modelNameLength,
            ModelName = modelName
        };

        // --- ボーンフレーム ---
        uint boneCount = ReadUInt32(reader, "ボーンフレーム数");
        ValidateFrameCount(boneCount, BoneFrameRecordSize, stream, "ボーンフレーム");
        vmd.BoneFrames = new List<BoneFrame>(checked((int)boneCount));
        for (uint i = 0; i < boneCount; i++)
        {
            if (stream.Position + BoneFrameRecordSize > fileSize)
            {
                throw new VmdFormatException(
                    $"このファイルは有効なVMDファイルではありません。（ボーンフレームデータが途中で終わっています：{i}/{boneCount}）");
            }

            byte[] nameBytes = reader.ReadBytes(BoneNameLength);
            string boneName = VmdBinaryUtil.ReadFixedString(nameBytes, 0, BoneNameLength, ShiftJis);
            uint frameNo = reader.ReadUInt32();
            float px = reader.ReadSingle();
            float py = reader.ReadSingle();
            float pz = reader.ReadSingle();
            float rx = reader.ReadSingle();
            float ry = reader.ReadSingle();
            float rz = reader.ReadSingle();
            float rw = reader.ReadSingle();
            byte[] interpolation = reader.ReadBytes(64);

            vmd.BoneFrames.Add(new BoneFrame
            {
                BoneName = boneName,
                FrameNumber = frameNo,
                PositionX = px,
                PositionY = py,
                PositionZ = pz,
                RotationX = rx,
                RotationY = ry,
                RotationZ = rz,
                RotationW = rw,
                Interpolation = interpolation
            });
        }
        log?.Invoke($"ボーンフレーム：{vmd.BoneFrames.Count:N0}");

        // --- モーフフレーム ---
        uint morphCount = ReadUInt32(reader, "モーフフレーム数");
        ValidateFrameCount(morphCount, MorphFrameRecordSize, stream, "モーフフレーム");
        vmd.MorphFrames = new List<MorphFrame>(checked((int)morphCount));
        for (uint i = 0; i < morphCount; i++)
        {
            if (stream.Position + MorphFrameRecordSize > fileSize)
            {
                throw new VmdFormatException(
                    $"このファイルは有効なVMDファイルではありません。（モーフフレームデータが途中で終わっています：{i}/{morphCount}）");
            }

            byte[] nameBytes = reader.ReadBytes(MorphNameLength);
            string morphName = VmdBinaryUtil.ReadFixedString(nameBytes, 0, MorphNameLength, ShiftJis);
            uint frameNo = reader.ReadUInt32();
            float weight = reader.ReadSingle();

            vmd.MorphFrames.Add(new MorphFrame
            {
                MorphName = morphName,
                FrameNumber = frameNo,
                Weight = weight
            });
        }
        log?.Invoke($"モーフフレーム：{vmd.MorphFrames.Count:N0}");

        // カメラ／照明／セルフシャドウ／IK・表示切替のブロックが後続する場合があるが、
        // 初期版の結合対象外（仕様書 27章 推奨初期仕様）のため読み飛ばす。

        return vmd;
    }

    private static byte[] ReadExact(BinaryReader reader, int length, string fieldName)
    {
        byte[] buffer = reader.ReadBytes(length);
        if (buffer.Length < length)
        {
            throw new VmdFormatException(
                $"このファイルは有効なVMDファイルではありません。（{fieldName}を読み込めません）");
        }
        return buffer;
    }

    private static uint ReadUInt32(BinaryReader reader, string fieldName)
    {
        try
        {
            return reader.ReadUInt32();
        }
        catch (EndOfStreamException ex)
        {
            throw new VmdFormatException(
                $"このファイルは有効なVMDファイルではありません。（{fieldName}を読み込めません）", ex);
        }
    }

    private static void ValidateFrameCount(uint count, int recordSize, FileStream stream, string label)
    {
        // 件数フィールドが壊れていると非現実的に大きな値になり得るため、
        // 残りファイルサイズから見て明らかに不正な場合は早期にエラーとする。
        long remaining = stream.Length - stream.Position;
        double requiredBytes = (double)count * recordSize;
        if (requiredBytes > remaining)
        {
            throw new VmdFormatException(
                $"このファイルは有効なVMDファイルではありません。（{label}数がファイルサイズと矛盾しています）");
        }
    }
}
