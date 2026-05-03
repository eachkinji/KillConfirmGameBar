using System;
using System.Collections.Generic;
using Windows.Globalization;
using Windows.Storage;

namespace TestXboxGameBar.Services
{
    public enum UiLanguage
    {
        English,
        SimplifiedChinese
    }

    public static class LocalizationManager
    {
        private const string SettingKey = "UiLanguage";
        private static UiLanguage _current = LoadLanguage();

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["MainTitle"] = "Kill Confirm Overlay",
            ["MainInstruction"] = "Use this guide to set up the Xbox Game Bar widget before playing.",
            ["MainShortcut"] = "Tip: press Win+G to open Xbox Game Bar. Open Kill Confirm Overlay from the widget menu.",
            ["GuideSetupTitle"] = "Setup checklist",
            ["GuidePin"] = "1. Press Win+G, open Kill Confirm Overlay, then pin it from the top-right.",
            ["GuideLights"] = "2. SVC and CFG both need to be green before playing.",
            ["GuideCfg"] = "3. Select the CS2 root folder if CFG is not ready. The app remembers it.",
            ["GuideTest"] = "4. Use the play button to test animation and sound.",
            ["GuideControlsTitle"] = "Panel controls",
            ["GuideVoice"] = "VOICE changes the sound pack.",
            ["GuideView"] = "VIEW adjusts window position, icon size, brightness, and contrast.",
            ["GuideService"] = "The companion process starts with the widget and closes with it.",
            ["GuidePreviewTitle"] = "Widget map",
            ["MockPin"] = "Pin the widget first",
            ["MockReady"] = "Ready when both are green",
            ["PinHint"] = "Pin the widget in the top-right to keep the icon on screen.",
            ["PinHintTooltip"] = "Click the pin button in Xbox Game Bar's top-right corner.",
            ["NeedBothLights"] = "Need both green lights before playing",
            ["ReadyBothLights"] = "Ready: service + CFG are green",
            ["WaitingGsi"] = "Waiting for CS2 data",
            ["ReadyAllSignals"] = "Ready: receiving CS2 data",
            ["StatusSvcReady"] = "SVC ready: service is running.",
            ["StatusSvcStarting"] = "SVC starting: waiting for service.",
            ["StatusSvcOffline"] = "SVC offline: start the service.",
            ["StatusCfgReady"] = "CFG ready: CS2 can send events.",
            ["StatusCfgChecking"] = "CFG checking: reading CS2 folder.",
            ["StatusCfgMissing"] = "CFG missing: add it before playing.",
            ["StatusCfgError"] = "CFG error: select the CS2 root folder.",
            ["StatusCfgSelect"] = "CFG not ready: select the CS2 root folder.",
            ["StatusGsiReady"] = "GSI ready: CS2 data is being received.",
            ["StatusGsiNeedsService"] = "GSI waiting: start SVC first.",
            ["StatusGsiWaiting"] = "GSI waiting: start CS2 or restart it after CFG.",
            ["StatusAniReady"] = "ANI ready: animations are preloaded.",
            ["StatusAniLoading"] = "ANI loading: ",
            ["StatusAniFailed"] = "ANI failed: animation preload failed.",
            ["VoiceLabel"] = "VOICE",
            ["CrossfireSwatGr"] = "swat GR",
            ["CrossfireSwatBl"] = "swat BL",
            ["CrossfireFlyingTigerGr"] = "tiger GR",
            ["CrossfireFlyingTigerBl"] = "tiger BL",
            ["CrossfireWomenGr"] = "women GR",
            ["CrossfireWomenBl"] = "women BL",
            ["CfgLabel"] = "CFG",
            ["TestLabel"] = "TEST",
            ["ViewLabel"] = "VIEW",
            ["Add"] = "Add",
            ["CfgNotChecked"] = "CFG not checked",
            ["SelectCsFolder"] = "Select CS2 folder",
            ["CfgFolderError"] = "CFG folder error",
            ["CfgFolderSaveError"] = "Could not save folder access.",
            ["CfgAutoDetecting"] = "Finding CS2",
            ["CfgChecking"] = "Checking CFG",
            ["CfgPathMissing"] = "CFG path missing",
            ["PickCsRoot"] = "Pick the CS2 root folder",
            ["CfgReady"] = "CFG ready",
            ["CfgMissing"] = "CFG missing",
            ["CfgCheckFailed"] = "CFG check failed",
            ["CfgAdding"] = "Adding CFG",
            ["CfgAddFailed"] = "CFG add failed",
            ["SelectCsFirst"] = "Select the CS2 folder first.",
            ["AddCfgQuestion"] = "The Game State Integration cfg was not found. Add it to the selected CS2 folder now?",
            ["AddCfgTitle"] = "Add KillConfirm cfg",
            ["Cancel"] = "Cancel",
            ["CfgWriteFailed"] = "Could not add the cfg. Make sure you selected the CS2 root folder and have write access.",
            ["CfgMessageTitle"] = "Kill Confirm CFG",
            ["StartServiceTooltip"] = "Start service",
            ["CheckServiceTooltip"] = "Check service",
            ["OpenGuideTooltip"] = "Open setup guide",
            ["OpenGuideFailed"] = "Open it from the Start menu",
            ["ServiceStatusTooltip"] = "Service status",
            ["CfgStatusTooltip"] = "CFG status",
            ["GsiStatusTooltip"] = "CS2 data status",
            ["AnimationCacheTooltip"] = "Animation cache status",
            ["AnimationCacheLoading"] = "Loading animations: ",
            ["AnimationCacheReady"] = "Animations ready",
            ["AnimationCacheFailed"] = "Animation preload failed",
            ["VoiceTooltip"] = "Select voice pack",
            ["SelectCsFolderTooltip"] = "Select CS2 root folder",
            ["CfgSelectRootHint"] = "Pick CS2 root folder",
            ["CfgDetectedNeedConfirm"] = "Found CS2. Click folder to confirm: ",
            ["CfgWrongFolderHint"] = "Wrong folder. Select the CS2 root folder, not cfg or csgo.",
            ["CfgSavedFolderPrefix"] = "Saved: ",
            ["AddMissingCfgTooltip"] = "Add missing CFG",
            ["TestPresetTooltip"] = "Select preview or test preset",
            ["PreviewTooltip"] = "Preview selected animation",
            ["SendTestButton"] = "Send service test",
            ["SendTestTooltip"] = "Send test event with sound",
            ["DefaultSizeTooltip"] = "Default size",
            ["CenterWindowTooltip"] = "Center window",
            ["LowerThirdTooltip"] = "Place icon near the lower fifth",
            ["MoveUpTooltip"] = "Move icon up",
            ["MoveDownTooltip"] = "Move icon down",
            ["ShrinkTooltip"] = "Shrink icon",
            ["EnlargeTooltip"] = "Enlarge icon",
            ["BrightnessTooltip"] = "Animation brightness",
            ["ContrastTooltip"] = "Animation contrast",
            ["ResetTooltip"] = "Reset visual adjustments",
            ["ServiceRunning"] = "Service running",
            ["ServiceStarting"] = "Service starting",
            ["ServiceOffline"] = "Service offline",
            ["OpenLogsTooltip"] = "Open logs folder",
            ["FreePortTooltip"] = "Close the process using the service port",
            ["FreePortRunning"] = "Freeing service port...",
            ["FreePortFailed"] = "Could not free port. Restart your PC and try again.",
            ["ServiceFailedGeneric"] = "Service failed. Restart your PC and try again.",
            ["ServiceFailedSeeLogs"] = "Service failed. Open logs for details.",
            ["ServicePortBlockedHint"] = "Service port blocked. Restart your PC and try again.",
            ["ServicePortInUseHint"] = "Service port in use. Click the port button, then start again.",
            ["GsiReceivingTooltip"] = "CS2 is sending data",
            ["GsiWaitingTooltip"] = "No CS2 data received yet. Restart CS2 after adding CFG.",
            ["GsiStaleTooltip"] = "CS2 data was received earlier, but not recently.",
            ["CfgReadyTooltip"] = "CFG ready: ",
            ["CfgMissingTooltip"] = "CFG missing: ",
            ["CheckingCfgTooltip"] = "Checking CFG",
            ["SelectCsRootTooltip"] = "Select the CS2 root folder"
        };

        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
        {
            ["MainTitle"] = "击杀确认悬浮窗",
            ["MainInstruction"] = "开始游戏前，按这个指引设置 Xbox Game Bar 小组件。",
            ["MainShortcut"] = "提示：按 Win+G 打开 Xbox Game Bar，然后从小组件菜单打开 Kill Confirm Overlay。",
            ["GuideSetupTitle"] = "设置步骤",
            ["GuidePin"] = "1. 按 Win+G，打开 Kill Confirm Overlay，然后点右上角固定。",
            ["GuideLights"] = "2. 开始游戏前，SVC 和 CFG 两盏灯都要变绿。",
            ["GuideCfg"] = "3. 如果 CFG 没就绪，选择 CS2 主文件夹。应用会记住这个位置。",
            ["GuideTest"] = "4. 用播放按钮测试动画和声音。",
            ["GuideControlsTitle"] = "面板功能",
            ["GuideVoice"] = "VOICE 用来切换语音包。",
            ["GuideView"] = "VIEW 用来调整窗口位置、图标大小、亮度和对比度。",
            ["GuideService"] = "后台伴随进程会随小组件启动，并随小组件关闭。",
            ["GuidePreviewTitle"] = "面板示意",
            ["MockPin"] = "先固定小组件",
            ["MockReady"] = "两盏灯都绿才就绪",
            ["PinHint"] = "点击右上角固定按钮，图标才会留在屏幕上。",
            ["PinHintTooltip"] = "点击 Xbox Game Bar 右上角的固定按钮。",
            ["NeedBothLights"] = "开始游戏前需要两盏灯都变绿",
            ["ReadyBothLights"] = "已就绪：服务和 CFG 都是绿色",
            ["WaitingGsi"] = "等待 CS2 数据",
            ["ReadyAllSignals"] = "已就绪：正在接收 CS2 数据",
            ["StatusSvcReady"] = "SVC 就绪：服务正在运行。",
            ["StatusSvcStarting"] = "SVC 启动中：正在等待服务。",
            ["StatusSvcOffline"] = "SVC 离线：请启动服务。",
            ["StatusCfgReady"] = "CFG 就绪：CS2 可以发送事件。",
            ["StatusCfgChecking"] = "CFG 检查中：正在读取 CS2 文件夹。",
            ["StatusCfgMissing"] = "CFG 缺失：开始游戏前请添加。",
            ["StatusCfgError"] = "CFG 错误：请选择 CS2 主文件夹。",
            ["StatusCfgSelect"] = "CFG 未就绪：请选择 CS2 主文件夹。",
            ["StatusGsiReady"] = "GSI 就绪：正在接收 CS2 数据。",
            ["StatusGsiNeedsService"] = "GSI 等待中：请先启动 SVC。",
            ["StatusGsiWaiting"] = "GSI 等待中：启动 CS2，或添加 CFG 后重启 CS2。",
            ["StatusAniReady"] = "ANI 就绪：动画已预载入。",
            ["StatusAniLoading"] = "ANI 载入中：",
            ["StatusAniFailed"] = "ANI 失败：动画预载入失败。",
            ["VoiceLabel"] = "语音",
            ["CrossfireSwatGr"] = "斯沃特语音 保卫者",
            ["CrossfireSwatBl"] = "斯沃特语音 潜伏者",
            ["CrossfireFlyingTigerGr"] = "飞虎队语音 保卫者",
            ["CrossfireFlyingTigerBl"] = "飞虎队语音 潜伏者",
            ["CrossfireWomenGr"] = "潘多拉语音 保卫者",
            ["CrossfireWomenBl"] = "潘多拉语音 潜伏者",
            ["CfgLabel"] = "配置",
            ["TestLabel"] = "测试",
            ["ViewLabel"] = "视图",
            ["Add"] = "添加",
            ["CfgNotChecked"] = "未检查 CFG",
            ["SelectCsFolder"] = "选择 CS2 文件夹",
            ["CfgFolderError"] = "CFG 文件夹错误",
            ["CfgFolderSaveError"] = "无法保存文件夹访问权限。",
            ["CfgAutoDetecting"] = "正在查找 CS2",
            ["CfgChecking"] = "正在检查 CFG",
            ["CfgPathMissing"] = "找不到 CFG 路径",
            ["PickCsRoot"] = "请选择 CS2 主文件夹",
            ["CfgReady"] = "CFG 已就绪",
            ["CfgMissing"] = "缺少 CFG",
            ["CfgCheckFailed"] = "CFG 检查失败",
            ["CfgAdding"] = "正在添加 CFG",
            ["CfgAddFailed"] = "CFG 添加失败",
            ["SelectCsFirst"] = "请先选择 CS2 文件夹。",
            ["AddCfgQuestion"] = "没有找到 Game State Integration cfg。现在添加到选择的 CS2 文件夹吗？",
            ["AddCfgTitle"] = "添加 KillConfirm cfg",
            ["Cancel"] = "取消",
            ["CfgWriteFailed"] = "无法添加 cfg。请确认选择的是 CS2 主文件夹，并且有写入权限。",
            ["CfgMessageTitle"] = "Kill Confirm CFG",
            ["StartServiceTooltip"] = "启动服务",
            ["CheckServiceTooltip"] = "检查服务",
            ["OpenGuideTooltip"] = "打开指引页",
            ["OpenGuideFailed"] = "请从开始菜单打开指引",
            ["ServiceStatusTooltip"] = "服务状态",
            ["CfgStatusTooltip"] = "CFG 状态",
            ["GsiStatusTooltip"] = "CS2 数据状态",
            ["AnimationCacheTooltip"] = "动画载入状态",
            ["AnimationCacheLoading"] = "正在载入动画：",
            ["AnimationCacheReady"] = "动画已准备好",
            ["AnimationCacheFailed"] = "动画预载入失败",
            ["VoiceTooltip"] = "选择语音包",
            ["SelectCsFolderTooltip"] = "选择 CS2 主文件夹",
            ["CfgSelectRootHint"] = "选择 CS2 主文件夹",
            ["CfgDetectedNeedConfirm"] = "找到 CS2，点文件夹确认：",
            ["CfgWrongFolderHint"] = "文件夹不对。请选择 CS2 主文件夹，不是 cfg 或 csgo。",
            ["CfgSavedFolderPrefix"] = "已保存：",
            ["AddMissingCfgTooltip"] = "添加缺失的 CFG",
            ["TestPresetTooltip"] = "选择预览或测试项目",
            ["PreviewTooltip"] = "预览选择的动画",
            ["SendTestButton"] = "发送服务测试",
            ["SendTestTooltip"] = "发送带声音的测试事件",
            ["DefaultSizeTooltip"] = "默认大小",
            ["CenterWindowTooltip"] = "窗口居中",
            ["LowerThirdTooltip"] = "把图标放到底部 1/5 位置",
            ["MoveUpTooltip"] = "图标上移",
            ["MoveDownTooltip"] = "图标下移",
            ["ShrinkTooltip"] = "缩小图标",
            ["EnlargeTooltip"] = "放大图标",
            ["BrightnessTooltip"] = "动画亮度",
            ["ContrastTooltip"] = "动画对比度",
            ["ResetTooltip"] = "重置视觉调整",
            ["ServiceRunning"] = "服务运行中",
            ["ServiceStarting"] = "服务启动中",
            ["ServiceOffline"] = "服务离线",
            ["OpenLogsTooltip"] = "打开日志文件夹",
            ["FreePortTooltip"] = "关闭占用服务端口的程序",
            ["FreePortRunning"] = "正在释放服务端口...",
            ["FreePortFailed"] = "无法释放端口，请重启电脑后再试。",
            ["ServiceFailedGeneric"] = "服务启动失败，请重启电脑后再试。",
            ["ServiceFailedSeeLogs"] = "服务启动失败，请打开日志查看原因。",
            ["ServicePortBlockedHint"] = "服务端口被阻止，请重启电脑后再试。",
            ["ServicePortInUseHint"] = "端口被占用，点端口按钮后再启动。",
            ["GsiReceivingTooltip"] = "CS2 正在发送数据",
            ["GsiWaitingTooltip"] = "还没收到 CS2 数据。添加 CFG 后请重启 CS2。",
            ["GsiStaleTooltip"] = "之前收到过 CS2 数据，但最近没有收到。",
            ["CfgReadyTooltip"] = "CFG 已就绪：",
            ["CfgMissingTooltip"] = "缺少 CFG：",
            ["CheckingCfgTooltip"] = "正在检查 CFG",
            ["SelectCsRootTooltip"] = "选择 CS2 主文件夹"
        };

        public static UiLanguage Current => _current;

        public static void SetLanguage(UiLanguage language)
        {
            _current = language;
            ApplicationData.Current.LocalSettings.Values[SettingKey] = language == UiLanguage.SimplifiedChinese
                ? "zh-CN"
                : "en-US";
        }

        public static string Text(string key)
        {
            Dictionary<string, string> table = _current == UiLanguage.SimplifiedChinese ? Chinese : English;
            if (table.TryGetValue(key, out string value))
            {
                return value;
            }

            return English.TryGetValue(key, out value) ? value : key;
        }

        private static UiLanguage LoadLanguage()
        {
            string saved = ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    ? UiLanguage.SimplifiedChinese
                    : UiLanguage.English;
            }

            foreach (string language in ApplicationLanguages.Languages)
            {
                if (language.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                    || language.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)
                    || language.StartsWith("zh-Hans-", StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.SimplifiedChinese;
                }

                if (!language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            return UiLanguage.English;
        }
    }
}
