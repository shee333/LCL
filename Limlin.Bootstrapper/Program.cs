using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Limlin.Bootstrapper;

/// <summary>
///     栎木栎林社区启动器的自解压引导程序。
///     <para>
///         最终 exe 的字节结构为：<c>[本引导程序][payload.zip][int64 载荷长度][8 字节魔数]</c>。
///         首次运行时把内嵌的载荷解压到 <c>%LOCALAPPDATA%\Limlin\Launcher\&lt;版本&gt;\</c>，
///         之后直接启动缓存中的主程序，不再重复解压。
///     </para>
///     <para>
///         主程序以「便携模式」运行：通过 <c>PCL_DATA_DIR</c> 把数据目录固定在引导程序 exe 旁边的 <c>PCL</c> 目录，
///         避免数据跑到 <c>%LOCALAPPDATA%</c> 里让用户找不到。
///     </para>
///     <para>调试用环境变量：<c>LIMLIN_APP_DIR</c> 可覆盖主程序的解压位置。</para>
/// </summary>
internal static class Program
{
    /// <summary>载荷尾部魔数，用于校验内嵌数据是否完整。</summary>
    private static readonly byte[] PayloadMagic = "LMLNSFX1"u8.ToArray();

    /// <summary>尾部结构长度：8 字节长度 + 8 字节魔数。</summary>
    private const int TrailerSize = 16;

    private static string _logPath = string.Empty;

    [STAThread]
    private static int Main()
    {
        try
        {
            var selfPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(selfPath))
                throw new InvalidOperationException("无法获取自身路径");

            _logPath = Path.Combine(Path.GetDirectoryName(selfPath)!, "Limlin.Bootstrapper.log");
            Log($"启动引导程序：{selfPath}");

            var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var overrideDir = Environment.GetEnvironmentVariable("LIMLIN_APP_DIR");
            var cacheRoot = !string.IsNullOrWhiteSpace(overrideDir)
                ? Path.GetFullPath(overrideDir)
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Limlin", "Launcher", version);
            Log($"版本={version} 缓存目录={cacheRoot}");

            if (string.IsNullOrWhiteSpace(cacheRoot))
                throw new InvalidOperationException("无法确定解压目录");

            var appExe = Path.Combine(cacheRoot, "PCL.Launcher.exe");
            if (!File.Exists(appExe))
            {
                Log("缓存不存在，开始解压载荷");
                ExtractPayload(selfPath, cacheRoot);
                Log("解压完成");
            }
            else
            {
                Log("缓存已存在，跳过解压");
            }

            if (!File.Exists(appExe))
                throw new FileNotFoundException("解压后仍未找到启动器主程序", appExe);

            // 便携模式：数据目录固定放在引导程序所在目录下
            var dataDir = Path.Combine(Path.GetDirectoryName(selfPath)!, "PCL");
            Directory.CreateDirectory(dataDir);

            var startInfo = new ProcessStartInfo
            {
                FileName = appExe,
                WorkingDirectory = cacheRoot,
                UseShellExecute = false
            };
            startInfo.Environment["PCL_DATA_DIR"] = dataDir;

            var args = Environment.GetCommandLineArgs();
            for (var i = 1; i < args.Length; i++)
                startInfo.ArgumentList.Add(args[i]);

            var process = Process.Start(startInfo);
            Log($"已启动主程序 pid={process?.Id} 数据目录={dataDir}");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"失败：{ex}");
            ShowError($"启动器启动失败：{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}"
                      + $"详细日志：{_logPath}");
            return 1;
        }
    }

    /// <summary>把 exe 尾部内嵌的 zip 载荷解压到目标目录。</summary>
    private static void ExtractPayload(string selfPath, string targetDir)
    {
        using var file = File.OpenRead(selfPath);
        Log($"自身大小 {file.Length} 字节");
        if (file.Length <= TrailerSize)
            throw new InvalidDataException("文件不完整，可能下载或复制过程被截断");

        file.Seek(-TrailerSize, SeekOrigin.End);
        var trailer = new byte[TrailerSize];
        file.ReadExactly(trailer);

        if (!new ReadOnlySpan<byte>(trailer, 8, 8).SequenceEqual(PayloadMagic))
            throw new InvalidDataException("未找到内嵌载荷标记，文件可能已损坏");

        var payloadLength = BitConverter.ToInt64(trailer, 0);
        Log($"载荷长度 {payloadLength} 字节");
        if (payloadLength <= 0 || payloadLength > file.Length - TrailerSize)
            throw new InvalidDataException("内嵌载荷长度非法，文件可能已损坏");

        file.Seek(-TrailerSize - payloadLength, SeekOrigin.End);
        var buffer = new byte[payloadLength];
        file.ReadExactly(buffer);
        Log("载荷已读入内存，开始解压");

        // 先解压到临时目录再整体替换，避免中途失败留下半套文件
        var staging = targetDir + ".extracting";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        Log($"临时目录 {staging}");

        try
        {
            using (var stream = new MemoryStream(buffer, false))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                Log($"载荷内条目数 {archive.Entries.Count}");
                archive.ExtractToDirectory(staging, true);
            }

            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);

            var parent = Path.GetDirectoryName(targetDir);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            Directory.Move(staging, targetDir);
            Log($"已解压到 {targetDir}");
        }
        catch
        {
            try
            {
                Directory.Delete(staging, true);
            }
            catch
            {
                // 清理失败不影响错误上报
            }

            throw;
        }
    }

    private static void Log(string message)
    {
        if (string.IsNullOrEmpty(_logPath)) return;
        try
        {
            File.AppendAllText(
                _logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // 写日志失败不应影响启动
        }
    }

    private static void ShowError(string message)
    {
        const uint iconError = 0x00000010;
        _ = MessageBoxW(IntPtr.Zero, message, "栎木栎林社区启动器", iconError);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
