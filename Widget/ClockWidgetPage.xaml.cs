using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TestXboxGameBar.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Windows.System;

namespace TestXboxGameBar
{
    public sealed partial class ClockWidgetPage : Page
    {
        private static readonly Size DefaultWidgetSize = new Size(324, 190);
        private static readonly Size MinWidgetSize = new Size(160, 120);
        private static readonly Size MaxWidgetSize = new Size(720, 720);
        private const double AnimationOffsetStep = 12.0;
        private const double MaxAnimationOffsetRatio = 0.45;
        private const double BottomFifthAnimationOffsetRatio = 0.30;
        private const double ScaleUpFactor = 1.1;
        private const double ScaleDownFactor = 0.9;
        private const int StartupPreloadDelayMs = 250;
        private const int StreakBadgeDisplayDurationMs = 1800;
        private const int DefaultPreviewKillCount = 2;
        private const double DefaultBrightnessValue = 70;
        private const double DefaultContrastValue = 80;
        private const double DefaultAudioVolumeValue = 100;
        private const string FirstKillAssetKey = "firstkill";
        private const string GoldHeadshotAssetKey = "goldheadshot";
        private const string HeadshotAssetKey = "headshot_silver";
        private const string KnifeKillAssetKey = "knife_kill";
        private const string LastKillAssetKey = "last_kill";
        private const string BrightnessSettingKey = "AnimationBrightness";
        private const string ContrastSettingKey = "AnimationContrast";
        private const string AudioVolumeSettingKey = "AudioVolume";
        private const string AnimationPlacementSettingKey = "AnimationPlacement";
        private const string AnimationOffsetSettingKey = "AnimationOffset";
        private const string AnimationScaleSettingKey = "AnimationScale";
        private const string VoicePackSettingKey = "VoicePack";
        private const string CsInstallFolderAccessToken = "CsInstallFolder";
        private const string CsInstallFolderTokenSettingKey = "CsInstallFolderToken";
        private const string CsInstallFolderPathSettingKey = "CsInstallFolderPath";
        private const string GsiConfigFileName = "gamestate_integration_killconfirm.cfg";
        private const string GsiConfigText =
            "\"KillConfirmGameBar\"\r\n" +
            "{\r\n" +
            " \"uri\" \"http://127.0.0.1:3000/\"\r\n" +
            " \"timeout\" \"5.0\"\r\n" +
            " \"buffer\"  \"0.1\"\r\n" +
            " \"throttle\" \"0.1\"\r\n" +
            " \"heartbeat\" \"30.0\"\r\n" +
            " \"auth\"\r\n" +
            " {\r\n" +
            "   \"token\" \"killconfirm\"\r\n" +
            " }\r\n" +
            " \"data\"\r\n" +
            " {\r\n" +
            "   \"provider\"           \"1\"\r\n" +
            "   \"map\"                \"1\"\r\n" +
            "   \"round\"              \"1\"\r\n" +
            "   \"player_id\"          \"1\"\r\n" +
            "   \"player_state\"       \"1\"\r\n" +
            "   \"player_weapons\"     \"1\"\r\n" +
            "   \"player_match_stats\" \"1\"\r\n" +
            " }\r\n" +
            "}\r\n";
        private const int ControlPanelStateRefreshMs = 250;
        private const int StatusHintRotationMs = 5000;
        private const string PackagedServiceParameterGroupId = "CrossfirePreset";
        private const string FullTrustProcessLauncherRuntimeClass = "Windows.ApplicationModel.FullTrustProcessLauncher";
        private static readonly System.Guid FullTrustProcessLauncherStaticsGuid =
            new System.Guid("D784837F-1100-3C6B-A455-F6262CC331B6");
        private const int GsiStatusRefreshMs = 10000;
        private const double RecentGsiAgeMs = 120000;
        private static readonly Uri ServiceHealthUri = new Uri("http://127.0.0.1:3000/health");
        private static readonly Uri GsiStatusUri = new Uri("http://127.0.0.1:3000/gsi-status");
        private static readonly Uri ServiceShutdownUri = new Uri("http://127.0.0.1:3000/shutdown");
        private static readonly Uri SoundPackUri = new Uri("http://127.0.0.1:3000/soundpack");
        private static readonly Uri AudioReloadUri = new Uri("http://127.0.0.1:3000/audio/reload");
        private static readonly Uri AudioVolumeUri = new Uri("http://127.0.0.1:3000/audio/volume");
        private static readonly Uri Cs2RootUri = new Uri("http://127.0.0.1:3000/cs2-root");
        private static readonly Uri GuideUri = new Uri("killconfirmoverlay://guide");
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
        private const string FreeServicePortParameterGroupId = "FreeServicePort";
        private const string OpenRuntimeLogsParameterGroupId = "OpenRuntimeLogs";
        private static readonly SemaphoreSlim ServiceStartupGate = new SemaphoreSlim(1, 1);
        private static readonly IReadOnlyDictionary<string, TestPreset> TestPresets =
            new Dictionary<string, TestPreset>(StringComparer.OrdinalIgnoreCase)
            {
                ["one"] = new TestPreset(1),
                ["one_hs"] = new TestPreset(1, isHeadshot: true),
                ["one_knife"] = new TestPreset(1, isKnifeKill: true),
                ["one_first"] = new TestPreset(1, isFirstKill: true),
                ["one_last"] = new TestPreset(1, isLastKill: true),
                ["gold_first"] = new TestPreset(1, isHeadshot: true, isFirstKill: true),
                ["gold_last"] = new TestPreset(1, isHeadshot: true, isLastKill: true),
                ["two"] = new TestPreset(2),
                ["three"] = new TestPreset(3),
                ["four"] = new TestPreset(4),
                ["five"] = new TestPreset(5),
                ["six"] = new TestPreset(6),
                ["seven"] = new TestPreset(7),
                ["eight"] = new TestPreset(8),
                ["nine"] = new TestPreset(9),
                ["badge_first"] = new TestPreset(1, isFirstKill: true, playMainAnimation: false),
                ["badge_last"] = new TestPreset(1, isLastKill: true, playMainAnimation: false)
            };

        private XboxGameBarWidget _widget;
        private KillEventClient _eventClient;
        private double _animationOffset;
        private double _animationScale = 1.0;
        private AnimationPlacementMode _animationPlacement = AnimationPlacementMode.Center;
        private bool _isWidgetVisible = true;
        private XboxGameBarDisplayMode _displayMode = XboxGameBarDisplayMode.Foreground;
        private XboxGameBarWidgetWindowState _windowState = XboxGameBarWidgetWindowState.Restored;
        private bool _suppressVisualAdjustmentEvents;
        private bool _suppressVoicePackEvents;
        private bool _suppressLanguageEvents = true;
        private bool _isPageActive;
        private StorageFolder _csInstallFolder;
        private CfgDetectionState _cfgDetectionState = CfgDetectionState.NotSelected;
        private string _cfgStatusDetail = string.Empty;
        private KillEventConnectionState _serviceConnectionState = KillEventConnectionState.Disconnected;
        private bool _gsiRecentlySeen;
        private bool _gsiStatusCheckPending;
        private int _animationPreloadToken;
        private int _animationCacheProgress;
        private bool _animationCacheReady;
        private bool _animationCacheFailed;
        private int _statusHintIndex;
        private DateTimeOffset _lastGsiStatusCheck = DateTimeOffset.MinValue;
        private readonly DispatcherTimer _controlPanelStateTimer;
        private readonly DispatcherTimer _streakBadgeTimer;
        private readonly DispatcherTimer _statusHintTimer;

        public ClockWidgetPage()
        {
            InitializeComponent();
            AnimationLayer.SizeChanged += OnAnimationLayerSizeChanged;
            VersionText.Text = GetCompactDisplayVersion();
            ToolTipService.SetToolTip(VersionText, GetDisplayVersion());
            LoadLanguageSelector();
            ApplyLanguage();

            _controlPanelStateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ControlPanelStateRefreshMs)
            };
            _controlPanelStateTimer.Tick += OnControlPanelStateTimerTick;

            _streakBadgeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(StreakBadgeDisplayDurationMs)
            };
            _streakBadgeTimer.Tick += OnStreakBadgeTimerTick;

            _statusHintTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(StatusHintRotationMs)
            };
            _statusHintTimer.Tick += OnStatusHintTimerTick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _isPageActive = true;
            _widget = e.Parameter as XboxGameBarWidget;
            if (_widget != null)
            {
                _widget.VisibleChanged += OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged += OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged += OnWidgetWindowStateChanged;
                SyncWidgetPresentationState();
            }

            LoadVisualAdjustmentSettings();
            LoadAnimationPlacementSettings();
            LoadVoicePackSetting();
            _controlPanelStateTimer.Start();
            _statusHintTimer.Start();

            StartKillEventClient();
            ConfigureWidgetCapabilities();
            _ = EnsureServiceAvailableAsync();
            _ = LoadSavedCsFolderAsync();
            UpdateControlPanelVisibility();
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isPageActive = false;
            if (_widget != null)
            {
                _widget.VisibleChanged -= OnWidgetVisibleChanged;
                _widget.GameBarDisplayModeChanged -= OnGameBarDisplayModeChanged;
                _widget.WindowStateChanged -= OnWidgetWindowStateChanged;
            }

            _controlPanelStateTimer.Stop();
            _streakBadgeTimer.Stop();
            _statusHintTimer.Stop();
            _widget = null;
            HideStreakBadge();
            _ = RequestServiceShutdownAsync();

            if (_eventClient != null)
            {
                _eventClient.KillReceived -= OnKillReceived;
                _eventClient.ConnectionStateChanged -= OnConnectionStateChanged;
                _eventClient.Dispose();
                _eventClient = null;
            }

            base.OnNavigatedFrom(e);
        }

        private void OnKillReceived(object sender, KillEvent e)
        {
            HandleKillEvent(e);
        }

        private async void OnResizeClick(object sender, RoutedEventArgs e)
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.TryResizeWindowAsync(DefaultWidgetSize);
            }
            catch (Exception)
            {
            }
        }

        private async void OnCenterClick(object sender, RoutedEventArgs e)
        {
            _animationOffset = 0;
            _animationPlacement = AnimationPlacementMode.Center;
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();

            if (_widget == null)
            {
                return;
            }

            try
            {
                await _widget.CenterWindowAsync();
            }
            catch (Exception)
            {
            }
        }

        private void OnLowerThirdClick(object sender, RoutedEventArgs e)
        {
            _animationPlacement = AnimationPlacementMode.Bottom;
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(-AnimationOffsetStep);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            NudgeAnimation(AnimationOffsetStep);
        }

        private void OnScaleUpClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleUpFactor);
        }

        private void OnScaleDownClick(object sender, RoutedEventArgs e)
        {
            ScaleAnimation(ScaleDownFactor);
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            TestPreset preset = GetSelectedTestPreset();
            if (preset == null)
            {
                HandleKillEvent(new KillEvent
                {
                    KillCount = DefaultPreviewKillCount,
                    PlayMainAnimation = true
                });
                return;
            }

            HandleKillEvent(preset.ToKillEvent());
        }

        private async void OnTestEventClick(object sender, RoutedEventArgs e)
        {
            TestPreset preset = GetSelectedTestPreset();
            if (preset == null)
            {
                return;
            }

            await SendTestEventAsync(preset);
        }

        private async void OnReloadAudioClick(object sender, RoutedEventArgs e)
        {
            await ReloadAudioOutputAsync();
        }

        private async void OnCheckServerClick(object sender, RoutedEventArgs e)
        {
            await CheckServerHealthAsync();
        }

        private async void OnStartServiceClick(object sender, RoutedEventArgs e)
        {
            await EnsureServiceAvailableAsync();
        }

        private async void OnOpenGuideClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await TryOpenGuideAppEntryAsync())
                {
                    return;
                }

                bool launched = await Launcher.LaunchUriAsync(GuideUri);
                App.Log("Open guide protocol launch result=" + launched);

                if (!launched)
                {
                    ShowGuideOpenFailedHint();
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open guide: " + ex);
                ShowGuideOpenFailedHint();
            }
        }

        private static async Task<bool> TryOpenGuideAppEntryAsync()
        {
            try
            {
                IReadOnlyList<AppListEntry> entries = await Package.Current.GetAppListEntriesAsync();
                App.Log("Open guide app entries=" + entries.Count);

                foreach (AppListEntry entry in entries)
                {
                    bool launched = await entry.LaunchAsync();
                    App.Log("Open guide app entry launch result=" + launched);
                    if (launched)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Open guide app entry launch failed: " + ex);
            }

            return false;
        }

        private void ShowGuideOpenFailedHint()
        {
            string hint = LocalizationManager.Text("OpenGuideFailed");
            ShowStatusHint(hint, Color.FromArgb(255, 251, 191, 36));
        }

        private async void OnOpenLogsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                bool launched = await TryLaunchFullTrustHelperAsync(OpenRuntimeLogsParameterGroupId);
                if (!launched)
                {
                    await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to open log folder: " + ex);
            }
        }

        private async void OnFreePortClick(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("Free port requested from widget.");
                ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortRunning");
                ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);

                bool launched = await TryLaunchFullTrustHelperAsync(FreeServicePortParameterGroupId);
                if (!launched)
                {
                    ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                    ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);
                    App.Log("Free port helper launch failed.");
                    return;
                }

                await Task.Delay(1200);
                await EnsureServiceAvailableAsync();
            }
            catch (Exception ex)
            {
                ServiceDiagnosticText.Text = LocalizationManager.Text("FreePortFailed");
                ToolTipService.SetToolTip(ServiceDiagnosticText, ServiceDiagnosticText.Text);
                App.Log("Free port failed: " + ex);
            }
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLanguageEvents)
            {
                return;
            }

            string tag = null;
            if (LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                tag = item.Tag as string;
            }

            LocalizationManager.SetLanguage(string.Equals(tag, "zh-CN", StringComparison.OrdinalIgnoreCase)
                ? UiLanguage.SimplifiedChinese
                : UiLanguage.English);
            ApplyLanguage();
        }

        private async void OnSelectCsFolderClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            picker.FileTypeFilter.Add("*");

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            try
            {
                SaveCsFolder(folder);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to save selected CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgFolderError"), LocalizationManager.Text("CfgFolderSaveError"));
            }
        }

        private async void OnInstallCfgClick(object sender, RoutedEventArgs e)
        {
            if (_csInstallFolder == null)
            {
                await ShowCfgMessageAsync(LocalizationManager.Text("SelectCsFirst"));
                return;
            }

            var dialog = new MessageDialog(
                LocalizationManager.Text("AddCfgQuestion"),
                LocalizationManager.Text("AddCfgTitle"));
            string addText = LocalizationManager.Text("Add");
            dialog.Commands.Add(new UICommand(addText));
            dialog.Commands.Add(new UICommand(LocalizationManager.Text("Cancel")));
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;

            IUICommand result = await dialog.ShowAsync();
            if (result.Label != addText)
            {
                return;
            }

            await InstallCfgAsync();
        }

        private void OnWidgetVisibleChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnGameBarDisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnWidgetWindowStateChanged(XboxGameBarWidget sender, object args)
        {
            SyncWidgetPresentationState();
        }

        private void OnControlPanelStateTimerTick(object sender, object e)
        {
            SyncWidgetPresentationState();
            if (IsControlPanelVisible()
                && !_gsiStatusCheckPending
                && DateTimeOffset.Now - _lastGsiStatusCheck > TimeSpan.FromMilliseconds(GsiStatusRefreshMs))
            {
                _ = RefreshGsiStatusAsync();
            }
        }

        private void OnStatusHintTimerTick(object sender, object e)
        {
            AdvanceStatusHint();
        }

        private void OnStreakBadgeTimerTick(object sender, object e)
        {
            _streakBadgeTimer.Stop();
            HideStreakBadge();
        }

        private void OnConnectionStateChanged(object sender, KillEventConnectionState state)
        {
            UpdateConnectionState(state);
        }

        private async Task LoadSavedCsFolderAsync()
        {
            string token = ApplicationData.Current.LocalSettings.Values[CsInstallFolderTokenSettingKey] as string;
            if (string.IsNullOrWhiteSpace(token))
            {
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                return;
            }

            try
            {
                _csInstallFolder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
                await RefreshCfgStatusAsync();
            }
            catch (Exception ex)
            {
                App.Log("Failed to restore CS folder access: " + ex);
                _csInstallFolder = null;
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
            }
        }

        private async Task TryAutoDetectCsFolderAsync()
        {
            if (_csInstallFolder != null)
            {
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAutoDetecting"), LocalizationManager.Text("CfgSelectRootHint"));

            try
            {
                await EnsureServiceAvailableAsync();

                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(Cs2RootUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    bool found = json.GetNamedBoolean("found", false);
                    string path = json.GetNamedString("path", string.Empty);

                    if (!found || string.IsNullOrWhiteSpace(path))
                    {
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                        return;
                    }

                    try
                    {
                        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(path);
                        SaveCsFolder(folder);
                        await RefreshCfgStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        App.Log("Auto-detected CS folder, but folder access failed: " + ex);
                        ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] = path;
                        UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgDetectedNeedConfirm") + path);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Failed to auto-detect CS folder: " + ex);
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
            }
        }

        private void SaveCsFolder(StorageFolder folder)
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(CsInstallFolderAccessToken, folder);
            ApplicationData.Current.LocalSettings.Values[CsInstallFolderTokenSettingKey] = CsInstallFolderAccessToken;
            ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] = folder.Path;
            _csInstallFolder = folder;
        }

        private async Task RefreshCfgStatusAsync()
        {
            if (_csInstallFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.NotSelected, null, LocalizationManager.Text("CfgSelectRootHint"));
                return;
            }

            UpdateCfgStatus(CfgDetectionState.Checking, null, GetCsFolderDisplayText());

            StorageFolder cfgFolder = await TryGetCfgFolderAsync(_csInstallFolder);
            if (cfgFolder == null)
            {
                UpdateCfgStatus(CfgDetectionState.Error, null, LocalizationManager.Text("CfgWrongFolderHint"));
                return;
            }

            try
            {
                await cfgFolder.GetFileAsync(GsiConfigFileName);
                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (System.IO.FileNotFoundException)
            {
                UpdateCfgStatus(CfgDetectionState.Missing, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to check cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, null, GetCsFolderDisplayText());
            }
        }

        private async Task InstallCfgAsync()
        {
            try
            {
                UpdateCfgStatus(CfgDetectionState.Checking, LocalizationManager.Text("CfgAdding"), GetCsFolderDisplayText());
                StorageFolder cfgFolder = await GetOrCreateCfgFolderAsync(_csInstallFolder);
                StorageFile cfgFile = await cfgFolder.CreateFileAsync(GsiConfigFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(cfgFile, GsiConfigText, UnicodeEncoding.Utf8);
                UpdateCfgStatus(CfgDetectionState.Ready, null, GetCsFolderDisplayText());
            }
            catch (Exception ex)
            {
                App.Log("Failed to install cfg file: " + ex);
                UpdateCfgStatus(CfgDetectionState.Error, LocalizationManager.Text("CfgAddFailed"), GetCsFolderDisplayText());
                await ShowCfgMessageAsync(LocalizationManager.Text("CfgWriteFailed"));
            }
        }

        private string GetCsFolderDisplayText()
        {
            string savedPath = ApplicationData.Current.LocalSettings.Values[CsInstallFolderPathSettingKey] as string;
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                return savedPath;
            }

            return _csInstallFolder?.Path ?? _csInstallFolder?.Name ?? "Counter-Strike Global Offensive";
        }

        private static async Task<StorageFolder> TryGetCfgFolderAsync(StorageFolder root)
        {
            try
            {
                StorageFolder gameFolder = await root.GetFolderAsync("game");
                StorageFolder csgoFolder = await gameFolder.GetFolderAsync("csgo");
                return await csgoFolder.GetFolderAsync("cfg");
            }
            catch
            {
                return null;
            }
        }

        private static async Task<StorageFolder> GetOrCreateCfgFolderAsync(StorageFolder root)
        {
            StorageFolder gameFolder = await root.CreateFolderAsync("game", CreationCollisionOption.OpenIfExists);
            StorageFolder csgoFolder = await gameFolder.CreateFolderAsync("csgo", CreationCollisionOption.OpenIfExists);
            return await csgoFolder.CreateFolderAsync("cfg", CreationCollisionOption.OpenIfExists);
        }

        private async Task ShowCfgMessageAsync(string message)
        {
            try
            {
                await new MessageDialog(message, LocalizationManager.Text("CfgMessageTitle")).ShowAsync();
            }
            catch
            {
            }
        }

        private void LoadLanguageSelector()
        {
            _suppressLanguageEvents = true;
            try
            {
                string target = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "zh-CN" : "en-US";
                foreach (object option in LanguageSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))
                    {
                        LanguageSelector.SelectedItem = item;
                        return;
                    }
                }

                LanguageSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressLanguageEvents = false;
            }
        }

        private void ApplyLanguage()
        {
            RefreshStatusHint(true);
            ToolTipService.SetToolTip(LanguageSelector, "Language / 语言");

            ToolTipService.SetToolTip(StartServiceButton, LocalizationManager.Text("StartServiceTooltip"));
            ToolTipService.SetToolTip(CheckServiceButton, LocalizationManager.Text("CheckServiceTooltip"));
            ToolTipService.SetToolTip(OpenGuideButton, LocalizationManager.Text("OpenGuideTooltip"));
            ToolTipService.SetToolTip(OpenLogsButton, LocalizationManager.Text("OpenLogsTooltip"));
            ToolTipService.SetToolTip(FreePortButton, LocalizationManager.Text("FreePortTooltip"));
            ToolTipService.SetToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStatusTooltip"));
            ToolTipService.SetToolTip(CfgStatusBadge, LocalizationManager.Text("CfgStatusTooltip"));
            ToolTipService.SetToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStatusTooltip"));
            ToolTipService.SetToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheTooltip"));

            ServiceBadgeText.Text = "SVC";
            CfgBadgeText.Text = "CFG";
            GsiBadgeText.Text = "GSI";
            CfgLabelText.Text = LocalizationManager.Text("CfgLabel");
            CrossfireSwatGrVoiceItem.Content = LocalizationManager.Text("CrossfireSwatGr");
            CrossfireSwatBlVoiceItem.Content = LocalizationManager.Text("CrossfireSwatBl");
            CrossfireFlyingTigerGrVoiceItem.Content = LocalizationManager.Text("CrossfireFlyingTigerGr");
            CrossfireFlyingTigerBlVoiceItem.Content = LocalizationManager.Text("CrossfireFlyingTigerBl");
            CrossfireWomenGrVoiceItem.Content = LocalizationManager.Text("CrossfireWomenGr");
            CrossfireWomenBlVoiceItem.Content = LocalizationManager.Text("CrossfireWomenBl");

            ToolTipService.SetToolTip(VoicePackSelector, LocalizationManager.Text("VoiceTooltip"));
            ToolTipService.SetToolTip(SelectCsFolderButton, LocalizationManager.Text("SelectCsFolderTooltip"));
            CfgInstallButton.Content = LocalizationManager.Text("Add");
            ToolTipService.SetToolTip(CfgInstallButton, LocalizationManager.Text("AddMissingCfgTooltip"));

            ToolTipService.SetToolTip(TestPresetSelector, LocalizationManager.Text("TestPresetTooltip"));
            ToolTipService.SetToolTip(PreviewButton, LocalizationManager.Text("PreviewTooltip"));
            ToolTipService.SetToolTip(SendTestButton, LocalizationManager.Text("SendTestTooltip"));
            ToolTipService.SetToolTip(ReloadAudioButton, LocalizationManager.Text("ReloadAudioTooltip"));

            ToolTipService.SetToolTip(DefaultSizeButton, LocalizationManager.Text("DefaultSizeTooltip"));
            ToolTipService.SetToolTip(CenterButton, LocalizationManager.Text("CenterWindowTooltip"));
            ToolTipService.SetToolTip(LowerThirdButton, LocalizationManager.Text("LowerThirdTooltip"));
            ToolTipService.SetToolTip(MoveUpButton, LocalizationManager.Text("MoveUpTooltip"));
            ToolTipService.SetToolTip(MoveDownButton, LocalizationManager.Text("MoveDownTooltip"));
            ToolTipService.SetToolTip(ScaleDownButton, LocalizationManager.Text("ShrinkTooltip"));
            ToolTipService.SetToolTip(ScaleUpButton, LocalizationManager.Text("EnlargeTooltip"));

            ToolTipService.SetToolTip(BrightnessIcon, LocalizationManager.Text("BrightnessTooltip"));
            ToolTipService.SetToolTip(BrightnessSelector, LocalizationManager.Text("BrightnessTooltip"));
            ToolTipService.SetToolTip(ContrastIcon, LocalizationManager.Text("ContrastTooltip"));
            ToolTipService.SetToolTip(ContrastSelector, LocalizationManager.Text("ContrastTooltip"));
            ToolTipService.SetToolTip(VolumeIcon, LocalizationManager.Text("AudioVolumeTooltip"));
            ToolTipService.SetToolTip(AudioVolumeSelector, LocalizationManager.Text("AudioVolumeTooltip"));
            ToolTipService.SetToolTip(ResetVisualButton, LocalizationManager.Text("ResetTooltip"));

            UpdateConnectionState(_serviceConnectionState);
            UpdateCfgStatus(_cfgDetectionState, null, _cfgStatusDetail);
            UpdateGsiStatus(true, _gsiRecentlySeen, _gsiRecentlySeen ? 1 : 0, null);
        }

        private void ConfigureWidgetCapabilities()
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                _widget.MinWindowSize = MinWidgetSize;
                _widget.MaxWindowSize = MaxWidgetSize;
                _widget.HorizontalResizeSupported = true;
                _widget.VerticalResizeSupported = true;
            }
            catch (Exception)
            {
            }
        }

        private void StartKillEventClient()
        {
            if (_eventClient != null)
            {
                return;
            }

            _eventClient = new KillEventClient(Dispatcher);
            _eventClient.KillReceived += OnKillReceived;
            _eventClient.ConnectionStateChanged += OnConnectionStateChanged;
            _eventClient.Start();
        }

        private async Task EnsureServiceAvailableAsync()
        {
            App.Log("EnsureServiceAvailableAsync: entered. pageActive=" + _isPageActive);
            if (!_isPageActive)
            {
                App.Log("EnsureServiceAvailableAsync: skipped because page is inactive.");
                return;
            }

            bool initialHealth = await IsServiceHealthyAsync();
            App.Log("EnsureServiceAvailableAsync: initial health=" + initialHealth);
            if (initialHealth)
            {
                if (_isPageActive)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                }

                await SyncSelectedVoicePackAsync();
                return;
            }

            await ServiceStartupGate.WaitAsync();
            try
            {
                App.Log("EnsureServiceAvailableAsync: entered startup gate.");
                if (!_isPageActive)
                {
                    App.Log("EnsureServiceAvailableAsync: aborted inside gate because page is inactive.");
                    return;
                }

                bool gatedHealth = await IsServiceHealthyAsync();
                App.Log("EnsureServiceAvailableAsync: gated health=" + gatedHealth);
                if (gatedHealth)
                {
                    UpdateConnectionState(KillEventConnectionState.Connected);
                    await SyncSelectedVoicePackAsync();
                    return;
                }

                UpdateConnectionState(KillEventConnectionState.Connecting);
                App.Log("EnsureServiceAvailableAsync: attempting packaged service launch.");

                bool launched = await TryLaunchPackagedServiceAsync();
                App.Log("EnsureServiceAvailableAsync: launch result=" + launched);
                if (!launched)
                {
                    UpdateConnectionState(KillEventConnectionState.Disconnected);
                    await ShowServiceStartupFailureAsync();
                    return;
                }

                bool ready = await WaitForServiceReadyAsync();
                App.Log("EnsureServiceAvailableAsync: service ready after launch=" + ready);
                if (_isPageActive)
                {
                    UpdateConnectionState(ready
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);
                }

                if (ready)
                {
                    HideServiceDiagnostic();
                    await SyncSelectedVoicePackAsync();
                }
                else
                {
                    await ShowServiceStartupFailureAsync();
                }
            }
            finally
            {
                App.Log("EnsureServiceAvailableAsync: leaving startup gate.");
                ServiceStartupGate.Release();
            }
        }

        private async Task CheckServerHealthAsync()
        {
            App.Log("CheckServerHealthAsync: manual health check requested.");
            UpdateConnectionState(KillEventConnectionState.Connecting);

            bool isHealthy = await IsServiceHealthyAsync();
            App.Log("CheckServerHealthAsync: health result=" + isHealthy);
            UpdateConnectionState(isHealthy
                ? KillEventConnectionState.Connected
                : KillEventConnectionState.Disconnected);

            if (isHealthy)
            {
                HideServiceDiagnostic();
                await SyncSelectedVoicePackAsync();
            }
            else
            {
                await ShowServiceStartupFailureAsync();
            }
        }

        private static async Task<bool> TryLaunchPackagedServiceAsync()
        {
            return await TryLaunchFullTrustHelperAsync(PackagedServiceParameterGroupId);
        }

        private static async Task<bool> TryLaunchFullTrustHelperAsync(string parameterGroupId)
        {
            try
            {
                App.Log("Launching full-trust helper. group=" + parameterGroupId);
                if (!ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
                {
                    App.Log("FullTrustProcessLauncher is not available on this Windows build.");
                    return false;
                }

                IAsyncAction launchAction = LaunchFullTrustProcessForCurrentAppWithParameters(parameterGroupId);
                if (launchAction == null)
                {
                    App.Log("FullTrustProcessLauncher returned no launch action.");
                    return false;
                }

                await launchAction;
                App.Log("Full-trust helper launch call returned without exception. group=" + parameterGroupId);
                return true;
            }
            catch (Exception ex)
            {
                App.Log(
                    "Failed to launch packaged service: type=" + ex.GetType().FullName
                    + ", hresult=0x" + ex.HResult.ToString("X8")
                    + ", message=" + ex.Message
                    + ", detail=" + ex);
                return false;
            }
        }

        private static IAsyncAction LaunchFullTrustProcessForCurrentAppWithParameters(string parameterGroupId)
        {
            IntPtr runtimeClassName = IntPtr.Zero;
            IFullTrustProcessLauncherStatics launcherStatics = null;

            try
            {
                int hr = WindowsCreateString(
                    FullTrustProcessLauncherRuntimeClass,
                    FullTrustProcessLauncherRuntimeClass.Length,
                    out runtimeClassName);
                Marshal.ThrowExceptionForHR(hr);

                System.Guid iid = FullTrustProcessLauncherStaticsGuid;
                hr = RoGetActivationFactory(runtimeClassName, ref iid, out launcherStatics);
                Marshal.ThrowExceptionForHR(hr);

                return launcherStatics.LaunchFullTrustProcessForCurrentAppWithParametersAsync(parameterGroupId);
            }
            finally
            {
                if (runtimeClassName != IntPtr.Zero)
                {
                    WindowsDeleteString(runtimeClassName);
                }

                if (launcherStatics != null)
                {
                    Marshal.ReleaseComObject(launcherStatics);
                }
            }
        }

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", ExactSpelling = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", ExactSpelling = true)]
        private static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref System.Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IFullTrustProcessLauncherStatics factory);

        [ComImport]
        [System.Runtime.InteropServices.Guid("D784837F-1100-3C6B-A455-F6262CC331B6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
        private interface IFullTrustProcessLauncherStatics
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppAsync();

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForCurrentAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId);

            [return: MarshalAs(UnmanagedType.Interface)]
            IAsyncAction LaunchFullTrustProcessForAppWithParametersAsync(
                [MarshalAs(UnmanagedType.HString)] string fullTrustPackageRelativeAppId,
                [MarshalAs(UnmanagedType.HString)] string parameterGroupId);
        }

        private static async Task<bool> WaitForServiceReadyAsync()
        {
            App.Log("WaitForServiceReadyAsync: polling for service health.");
            DateTimeOffset deadline = DateTimeOffset.UtcNow + ServiceStartupTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await IsServiceHealthyAsync())
                {
                    App.Log("WaitForServiceReadyAsync: service became healthy.");
                    return true;
                }

                await Task.Delay(ServiceStartupPollInterval);
            }

            bool finalHealth = await IsServiceHealthyAsync();
            App.Log("WaitForServiceReadyAsync: timeout reached. final health=" + finalHealth);
            return finalHealth;
        }

        private async Task ShowServiceStartupFailureAsync()
        {
            string hint = await ResolveServiceFailureHintAsync();
            ServiceDiagnosticText.Text = hint;
            ServiceDiagnosticRow.Visibility = Visibility.Visible;
            ToolTipService.SetToolTip(ServiceDiagnosticText, hint);
            App.Log("Service diagnostic shown: " + hint);
        }

        private void HideServiceDiagnostic()
        {
            ServiceDiagnosticRow.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(ServiceDiagnosticText, null);
        }

        private static async Task<string> ResolveServiceFailureHintAsync()
        {
            string serviceLog = await TryReadLocalLogAsync("service.log");
            string bootstrapLog = await TryReadLocalLogAsync("bootstrap.log");
            string combined = (serviceLog + "\n" + bootstrapLog).ToLowerInvariant();

            if (combined.Contains("os error 10048"))
            {
                return LocalizationManager.Text("ServicePortInUseHint");
            }

            if (combined.Contains("os error 10013"))
            {
                return LocalizationManager.Text("ServicePortBlockedHint");
            }

            if (combined.Contains("fatal error"))
            {
                return LocalizationManager.Text("ServiceFailedSeeLogs");
            }

            return LocalizationManager.Text("ServiceFailedGeneric");
        }

        private static async Task<string> TryReadLocalLogAsync(string fileName)
        {
            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(ServiceHealthUri))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task RefreshGsiStatusAsync()
        {
            _gsiStatusCheckPending = true;
            _lastGsiStatusCheck = DateTimeOffset.Now;

            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(GsiStatusUri))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        UpdateGsiStatus(false, false, 0, null);
                        return;
                    }

                    string responseText = await response.Content.ReadAsStringAsync();
                    JsonObject json = JsonObject.Parse(responseText);
                    double posts = json.GetNamedNumber("posts", 0);
                    double? ageMs = TryGetJsonNumber(json, "last_post_age_ms");
                    bool recentlySeen = posts > 0 && ageMs.HasValue && ageMs.Value <= RecentGsiAgeMs;
                    UpdateGsiStatus(true, recentlySeen, posts, ageMs);
                }
            }
            catch (Exception)
            {
                UpdateGsiStatus(false, false, 0, null);
            }
            finally
            {
                _gsiStatusCheckPending = false;
            }
        }

        private static double? TryGetJsonNumber(JsonObject json, string key)
        {
            if (!json.ContainsKey(key))
            {
                return null;
            }

            IJsonValue value = json.GetNamedValue(key);
            return value.ValueType == JsonValueType.Number
                ? value.GetNumber()
                : (double?)null;
        }

        private static async Task RequestServiceShutdownAsync()
        {
            try
            {
                using (var client = new HttpClient())
                using (var content = new HttpStringContent(string.Empty, UnicodeEncoding.Utf8, "text/plain"))
                {
                    await client.PostAsync(ServiceShutdownUri, content);
                }
            }
            catch (Exception ex)
            {
                App.Log("Service shutdown request failed: " + ex.Message);
            }
        }

        private async void OnVoicePackSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressVoicePackEvents)
            {
                return;
            }

            string preset = GetSelectedVoicePackPreset();
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
            await EnsureServiceAvailableAsync();
            await SyncSelectedVoicePackAsync();
        }

        private void LoadVoicePackSetting()
        {
            string preset = ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] as string;
            if (string.IsNullOrWhiteSpace(preset))
            {
                preset = "crossfire";
            }

            preset = NormalizeVoicePackPreset(preset);
            ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
            SelectVoicePackPreset(preset);
        }

        private async Task SyncSelectedVoicePackAsync()
        {
            string preset = GetSelectedVoicePackPreset();
            if (string.IsNullOrWhiteSpace(preset))
            {
                return;
            }

            try
            {
                using (var client = new HttpClient())
                using (var content = new HttpStringContent(
                    "{\"preset\":\"" + preset + "\"}",
                    UnicodeEncoding.Utf8,
                    "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(SoundPackUri, content))
                {
                    UpdateConnectionState(response.IsSuccessStatusCode
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        ApplyVoicePackResponse(responseText);
                    }
                }
            }
            catch (Exception)
            {
                UpdateConnectionState(KillEventConnectionState.Disconnected);
            }
        }

        private string GetSelectedVoicePackPreset()
        {
            if (VoicePackSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag)
            {
                return tag;
            }

            return "crossfire";
        }

        private void SelectVoicePackPreset(string preset)
        {
            preset = NormalizeVoicePackPreset(preset);
            _suppressVoicePackEvents = true;
            try
            {
                foreach (object option in VoicePackSelector.Items)
                {
                    if (option is ComboBoxItem item
                        && item.Tag is string tag
                        && string.Equals(tag, preset, StringComparison.OrdinalIgnoreCase))
                    {
                        VoicePackSelector.SelectedItem = item;
                        return;
                    }
                }

                VoicePackSelector.SelectedIndex = 0;
            }
            finally
            {
                _suppressVoicePackEvents = false;
            }
        }

        private void ApplyVoicePackResponse(string responseText)
        {
            try
            {
                JsonObject json = JsonObject.Parse(responseText);
                string preset = NormalizeVoicePackPreset(json.GetNamedString("preset", GetSelectedVoicePackPreset()));
                ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
                SelectVoicePackPreset(preset);
            }
            catch (Exception)
            {
            }
        }

        private static string NormalizeVoicePackPreset(string preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
            {
                return "crossfire";
            }

            switch (preset.Trim().ToLowerInvariant())
            {
                case "cf":
                case "crossfire":
                    return "crossfire_swat_gr";
                case "cffhd":
                case "cf_fhd":
                case "crossfire_fhd":
                case "crossfire_v_fhd":
                    return "crossfire_flying_tiger_gr";
                case "kkgr":
                case "knifegr":
                case "knifekill_gr":
                    return "crossfire_women_gr";
                case "kkbl":
                case "knifebl":
                case "knifekill_bl":
                    return "crossfire_women_bl";
                default:
                    return preset;
            }
        }

        private async Task WarmStartupAnimationCacheAsync()
        {
            int token = ++_animationPreloadToken;
            UpdateAnimationCacheProgress(0);

            try
            {
                await Task.Delay(StartupPreloadDelayMs);

                if (!_isPageActive || token != _animationPreloadToken)
                {
                    return;
                }

                var progress = new Progress<int>(value =>
                {
                    if (token == _animationPreloadToken)
                    {
                        UpdateAnimationCacheProgress(value);
                    }
                });

                await PrimaryKillAnimation.PreloadGameplayAnimationsAsync(progress);

                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheReady();
                }
            }
            catch (Exception ex)
            {
                App.Log("Animation preload failed: " + ex);
                if (_isPageActive && token == _animationPreloadToken)
                {
                    UpdateAnimationCacheFailed();
                }
            }
        }

        private void UpdateAnimationCacheProgress(int percent)
        {
            int value = Math.Max(0, Math.Min(100, percent));
            _animationCacheProgress = value;
            _animationCacheReady = false;
            _animationCacheFailed = false;

            if (value >= 100)
            {
                UpdateAnimationCacheReady();
                return;
            }

            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
            AnimationCacheBadgeText.Text = value <= 0 ? "ANI" : value + "%";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
            ToolTipService.SetToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheLoading") + value + "%");
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheReady()
        {
            _animationCacheProgress = 100;
            _animationCacheReady = true;
            _animationCacheFailed = false;
            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 191, 208, 227));
            ToolTipService.SetToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheReady"));
            RefreshStatusHint(false);
        }

        private void UpdateAnimationCacheFailed()
        {
            _animationCacheReady = false;
            _animationCacheFailed = true;
            AnimationCacheDot.Visibility = Visibility.Visible;
            AnimationCacheDot.Background = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113));
            AnimationCacheBadgeText.Text = "ANI";
            AnimationCacheBadgeText.Foreground = new SolidColorBrush(Color.FromArgb(255, 191, 208, 227));
            ToolTipService.SetToolTip(AnimationCacheStatusBadge, LocalizationManager.Text("AnimationCacheFailed"));
            RefreshStatusHint(false);
        }

        private void HandleKillEvent(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return;
            }

            if (killEvent.PlayMainAnimation)
            {
                PlayPrimaryAnimation(killEvent);
            }

            PlayBadgeAnimation(killEvent);
            UpdateStreakBadge(killEvent);
        }

        private void PlayPrimaryAnimation(KillEvent killEvent)
        {
            if (killEvent == null || killEvent.KillCount <= 0)
            {
                return;
            }

            if (killEvent.KillCount == 1)
            {
                if (killEvent.IsKnifeKill)
                {
                    PrimaryKillAnimation.PlayNamed(KnifeKillAssetKey);
                    return;
                }

                if (killEvent.IsHeadshot)
                {
                    if (killEvent.IsFirstKill || killEvent.IsLastKill)
                    {
                        PrimaryKillAnimation.PlayNamed(GoldHeadshotAssetKey);
                        return;
                    }

                    PrimaryKillAnimation.PlayNamed(HeadshotAssetKey);
                    return;
                }
            }

            PrimaryKillAnimation.Play(killEvent.KillCount);
        }

        private void PlayBadgeAnimation(KillEvent killEvent)
        {
            if (killEvent == null)
            {
                return;
            }

            if (killEvent.IsLastKill)
            {
                BadgeKillAnimation.PlayNamed(LastKillAssetKey);
                return;
            }

            if (killEvent.IsFirstKill)
            {
                BadgeKillAnimation.PlayNamed(FirstKillAssetKey);
            }
        }

        private TestPreset GetSelectedTestPreset()
        {
            if (TestPresetSelector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && TestPresets.TryGetValue(tag, out TestPreset preset))
            {
                return preset;
            }

            return null;
        }

        private async Task SendTestEventAsync(TestPreset preset)
        {
            if (preset == null)
            {
                return;
            }

            UpdateConnectionState(KillEventConnectionState.Connecting);

            try
            {
                using (var client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(new Uri(BuildTestEventUri(preset))))
                {
                    UpdateConnectionState(response.IsSuccessStatusCode
                        ? KillEventConnectionState.Connected
                        : KillEventConnectionState.Disconnected);
                }
            }
            catch (Exception)
            {
                UpdateConnectionState(KillEventConnectionState.Disconnected);
            }
        }

        private async Task ReloadAudioOutputAsync()
        {
            App.Log("Reload audio output requested.");
            ShowStatusHint(LocalizationManager.Text("ReloadAudioRunning"), Color.FromArgb(255, 251, 191, 36));

            try
            {
                await EnsureServiceAvailableAsync();

                using (var client = new HttpClient())
                using (var content = new HttpStringContent(string.Empty))
                using (HttpResponseMessage response = await client.PostAsync(AudioReloadUri, content))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        ShowStatusHint(LocalizationManager.Text("ReloadAudioReady"), Color.FromArgb(255, 167, 243, 208));
                        App.Log("Reload audio output succeeded.");
                        return;
                    }

                    App.Log("Reload audio output failed: status=" + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                App.Log("Reload audio output failed: " + ex);
            }

            ShowStatusHint(LocalizationManager.Text("ReloadAudioFailed"), Color.FromArgb(255, 251, 191, 36));
        }

        private static string BuildTestEventUri(TestPreset preset)
        {
            var query = new List<string>();
            if (preset.IsHeadshot)
            {
                query.Add("headshot=true");
            }

            if (preset.IsKnifeKill)
            {
                query.Add("knife=true");
            }

            if (preset.IsFirstKill)
            {
                query.Add("first=true");
            }

            if (preset.IsLastKill)
            {
                query.Add("last=true");
            }

            if (!preset.PlayMainAnimation)
            {
                query.Add("main=false");
            }

            query.Add("audio=true");
            string suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
            return $"http://127.0.0.1:3000/test/{preset.KillCount}{suffix}";
        }

        private void NudgeAnimation(double delta)
        {
            double maxOffset = GetMaxAnimationOffset();
            double currentOffset = GetResolvedAnimationOffset();

            _animationPlacement = AnimationPlacementMode.Manual;
            _animationOffset = Math.Max(-maxOffset, Math.Min(maxOffset, currentOffset + delta));
            ApplyAnimationOffset();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationOffset()
        {
            ApplyAnimationTransform();
        }

        private void ScaleAnimation(double factor)
        {
            _animationScale *= factor;
            ApplyAnimationTransform();
            SaveAnimationPlacementSettings();
        }

        private void ApplyAnimationTransform()
        {
            AnimationTransform.ScaleX = _animationScale;
            AnimationTransform.ScaleY = _animationScale;
            AnimationTransform.TranslateY = GetResolvedAnimationOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_animationPlacement == AnimationPlacementMode.Bottom)
            {
                ApplyAnimationOffset();
                SaveAnimationPlacementSettings();
            }
        }

        private double GetResolvedAnimationOffset()
        {
            switch (_animationPlacement)
            {
                case AnimationPlacementMode.Bottom:
                    return GetBottomOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _animationOffset;
            }
        }

        private double GetBottomOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * BottomFifthAnimationOffsetRatio);
        }

        private double GetMaxAnimationOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return Math.Max(AnimationOffsetStep, layerHeight * MaxAnimationOffsetRatio);
        }

        private void UpdateControlPanelVisibility()
        {
            bool showControlPanel = IsControlPanelVisible();

            ControlPanel.Visibility = showControlPanel ? Visibility.Visible : Visibility.Collapsed;
            ControlPanel.IsHitTestVisible = showControlPanel;
            ControlPanel.Opacity = showControlPanel ? 1.0 : 0.0;
        }

        private bool IsControlPanelVisible()
        {
            return _isWidgetVisible
                && _displayMode == XboxGameBarDisplayMode.Foreground
                && _windowState != XboxGameBarWidgetWindowState.Minimized;
        }

        private void SyncWidgetPresentationState()
        {
            if (_widget == null)
            {
                return;
            }

            try
            {
                _isWidgetVisible = _widget.Visible;
                _displayMode = _widget.GameBarDisplayMode;
                _windowState = _widget.WindowState;
            }
            catch (Exception)
            {
            }

            UpdateControlPanelVisibility();
        }

        private void UpdateConnectionState(KillEventConnectionState state)
        {
            _serviceConnectionState = state;

            switch (state)
            {
                case KillEventConnectionState.Connected:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceRunning"));
                    HideServiceDiagnostic();
                    break;
                case KillEventConnectionState.Connecting:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceStarting"));
                    break;
                default:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, LocalizationManager.Text("ServiceOffline"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateCfgStatus(CfgDetectionState state, string label, string detail)
        {
            _cfgDetectionState = state;
            _cfgStatusDetail = detail ?? string.Empty;
            CfgStatusText.Text = string.IsNullOrWhiteSpace(label) ? ResolveCfgStatusLabel(state) : label;
            CfgHintText.Text = ResolveCfgHintText(state, _cfgStatusDetail);
            CfgActionRow.Visibility = state == CfgDetectionState.Ready
                ? Visibility.Collapsed
                : Visibility.Visible;
            CfgInstallButton.Visibility = state == CfgDetectionState.Missing
                ? Visibility.Visible
                : Visibility.Collapsed;

            switch (state)
            {
                case CfgDetectionState.Ready:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    ToolTipService.SetToolTip(CfgStatusBadge, LocalizationManager.Text("CfgReadyTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Checking:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                    ToolTipService.SetToolTip(CfgStatusBadge, LocalizationManager.Text("CheckingCfgTooltip"));
                    break;
                case CfgDetectionState.Missing:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                    ToolTipService.SetToolTip(CfgStatusBadge, LocalizationManager.Text("CfgMissingTooltip") + _cfgStatusDetail);
                    break;
                case CfgDetectionState.Error:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113));
                    ToolTipService.SetToolTip(CfgStatusBadge, _cfgStatusDetail);
                    break;
                default:
                    CfgDot.Background = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
                    ToolTipService.SetToolTip(CfgStatusBadge, LocalizationManager.Text("SelectCsRootTooltip"));
                    break;
            }

            RefreshStatusHint(false);
        }

        private void UpdateGsiStatus(bool serviceReachable, bool recentlySeen, double posts, double? ageMs)
        {
            _gsiRecentlySeen = recentlySeen;

            if (recentlySeen)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                ToolTipService.SetToolTip(GsiStatusBadge, LocalizationManager.Text("GsiReceivingTooltip"));
            }
            else if (serviceReachable && posts > 0)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                ToolTipService.SetToolTip(GsiStatusBadge, LocalizationManager.Text("GsiStaleTooltip"));
            }
            else if (serviceReachable)
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
                ToolTipService.SetToolTip(GsiStatusBadge, LocalizationManager.Text("GsiWaitingTooltip"));
            }
            else
            {
                GsiDot.Background = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113));
                ToolTipService.SetToolTip(GsiStatusBadge, LocalizationManager.Text("ServiceOffline"));
            }

            RefreshStatusHint(false);
        }

        private void AdvanceStatusHint()
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            _statusHintIndex = (_statusHintIndex + 1) % hints.Count;
            ApplyStatusHint(hints[_statusHintIndex]);
        }

        private void RefreshStatusHint(bool resetCycle)
        {
            IReadOnlyList<StatusHint> hints = BuildStatusHints();
            if (hints.Count == 0)
            {
                return;
            }

            if (resetCycle)
            {
                _statusHintIndex = 0;
            }
            else if (_statusHintIndex >= hints.Count)
            {
                _statusHintIndex = 0;
            }

            ApplyStatusHint(hints[_statusHintIndex]);
        }

        private IReadOnlyList<StatusHint> BuildStatusHints()
        {
            var hints = new List<StatusHint>();

            if (ShouldPrioritizePinHint())
            {
                hints.Add(new StatusHint(LocalizationManager.Text("PinHint"), Color.FromArgb(255, 251, 191, 36)));
            }

            hints.Add(new StatusHint(LocalizationManager.Text("DisableClickThroughHint"), Color.FromArgb(255, 251, 191, 36)));

            bool serviceReady = _serviceConnectionState == KillEventConnectionState.Connected;
            bool cfgReady = _cfgDetectionState == CfgDetectionState.Ready;
            bool animationReady = _animationCacheReady;

            if (serviceReady && cfgReady && _gsiRecentlySeen && animationReady)
            {
                hints.Add(new StatusHint(LocalizationManager.Text("ReadyAllSignals"), Color.FromArgb(255, 167, 243, 208)));
            }

            hints.Add(new StatusHint(GetServiceStatusHint(), GetServiceHintColor()));
            hints.Add(new StatusHint(GetCfgStatusHint(), GetCfgHintColor()));
            hints.Add(new StatusHint(GetGsiStatusHint(), GetGsiHintColor()));
            hints.Add(new StatusHint(GetAnimationStatusHint(), GetAnimationHintColor()));

            return hints;
        }

        private bool ShouldPrioritizePinHint()
        {
            return _displayMode == XboxGameBarDisplayMode.Foreground;
        }

        private void ApplyStatusHint(StatusHint hint)
        {
            ShowStatusHint(hint.Text, hint.Color);
        }

        private void ShowStatusHint(string text, Color color)
        {
            PinHintText.Text = text;
            PinHintText.Foreground = new SolidColorBrush(color);
            ToolTipService.SetToolTip(StatusHintBox, text);
        }

        private string GetServiceStatusHint()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return LocalizationManager.Text("StatusSvcReady");
                case KillEventConnectionState.Connecting:
                    return LocalizationManager.Text("StatusSvcStarting");
                default:
                    return LocalizationManager.Text("StatusSvcOffline");
            }
        }

        private Color GetServiceHintColor()
        {
            switch (_serviceConnectionState)
            {
                case KillEventConnectionState.Connected:
                    return Color.FromArgb(255, 167, 243, 208);
                case KillEventConnectionState.Connecting:
                    return Color.FromArgb(255, 251, 191, 36);
                default:
                    return Color.FromArgb(255, 248, 113, 113);
            }
        }

        private string GetCfgStatusHint()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("StatusCfgReady");
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("StatusCfgChecking");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("StatusCfgMissing");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("StatusCfgError");
                default:
                    return LocalizationManager.Text("StatusCfgSelect");
            }
        }

        private Color GetCfgHintColor()
        {
            switch (_cfgDetectionState)
            {
                case CfgDetectionState.Ready:
                    return Color.FromArgb(255, 167, 243, 208);
                case CfgDetectionState.Error:
                    return Color.FromArgb(255, 248, 113, 113);
                default:
                    return Color.FromArgb(255, 251, 191, 36);
            }
        }

        private string GetGsiStatusHint()
        {
            if (_gsiRecentlySeen)
            {
                return LocalizationManager.Text("StatusGsiReady");
            }

            if (_serviceConnectionState != KillEventConnectionState.Connected)
            {
                return LocalizationManager.Text("StatusGsiNeedsService");
            }

            return LocalizationManager.Text("StatusGsiWaiting");
        }

        private Color GetGsiHintColor()
        {
            if (_gsiRecentlySeen)
            {
                return Color.FromArgb(255, 167, 243, 208);
            }

            return _serviceConnectionState == KillEventConnectionState.Connected
                ? Color.FromArgb(255, 251, 191, 36)
                : Color.FromArgb(255, 107, 114, 128);
        }

        private string GetAnimationStatusHint()
        {
            if (_animationCacheReady)
            {
                return LocalizationManager.Text("StatusAniReady");
            }

            if (_animationCacheFailed)
            {
                return LocalizationManager.Text("StatusAniFailed");
            }

            return LocalizationManager.Text("StatusAniLoading") + Math.Max(0, Math.Min(99, _animationCacheProgress)) + "%";
        }

        private Color GetAnimationHintColor()
        {
            if (_animationCacheReady)
            {
                return Color.FromArgb(255, 167, 243, 208);
            }

            if (_animationCacheFailed)
            {
                return Color.FromArgb(255, 248, 113, 113);
            }

            return Color.FromArgb(255, 251, 191, 36);
        }

        private static string ResolveCfgStatusLabel(CfgDetectionState state)
        {
            switch (state)
            {
                case CfgDetectionState.Checking:
                    return LocalizationManager.Text("CfgChecking");
                case CfgDetectionState.Ready:
                    return LocalizationManager.Text("CfgReady");
                case CfgDetectionState.Missing:
                    return LocalizationManager.Text("CfgMissing");
                case CfgDetectionState.Error:
                    return LocalizationManager.Text("CfgCheckFailed");
                default:
                    return LocalizationManager.Text("CfgNotChecked");
            }
        }

        private static string ResolveCfgHintText(CfgDetectionState state, string detail)
        {
            if (state == CfgDetectionState.NotSelected)
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            if (state == CfgDetectionState.Error)
            {
                return string.IsNullOrWhiteSpace(detail)
                    ? LocalizationManager.Text("CfgWrongFolderHint")
                    : detail;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return LocalizationManager.Text("CfgSelectRootHint");
            }

            return LocalizationManager.Text("CfgSavedFolderPrefix") + detail;
        }

        private void OnBrightnessSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private void OnContrastSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private async void OnAudioVolumeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ApplyAndSaveAudioVolumeAsync();
        }

        private void OnResetVisualAdjustmentsClick(object sender, RoutedEventArgs e)
        {
            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, DefaultBrightnessValue);
            SelectPercentageOption(ContrastSelector, DefaultContrastValue);
            _suppressVisualAdjustmentEvents = false;
            ApplyVisualAdjustmentSettings();
        }

        private void LoadVisualAdjustmentSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            double brightness = ReadSetting(localSettings, BrightnessSettingKey);
            double contrast = ReadSetting(localSettings, ContrastSettingKey);
            double audioVolume = ReadSetting(localSettings, AudioVolumeSettingKey);

            _suppressVisualAdjustmentEvents = true;
            SelectPercentageOption(BrightnessSelector, brightness);
            SelectPercentageOption(ContrastSelector, contrast);
            SelectPercentageOption(AudioVolumeSelector, audioVolume);
            _suppressVisualAdjustmentEvents = false;

            UpdateVisualAdjustmentLabels(brightness, contrast);
            ApplyVisualAdjustmentSettings();
            _ = ApplyAndSaveAudioVolumeAsync();
        }

        private void ApplyVisualAdjustmentSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double brightness = ReadSelectedPercentage(BrightnessSelector, DefaultBrightnessValue);
            double contrast = ReadSelectedPercentage(ContrastSelector, DefaultContrastValue);

            Controls.KillConfirmAnimation.ConfigureRenderSettings(brightness / 100.0, contrast / 100.0);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[BrightnessSettingKey] = brightness;
            localSettings.Values[ContrastSettingKey] = contrast;
            UpdateVisualAdjustmentLabels(brightness, contrast);

            if (_isPageActive)
            {
                _ = WarmStartupAnimationCacheAsync();
            }
        }

        private async Task ApplyAndSaveAudioVolumeAsync()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double volume = ReadSelectedPercentage(AudioVolumeSelector, DefaultAudioVolumeValue);
            ApplicationData.Current.LocalSettings.Values[AudioVolumeSettingKey] = volume;

            try
            {
                await EnsureServiceAvailableAsync();
                string payload = "{\"percent\":" + Math.Max(0, Math.Min(200, (int)Math.Round(volume))) + "}";

                using (var client = new HttpClient())
                using (var content = new HttpStringContent(payload, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(AudioVolumeUri, content))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        App.Log("Set audio volume failed: status=" + response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Log("Set audio volume failed: " + ex);
            }
        }

        private static double ReadSelectedPercentage(ComboBox selector, double fallback)
        {
            if (selector.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && double.TryParse(tag, out double value))
            {
                return value;
            }

            return fallback;
        }

        private static void SelectPercentageOption(ComboBox selector, double value)
        {
            double rounded = Math.Round(value / 10.0) * 10.0;

            foreach (object option in selector.Items)
            {
                if (option is ComboBoxItem item
                    && item.Tag is string tag
                    && double.TryParse(tag, out double optionValue)
                    && Math.Abs(optionValue - rounded) < 0.1)
                {
                    selector.SelectedItem = item;
                    return;
                }
            }

            selector.SelectedIndex = 0;
        }

        private static double ReadSetting(ApplicationDataContainer settings, string key)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    switch (key)
                    {
                        case BrightnessSettingKey:
                            return DefaultBrightnessValue;
                        case ContrastSettingKey:
                            return DefaultContrastValue;
                        case AudioVolumeSettingKey:
                            return DefaultAudioVolumeValue;
                        default:
                            return 0;
                    }
            }
        }

        private void LoadAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            string placement = localSettings.Values[AnimationPlacementSettingKey] as string;

            if (string.Equals(placement, nameof(AnimationPlacementMode.Bottom), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Bottom;
            }
            else if (string.Equals(placement, nameof(AnimationPlacementMode.Manual), StringComparison.OrdinalIgnoreCase))
            {
                _animationPlacement = AnimationPlacementMode.Manual;
            }
            else
            {
                _animationPlacement = AnimationPlacementMode.Center;
            }

            _animationOffset = ReadDoubleSetting(localSettings, AnimationOffsetSettingKey, 0);
            _animationScale = Math.Max(0.35, Math.Min(3.0, ReadDoubleSetting(localSettings, AnimationScaleSettingKey, 1.0)));
            ApplyAnimationTransform();
        }

        private void SaveAnimationPlacementSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[AnimationPlacementSettingKey] = _animationPlacement.ToString();
            localSettings.Values[AnimationOffsetSettingKey] = _animationOffset;
            localSettings.Values[AnimationScaleSettingKey] = _animationScale;
        }

        private static double ReadDoubleSetting(ApplicationDataContainer settings, string key, double fallback)
        {
            object rawValue = settings.Values[key];
            switch (rawValue)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
                default:
                    return fallback;
            }
        }

        private static string GetDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch (Exception)
            {
                return "v?";
            }
        }

        private static string GetCompactDisplayVersion()
        {
            try
            {
                PackageVersion version = Package.Current.Id.Version;
                return $"v{version.Revision}";
            }
            catch (Exception)
            {
                return "v?";
            }
        }

        private void UpdateStreakBadge(KillEvent killEvent)
        {
            if (killEvent == null || !killEvent.PlayMainAnimation || killEvent.KillCount < 7)
            {
                HideStreakBadge();
                return;
            }

            string streakText = killEvent.KillCount > 9
                ? "x9+"
                : $"x{killEvent.KillCount}";

            StreakBadgeText.Text = streakText;
            StreakBadge.Visibility = Visibility.Visible;
            StreakBadge.Opacity = 1.0;
            _streakBadgeTimer.Stop();
            _streakBadgeTimer.Start();
        }

        private void HideStreakBadge()
        {
            StreakBadge.Visibility = Visibility.Collapsed;
            StreakBadge.Opacity = 0.0;
        }

        private void UpdateVisualAdjustmentLabels(double brightness, double contrast)
        {
        }

        private enum AnimationPlacementMode
        {
            Center,
            Manual,
            Bottom
        }

        private enum CfgDetectionState
        {
            NotSelected,
            Checking,
            Ready,
            Missing,
            Error
        }

        private sealed class StatusHint
        {
            public StatusHint(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }

            public Color Color { get; }
        }

        private sealed class TestPreset
        {
            public TestPreset(
                int killCount,
                bool isHeadshot = false,
                bool isKnifeKill = false,
                bool isFirstKill = false,
                bool isLastKill = false,
                bool playMainAnimation = true)
            {
                KillCount = killCount;
                IsHeadshot = isHeadshot;
                IsKnifeKill = isKnifeKill;
                IsFirstKill = isFirstKill;
                IsLastKill = isLastKill;
                PlayMainAnimation = playMainAnimation;
            }

            public int KillCount { get; }
            public bool IsHeadshot { get; }
            public bool IsKnifeKill { get; }
            public bool IsFirstKill { get; }
            public bool IsLastKill { get; }
            public bool PlayMainAnimation { get; }

            public KillEvent ToKillEvent()
            {
                return new KillEvent
                {
                    KillCount = KillCount,
                    IsHeadshot = IsHeadshot,
                    IsKnifeKill = IsKnifeKill,
                    IsFirstKill = IsFirstKill,
                    IsLastKill = IsLastKill,
                    PlayMainAnimation = PlayMainAnimation
                };
            }
        }
    }
}
