using System.IO;

namespace MechabellumModManager.Services;

/// <summary>
/// Copy to dest succeeded but deleting the source failed — both directories exist.
/// Journal / emergency recover can offer to pick a canonical copy.
/// </summary>
public sealed class PartialDirectoryMoveException : IOException
{
    public string SourcePath { get; }
    public string DestPath { get; }

    public PartialDirectoryMoveException(string sourcePath, string destPath, Exception inner)
        : base(
            "已复制到目标目录，但删除原目录失败（仍有文件被占用）。请手动确认后重试或重启后再试。\n"
            + "原始信息：" + inner.Message,
            inner)
    {
        SourcePath = sourcePath;
        DestPath = destPath;
    }
}
