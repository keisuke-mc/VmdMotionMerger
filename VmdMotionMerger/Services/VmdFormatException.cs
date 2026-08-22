using System;

namespace VmdMotionMerger.Services;

/// <summary>
/// VMDファイルの検証エラー・結合条件エラーを表す例外。
/// メッセージはそのままステータス表示・ログ・エラーダイアログに使う想定。
/// </summary>
public sealed class VmdFormatException : Exception
{
    public VmdFormatException(string message) : base(message)
    {
    }

    public VmdFormatException(string message, Exception inner) : base(message, inner)
    {
    }
}
