namespace PCL;

/// <summary>
///     品牌与分发相关常量。
///     <para>
///         所有对外标识（发行版名称、更新地址等）应统一从这里取值，避免散落在各处的硬编码字符串。
///     </para>
/// </summary>
public static class Brand
{
    /// <summary>
    ///     是否启用启动器在线更新。
    ///     <para>
    ///         关闭时：启动不检查更新、不拉取公告，更新页不发起任何网络请求，也不会下载并安装上游发行版。
    ///         在接入本社区自有更新源之前必须保持 <see langword="false" />，
    ///         否则用户点击更新后会被替换回上游版本，导致本社区的改动全部丢失。
    ///     </para>
    /// </summary>
    public const bool AutoUpdateEnabled = false;
}
