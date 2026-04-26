using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TestXboxGameBar.Services;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

namespace TestXboxGameBar
{
    public sealed partial class ClockWidgetPage : Page
    {
        private static readonly Size DefaultWidgetSize = new Size(240, 180);
        private static readonly Size MinWidgetSize = new Size(160, 120);
        private static readonly Size MaxWidgetSize = new Size(400, 720);
        private const double AnimationOffsetStep = 12.0;
        private const double MaxAnimationOffsetRatio = 0.45;
        private const double ScaleUpFactor = 1.1;
        private const double ScaleDownFactor = 0.9;
        private const int StartupPreloadDelayMs = 1200;
        private const int ExtendedPreloadDelayMs = 5000;
        private const int StreakBadgeDisplayDurationMs = 1800;
        private const int DefaultPreviewKillCount = 2;
        private const double DefaultBrightnessValue = 70;
        private const double DefaultContrastValue = 80;
        private const string FirstKillAssetKey = "firstkill";
        private const string GoldHeadshotAssetKey = "goldheadshot";
        private const string HeadshotAssetKey = "headshot_silver";
        private const string KnifeKillAssetKey = "knife_kill";
        private const string LastKillAssetKey = "last_kill";
        private const string BrightnessSettingKey = "AnimationBrightness";
        private const string ContrastSettingKey = "AnimationContrast";
        private const string VoicePackSettingKey = "VoicePack";
        private const int ControlPanelStateRefreshMs = 250;
        private const string PackagedServiceParameterGroupId = "CrossfirePreset";
        private static readonly Uri ServiceHealthUri = new Uri("http://127.0.0.1:3000/health");
        private static readonly Uri SoundPackUri = new Uri("http://127.0.0.1:3000/soundpack");
        private static readonly TimeSpan ServiceStartupTimeout = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan ServiceStartupPollInterval = TimeSpan.FromMilliseconds(250);
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
        private bool _isPageActive;
        private readonly DispatcherTimer _controlPanelStateTimer;
        private readonly DispatcherTimer _streakBadgeTimer;

        public ClockWidgetPage()
        {
            InitializeComponent();
            AnimationLayer.SizeChanged += OnAnimationLayerSizeChanged;
            VersionText.Text = GetDisplayVersion();

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
            LoadVoicePackSetting();
            _controlPanelStateTimer.Start();

            StartKillEventClient();
            ConfigureWidgetCapabilities();
            _ = EnsureServiceAvailableAsync();
            _ = WarmStartupAnimationCacheAsync();
            _ = WarmExtendedAnimationCacheAsync();
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
            _widget = null;
            HideStreakBadge();

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
            _animationPlacement = AnimationPlacementMode.LowerThird;
            ApplyAnimationOffset();
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

        private async void OnCheckServerClick(object sender, RoutedEventArgs e)
        {
            await CheckServerHealthAsync();
        }

        private async void OnStartServiceClick(object sender, RoutedEventArgs e)
        {
            await EnsureServiceAvailableAsync();
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
                    await SyncSelectedVoicePackAsync();
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
                await SyncSelectedVoicePackAsync();
            }
        }

        private static async Task<bool> TryLaunchPackagedServiceAsync()
        {
            try
            {
                App.Log("Launching packaged KillConfirm service. group=" + PackagedServiceParameterGroupId);
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync(PackagedServiceParameterGroupId);
                App.Log("Packaged service launch call returned without exception.");
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
                string preset = json.GetNamedString("preset", GetSelectedVoicePackPreset());
                ApplicationData.Current.LocalSettings.Values[VoicePackSettingKey] = preset;
                SelectVoicePackPreset(preset);
            }
            catch (Exception)
            {
            }
        }

        private async Task WarmStartupAnimationCacheAsync()
        {
            try
            {
                await Task.Delay(StartupPreloadDelayMs);
                await PrimaryKillAnimation.PreloadStartupAnimationsAsync();
                await BadgeKillAnimation.PreloadStartupAnimationsAsync();
            }
            catch (Exception)
            {
            }
        }

        private async Task WarmExtendedAnimationCacheAsync()
        {
            try
            {
                await Task.Delay(ExtendedPreloadDelayMs);
                await PrimaryKillAnimation.PreloadCommonAnimationsAsync();
                await BadgeKillAnimation.PreloadCommonAnimationsAsync();
            }
            catch (Exception)
            {
            }
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
        }

        private void ApplyAnimationOffset()
        {
            ApplyAnimationTransform();
        }

        private void ScaleAnimation(double factor)
        {
            _animationScale *= factor;
            ApplyAnimationTransform();
        }

        private void ApplyAnimationTransform()
        {
            AnimationTransform.ScaleX = _animationScale;
            AnimationTransform.ScaleY = _animationScale;
            AnimationTransform.TranslateY = GetResolvedAnimationOffset();
        }

        private void OnAnimationLayerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_animationPlacement == AnimationPlacementMode.LowerThird)
            {
                ApplyAnimationOffset();
            }
        }

        private double GetResolvedAnimationOffset()
        {
            switch (_animationPlacement)
            {
                case AnimationPlacementMode.LowerThird:
                    return GetLowerThirdOffset();
                case AnimationPlacementMode.Center:
                    return 0;
                default:
                    return _animationOffset;
            }
        }

        private double GetLowerThirdOffset()
        {
            double layerHeight = AnimationLayer.ActualHeight;
            if (layerHeight <= 0)
            {
                layerHeight = DefaultWidgetSize.Height;
            }

            return layerHeight / 6.0;
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
            bool isMinimized = _windowState == XboxGameBarWidgetWindowState.Minimized;
            bool showControlPanel =
                _isWidgetVisible &&
                _displayMode == XboxGameBarDisplayMode.Foreground &&
                !isMinimized;

            ControlPanel.Visibility = showControlPanel ? Visibility.Visible : Visibility.Collapsed;
            ControlPanel.IsHitTestVisible = showControlPanel;
            ControlPanel.Opacity = showControlPanel ? 1.0 : 0.0;
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
            switch (state)
            {
                case KillEventConnectionState.Connected:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 52, 211, 153));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, "Service running");
                    break;
                case KillEventConnectionState.Connecting:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 251, 191, 36));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, "Service starting");
                    break;
                default:
                    ConnectionDot.Background = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113));
                    ToolTipService.SetToolTip(ConnectionStatusBadge, "Service offline");
                    break;
            }
        }

        private void OnBrightnessValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private void OnContrastValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ApplyVisualAdjustmentSettings();
        }

        private void OnResetVisualAdjustmentsClick(object sender, RoutedEventArgs e)
        {
            _suppressVisualAdjustmentEvents = true;
            BrightnessSlider.Value = DefaultBrightnessValue;
            ContrastSlider.Value = DefaultContrastValue;
            _suppressVisualAdjustmentEvents = false;
            ApplyVisualAdjustmentSettings();
        }

        private void LoadVisualAdjustmentSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            double brightness = ReadSetting(localSettings, BrightnessSettingKey);
            double contrast = ReadSetting(localSettings, ContrastSettingKey);

            _suppressVisualAdjustmentEvents = true;
            BrightnessSlider.Value = brightness;
            ContrastSlider.Value = contrast;
            _suppressVisualAdjustmentEvents = false;

            UpdateVisualAdjustmentLabels(brightness, contrast);
            ApplyVisualAdjustmentSettings();
        }

        private void ApplyVisualAdjustmentSettings()
        {
            if (_suppressVisualAdjustmentEvents)
            {
                return;
            }

            double brightness = BrightnessSlider.Value;
            double contrast = ContrastSlider.Value;

            Controls.KillConfirmAnimation.ConfigureRenderSettings(brightness / 100.0, contrast / 100.0);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[BrightnessSettingKey] = brightness;
            localSettings.Values[ContrastSettingKey] = contrast;
            UpdateVisualAdjustmentLabels(brightness, contrast);
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
                    return key == BrightnessSettingKey
                        ? DefaultBrightnessValue
                        : DefaultContrastValue;
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
            LowerThird
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
