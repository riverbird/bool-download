using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;

namespace BoolDownload.Services;

/// <summary>界面语言选项。</summary>
public enum AppLanguage
{
    FollowSystem,
    Chinese,
    English,
}

/// <summary>
/// 中英文文案表。所有文案集中在此，键同时作为 XAML 的 DynamicResource 资源键，
/// 切换语言时重写 Application.Resources，界面无需重建即可刷新。
/// </summary>
public static class Localization
{
    /// <summary>候选语言（含“跟随系统语言”）。</summary>
    public static readonly AppLanguage[] LanguageOptions =
    {
        AppLanguage.FollowSystem,
        AppLanguage.Chinese,
        AppLanguage.English,
    };

    private static readonly Dictionary<string, string[]> Entries = new()
    {
        // 菜单
        ["Loc_Menu_File"] = new[] { "文件", "File" },
        ["Loc_Menu_Tools"] = new[] { "工具", "Tools" },
        ["Loc_Menu_Help"] = new[] { "帮助", "Help" },
        ["Loc_Menu_NewDownload"] = new[] { "新建下载...", "New download..." },
        ["Loc_Menu_NewMagnet"] = new[] { "新建磁力链接下载...", "New magnet link download..." },
        ["Loc_Menu_OpenTorrent"] = new[] { "打开BT种子...", "Open torrent..." },
        ["Loc_Menu_Exit"] = new[] { "退出", "Exit" },
        ["Loc_Menu_Accelerator"] = new[] { "SourceForge/Github加速转换...", "SourceForge/Github accelerator..." },
        ["Loc_Menu_About"] = new[] { "关于...", "About..." },
        ["Loc_Menu_CheckUpdate"] = new[] { "检查更新", "Check for updates" },
        ["Loc_Menu_Options"] = new[] { "选项...", "Options..." },

        // 工具栏
        ["Loc_Tb_NewDownload"] = new[] { "新建下载", "New download" },
        ["Loc_Tb_Magnet"] = new[] { "磁力链接", "Magnet link" },
        ["Loc_Tb_Torrent"] = new[] { "BT下载", "Torrent" },
        ["Loc_Tb_Start"] = new[] { "开始", "Resume" },
        ["Loc_Tb_Pause"] = new[] { "暂停", "Pause" },
        ["Loc_Tb_Delete"] = new[] { "删除", "Delete" },
        ["Loc_Tb_Properties"] = new[] { "属性", "Properties" },
        ["Loc_Tip_NewMagnet"] = new[] { "新建磁力链接下载", "New magnet link download" },
        ["Loc_Tip_NewTorrent"] = new[] { "打开BT种子下载", "Open a torrent file to download" },

        // 侧栏
        ["Loc_Pane_Title"] = new[] { "下载管理", "Downloads" },
        ["Loc_Pane_Subtitle"] = new[] { "极速下载，轻松管理", "Fast downloads, easy management" },

        // 表格列
        ["Loc_Col_Status"] = new[] { "状态", "Status" },
        ["Loc_Col_Name"] = new[] { "名称", "Name" },
        ["Loc_Col_Done"] = new[] { "完成", "Done" },
        ["Loc_Col_Size"] = new[] { "大小", "Size" },
        ["Loc_Col_Progress"] = new[] { "进度(%)", "Progress (%)" },
        ["Loc_Col_Speed"] = new[] { "速度", "Speed" },
        ["Loc_Col_AddedTime"] = new[] { "添加时间", "Added" },
        ["Loc_Col_Path"] = new[] { "文件 / 文件夹", "File / Folder" },

        // 右键菜单
        ["Loc_Ctx_DeleteAndFiles"] = new[] { "删除条目及文件", "Delete and its files" },
        ["Loc_Ctx_OpenFile"] = new[] { "打开文件", "Open file" },
        ["Loc_Ctx_OpenFolder"] = new[] { "打开保存文件夹", "Open folder" },
        ["Loc_Ctx_Redownload"] = new[] { "重新下载", "Redownload" },
        ["Loc_Ctx_CopyLink"] = new[] { "复制下载链接", "Copy download link" },

        // 通用按钮
        ["Loc_Btn_Ok"] = new[] { "确定", "OK" },
        ["Loc_Btn_Cancel"] = new[] { "取消", "Cancel" },
        ["Loc_Btn_Close"] = new[] { "关闭", "Close" },
        ["Loc_Btn_Delete"] = new[] { "删除", "Delete" },
        ["Loc_Btn_Upgrade"] = new[] { "升级", "Upgrade" },
        ["Loc_Btn_Convert"] = new[] { "转换链接", "Convert link" },
        ["Loc_Btn_Copy"] = new[] { "复制链接", "Copy link" },
        ["Loc_Btn_Download"] = new[] { "立即下载", "Download now" },

        // 表单字段
        ["Loc_Field_Url"] = new[] { "下载链接", "Download link" },
        ["Loc_Field_FileName"] = new[] { "文件名", "File name" },
        ["Loc_Field_MaxConnections"] = new[] { "最大连接数", "Max connections" },
        ["Loc_Field_SavePath"] = new[] { "保存位置", "Save in" },
        ["Loc_Field_Engine"] = new[] { "下载引擎", "Download engine" },
        ["Loc_Field_Magnet"] = new[] { "磁力链接", "Magnet link" },
        ["Loc_Field_TaskName"] = new[] { "任务名称", "Task name" },
        ["Loc_Field_TorrentFile"] = new[] { "种子文件", "Torrent file" },
        ["Loc_Tip_ChooseFolder"] = new[] { "选择保存目录", "Choose save folder" },
        ["Loc_Tip_ChooseSavePath"] = new[] { "选择保存位置", "Choose save location" },
        ["Loc_Ph_Url"] = new[]
        {
            "支持http://,https://,ftp://,ftps://,thunder://下载链接",
            "Supports http://, https://, ftp://, ftps://, thunder:// links",
        },
        ["Loc_Ph_AutoName"] = new[] { "留空自动获取", "Leave empty to detect automatically" },
        ["Loc_Ph_AcceleratorUrl"] = new[]
        {
            "输入下载链接，例如：https://github.com/user/repo/releases/download/...",
            "Enter a download link, e.g. https://github.com/user/repo/releases/download/...",
        },

        // 对话框标题
        ["Loc_Title_NewDownload"] = new[] { "创建新下载", "New download" },
        ["Loc_Title_NewMagnet"] = new[] { "创建磁力链接下载", "New magnet link download" },
        ["Loc_Title_Torrent"] = new[] { "BT下载", "Torrent download" },
        ["Loc_Title_MagnetInfo"] = new[] { "磁力链接下载信息", "Magnet download information" },
        ["Loc_Title_Properties"] = new[] { "属性", "Properties" },
        ["Loc_Title_About"] = new[] { "关于 BoolDownload", "About BoolDownload" },
        ["Loc_Title_DeleteConfirm"] = new[] { "删除下载任务", "Delete download task" },
        ["Loc_Title_Accelerator"] = new[] { "文件加速", "File accelerator" },
        ["Loc_Title_Options"] = new[] { "选项", "Options" },

        // 删除确认
        ["Loc_Del_Question"] = new[] { "确定要删除选中的下载任务吗？", "Delete the selected download task?" },
        ["Loc_Del_Hint"] = new[]
        {
            "删除后将同时删除该任务已下载的文件，且无法恢复。",
            "The downloaded files are deleted too. This cannot be undone.",
        },

        // 磁力 / 种子文件列表
        ["Loc_Info_Files"] = new[] { "包含的文件及文件夹", "Files and folders" },

        // 关于
        ["Loc_About_SoftwareInfo"] = new[] { "软件信息", "Software information" },
        ["Loc_About_Product"] = new[] { "产品名称", "Product" },
        ["Loc_About_Version"] = new[] { "版本", "Version" },
        ["Loc_About_Copyright"] = new[] { "版权", "Copyright" },
        ["Loc_About_License"] = new[] { "许可证", "License" },
        ["Loc_About_Engine"] = new[] { "引擎", "Engine" },
        ["Loc_About_ThirdParty"] = new[] { "第三方库", "Third-party libraries" },
        ["Loc_About_System"] = new[] { "系统运行环境", "System" },
        ["Loc_About_Os"] = new[] { "操作系统", "Operating system" },
        ["Loc_About_Runtime"] = new[] { ".NET 运行时", ".NET runtime" },
        ["Loc_About_Arch"] = new[] { "体系结构", "Architecture" },
        ["Loc_About_WorkDir"] = new[] { "工作目录", "Working directory" },
        ["Loc_About_UpdateLog"] = new[] { "更新日志", "Changelog" },

        // 文件加速
        ["Loc_Acc_Type"] = new[] { "加速类型", "Acceleration type" },
        ["Loc_Acc_SourceForge"] = new[] { "SourceForge加速", "SourceForge accelerator" },
        ["Loc_Acc_Github"] = new[] { "Github加速", "Github accelerator" },
        ["Loc_Acc_CfV4V6"] = new[]
        {
            "Cloudflare (v4/v6) 优选加速服务器，支持 IPv6/IPv4 网络智能解析。",
            "Cloudflare (v4/v6) premium nodes with IPv6/IPv4 smart resolution.",
        },
        ["Loc_Acc_CfV4"] = new[]
        {
            "Cloudflare (v4)推荐 优选加速服务器，仅支持IPv4 网络智能解析。",
            "Cloudflare (v4) recommended premium nodes with IPv4 smart resolution.",
        },
        ["Loc_Acc_Fastly"] = new[]
        {
            "Fastly (v4) Fastly CDN 节点加速。",
            "Fastly (v4) acceleration through Fastly CDN nodes.",
        },
        ["Loc_Acc_CfMain"] = new[]
        {
            "cloudflare (v4) 主站加速，全球高速分发！国内优选和IPv6网络支持请使用优选服务器。",
            "Cloudflare (v4) main-site acceleration with global delivery. "
            + "Use the premium nodes for mainland China or IPv6.",
        },

        // 选项
        ["Loc_Opt_PageUI"] = new[] { "界面", "Interface" },
        ["Loc_Opt_Language"] = new[] { "界面语言", "Language" },
        ["Loc_Opt_Theme"] = new[] { "主题色", "Theme" },
        ["Loc_Lang_FollowSystem"] = new[] { "跟随系统语言", "Follow system language" },
        ["Loc_Lang_Chinese"] = new[] { "中文", "Chinese" },
        ["Loc_Lang_English"] = new[] { "英文", "English" },
        ["Loc_Theme_Light"] = new[] { "浅色", "Light" },
        ["Loc_Theme_Dark"] = new[] { "深色", "Dark" },
        ["Loc_Opt_Mica"] = new[] { "启用毛玻璃效果(试验性)", "Enable mica effect (experimental)" },

        // 状态（内部状态值保持中文，展示时经由此表翻译）
        ["Loc_Status_Creating"] = new[] { "创建中", "Creating" },
        ["Loc_Status_Waiting"] = new[] { "等待", "Waiting" },
        ["Loc_Status_FetchingMetadata"] = new[] { "获取元数据中", "Fetching metadata" },
        ["Loc_Status_Starting"] = new[] { "启动中", "Starting" },
        ["Loc_Status_StartFailed"] = new[] { "启动失败", "Start failed" },
        ["Loc_Status_Downloading"] = new[] { "下载中", "Downloading" },
        ["Loc_Status_Pausing"] = new[] { "暂停中", "Pausing" },
        ["Loc_Status_Paused"] = new[] { "已暂停", "Paused" },
        ["Loc_Status_Completed"] = new[] { "已完成", "Completed" },
        ["Loc_Status_DownloadFailed"] = new[] { "下载失败", "Download failed" },
        ["Loc_Status_CreateFailed"] = new[] { "创建失败", "Create failed" },
        ["Loc_Status_LoginFailed"] = new[] { "登录失败", "Login failed" },
        ["Loc_Status_ResumeFailed"] = new[] { "恢复失败", "Resume failed" },
        ["Loc_Status_Deleted"] = new[] { "已删除", "Deleted" },
        ["Loc_Status_Failed"] = new[] { "失败", "Failed" },
        ["Loc_Status_Trash"] = new[] { "回收站", "Recycle bin" },

        // 下载引擎名称
        ["Loc_Engine_Xunlei"] = new[] { "迅雷开放下载引擎", "Xunlei download engine" },
        ["Loc_Engine_Native"] = new[] { "原生下载引擎(HttpClient)", "Native engine (HttpClient)" },
        ["Loc_Engine_Downloader"] = new[] { "原生下载引擎(Downloader)", "Native engine (Downloader)" },

        // 运行时提示
        ["Loc_Msg_UpdateLatest"] = new[] { "您现在使用的是最新版本!", "You are already using the latest version." },
        ["Loc_Msg_UpdateParseFailed"] = new[] { "更新信息解析失败。", "Failed to parse the update information." },
        ["Loc_Msg_UnsupportedOs"] = new[] { "不支持的操作系统或架构。", "Unsupported operating system or architecture." },
        ["Loc_Msg_NoPlatformUpdate"] = new[]
        {
            "未找到对应平台的更新信息。",
            "No update information found for this platform.",
        },
        ["Loc_Msg_CheckUpdateFailed"] = new[] { "获取更新信息失败: ", "Failed to load update information: " },
        ["Loc_Msg_NewVersionAvailable"] = new[] { "有新版本可用", "A new version is available" },
        ["Loc_Msg_MagnetTaskName"] = new[] { "磁力链接任务", "Magnet link task" },
        ["Loc_Picker_TorrentTitle"] = new[] { "打开BT种子文件", "Open torrent file" },
        ["Loc_Picker_TorrentFilter"] = new[] { "BT种子文件", "Torrent files" },
    };

    /// <summary>内部状态值（中文）到文案键的映射。</summary>
    private static readonly Dictionary<string, string> StatusKeys = new()
    {
        ["创建中"] = "Loc_Status_Creating",
        ["等待"] = "Loc_Status_Waiting",
        ["获取元数据中"] = "Loc_Status_FetchingMetadata",
        ["启动中"] = "Loc_Status_Starting",
        ["启动失败"] = "Loc_Status_StartFailed",
        ["下载中"] = "Loc_Status_Downloading",
        ["暂停中"] = "Loc_Status_Pausing",
        ["已暂停"] = "Loc_Status_Paused",
        ["已完成"] = "Loc_Status_Completed",
        ["下载失败"] = "Loc_Status_DownloadFailed",
        ["创建失败"] = "Loc_Status_CreateFailed",
        ["登录失败"] = "Loc_Status_LoginFailed",
        ["恢复失败"] = "Loc_Status_ResumeFailed",
        ["已删除"] = "Loc_Status_Deleted",
        ["失败"] = "Loc_Status_Failed",
        ["回收站"] = "Loc_Status_Trash",
    };

    private static int _index;

    /// <summary>用户选择的语言（可能是“跟随系统语言”）。</summary>
    public static AppLanguage Requested { get; private set; } = AppLanguage.Chinese;

    /// <summary>解析后的实际界面语言。</summary>
    public static bool IsEnglish => _index == 1;

    public static event EventHandler? LanguageChanged;

    /// <summary>取文案；未知键直接返回键名，便于定位遗漏。</summary>
    public static string T(string key) =>
        Entries.TryGetValue(key, out var value) ? value[_index] : key;

    /// <summary>把内部状态值翻译为当前语言的显示文本，未收录的状态原样返回。</summary>
    public static string StatusText(string status) =>
        StatusKeys.TryGetValue(status, out var key) ? T(key) : status;

    /// <summary>语言选项在下拉框中的显示文本。</summary>
    public static string LanguageLabel(AppLanguage language) => language switch
    {
        AppLanguage.FollowSystem => T("Loc_Lang_FollowSystem"),
        AppLanguage.English => T("Loc_Lang_English"),
        _ => T("Loc_Lang_Chinese"),
    };

    public static void Apply(AppLanguage language)
    {
        Requested = language;
        _index = ResolveToEnglish(language) ? 1 : 0;

        var resources = Application.Current?.Resources;
        if (resources is null) return;

        foreach (var entry in Entries)
            resources[entry.Key] = entry.Value[_index];

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    private static bool ResolveToEnglish(AppLanguage language) => language switch
    {
        AppLanguage.English => true,
        AppLanguage.Chinese => false,
        _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase),
    };
}
