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
            ["VoiceLabel"] = "VOICE",
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
            ["ServiceStatusTooltip"] = "Service status",
            ["CfgStatusTooltip"] = "CFG status",
            ["VoiceTooltip"] = "Select voice pack",
            ["SelectCsFolderTooltip"] = "Select CS2 root folder",
            ["CfgSelectRootHint"] = "Pick CS2 root folder",
            ["CfgDetectedNeedConfirm"] = "Found CS2. Click folder to confirm: ",
            ["CfgWrongFolderHint"] = "Wrong folder. Select the CS2 root folder, not cfg or csgo.",
            ["CfgSavedFolderPrefix"] = "Saved: ",
            ["AddMissingCfgTooltip"] = "Add missing CFG",
            ["TestPresetTooltip"] = "Select preview or test preset",
            ["PreviewTooltip"] = "Preview selected animation",
            ["SendTestTooltip"] = "Send test event with sound",
            ["DefaultSizeTooltip"] = "Default size",
            ["CenterWindowTooltip"] = "Center window",
            ["LowerThirdTooltip"] = "Place icon at lower third",
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
            ["VoiceLabel"] = "语音",
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
            ["ServiceStatusTooltip"] = "服务状态",
            ["CfgStatusTooltip"] = "CFG 状态",
            ["VoiceTooltip"] = "选择语音包",
            ["SelectCsFolderTooltip"] = "选择 CS2 主文件夹",
            ["CfgSelectRootHint"] = "选择 CS2 主文件夹",
            ["CfgDetectedNeedConfirm"] = "找到 CS2，点文件夹确认：",
            ["CfgWrongFolderHint"] = "文件夹不对。请选择 CS2 主文件夹，不是 cfg 或 csgo。",
            ["CfgSavedFolderPrefix"] = "已保存：",
            ["AddMissingCfgTooltip"] = "添加缺失的 CFG",
            ["TestPresetTooltip"] = "选择预览或测试项目",
            ["PreviewTooltip"] = "预览选择的动画",
            ["SendTestTooltip"] = "发送带声音的测试事件",
            ["DefaultSizeTooltip"] = "默认大小",
            ["CenterWindowTooltip"] = "窗口居中",
            ["LowerThirdTooltip"] = "把图标放到下三分之一位置",
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
