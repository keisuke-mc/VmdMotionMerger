using System;
using System.Text;

namespace VmdMotionMerger.Services;

/// <summary>
/// VMDのバイナリ形式で使われる「固定長・null終端・Shift-JIS」文字列の読み書きヘルパー。
/// </summary>
internal static class VmdBinaryUtil
{
    /// <summary>
    /// 固定長バイト領域から、最初の 0x00 までを文字列として読み取る。
    /// MMD系ツールが吐き出すVMDは、null終端後にゴミバイトが残っている場合があるため、
    /// 0x00 以降は無視する。
    /// </summary>
    public static string ReadFixedString(byte[] buffer, int offset, int length, Encoding encoding)
    {
        int nullIndex = -1;
        for (int i = 0; i < length; i++)
        {
            if (buffer[offset + i] == 0x00)
            {
                nullIndex = i;
                break;
            }
        }

        int actualLength = nullIndex >= 0 ? nullIndex : length;
        if (actualLength <= 0) return string.Empty;

        try
        {
            return encoding.GetString(buffer, offset, actualLength);
        }
        catch (Exception)
        {
            // 文字コードとして解釈できないゴミデータが含まれていた場合は、
            // 結合処理自体は継続できるよう空文字列にフォールバックする。
            return string.Empty;
        }
    }

    /// <summary>
    /// 文字列を指定バイト長のバッファへエンコードする。長すぎる場合は切り詰め、
    /// 短い場合は残りを 0x00 で埋める。
    /// </summary>
    public static byte[] WriteFixedString(string text, int length, Encoding encoding)
    {
        var result = new byte[length];
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        byte[] bytes = encoding.GetBytes(text);
        int copyLength = Math.Min(bytes.Length, length);
        Array.Copy(bytes, result, copyLength);
        return result;
    }
}
