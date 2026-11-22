using PCL.Core.Utils;

namespace PCL;

/// <summary>
///     未接入自有更新源时使用的占位更新源。
///     <para>
///         不发起任何网络请求：永远报告“已是最新版本”，公告列表与下载任务列表均为空。
///         这样在 <see cref="Brand.AutoUpdateEnabled" /> 为 <see langword="false" /> 时，
///         无论从哪条调用路径进入更新逻辑（启动检查、更新页检查、手动安装、整合包导出），
///         都不会访问上游服务器。
///     </para>
/// </summary>
public sealed class UpdatesDisabledSource : IUpdateSource
{
    public string SourceName { get; set; } = "Local";

    public bool IsAvailable() => true;

    public bool RefreshCache() => true;

    public VersionDataModel GetLatestVersion(UpdateSourceChannel channel, UpdateArch arch) =>
        new()
        {
            VersionName = LauncherCommon.VersionBaseName,
            VersionCode = LauncherCommon.VersionCode,
            Changelog = string.Empty,
            Source = SourceName
        };

    public bool IsLatest(
        UpdateSourceChannel channel,
        UpdateArch arch,
        SemVer currentVersion,
        int currentVersionCode) => true;

    public VersionAnnouncementDataModel GetAnnouncementList() => new() { Content = [] };

    public List<Loading.LoaderBase> GetDownloadLoader(
        UpdateSourceChannel channel,
        UpdateArch arch,
        string output) => [];
}
