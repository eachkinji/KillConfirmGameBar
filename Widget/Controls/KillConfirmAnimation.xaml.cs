using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using TestXboxGameBar.Services;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestXboxGameBar.Controls
{
    public sealed partial class KillConfirmAnimation : UserControl
    {
        private const string HeadshotAssetKey = "headshot_silver";
        private const string OneKillRemasterAssetKey = "1killre";
        private const string TwoKillRemasterAssetKey = "2killre";
        private const string ThreeKillRemasterAssetKey = "3killre";
        private const string FourKillRemasterAssetKey = "4killre";
        private const string FiveKillRemasterAssetKey = "5killre";
        private const string SixKillRemasterAssetKey = "6killre";
        private const string FirstKillAssetKey = "firstkill";
        private const string GoldHeadshotAssetKey = "goldheadshot";
        private const string KnifeKillAssetKey = "knife_kill";
        private const string LastKillAssetKey = "last_kill";
        private const string DefaultCodeFolder = "Original";
        private const string VipCodeFolder = "Vip";
        private const string AngelicBeastCodeFolder = "AngelicBeast";
        private const string KnifeCodeFolder = "Knife";
        private const string FirstLastCodeFolder = "FirstLast";
        private const string CommonFxCodeFolder = "CommonFx";
        private const string EliteUpgradeCodeFolder = "EliteUpgrade";
        private const string WeaponBadgeCodeFolder = "WeaponBadge";
        private const int FrameSequenceFps = 60;
        private const double TargetPlaybackFrames = 77.0;
        private const int LoadingIndicatorDelayMs = 250;
        private const int MaxCachedFrameWidth = 400;
        private const int MaxCachedFrameHeight = 300;
        private const double CodeKillFrameWidth = 607;
        private const double CodeKillFrameHeight = 436;
        private static double _brightnessBoost;
        private static double _contrastBoost;
        private static double _targetPlaybackFps = FrameSequenceFps;
        private static string _iconPack = "default";
        private static int _eliteEffectLevel;
        private static bool _weaponBadgeEnabled;
        private static int _mainAnimationStyle = 1;

        private static readonly Dictionary<string, SpriteMetadata> MetadataCache = new Dictionary<string, SpriteMetadata>();
        private static readonly Dictionary<string, IReadOnlyList<SpriteSheetSegment>> SheetCache = new Dictionary<string, IReadOnlyList<SpriteSheetSegment>>();
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _playbackClock = new Stopwatch();

        private SpriteMetadata _currentMetadata;
        private IReadOnlyList<SpriteSheetSegment> _currentSheets;
        private SpriteSheetSegment _currentSheet;
        private Code2KillAsset _currentCodeAsset;
        private static readonly Dictionary<string, Code2KillAsset> CodeKillCache = new Dictionary<string, Code2KillAsset>();
        private static Task _startupPreloadTask;
        private static Task _preloadTask;
        private int _currentFrame;
        private int _playToken;

        public KillConfirmAnimation()
        {
            InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }

        public void Play(int killCount, bool isHeadshot = false)
        {
            int normalizedKillCount = Math.Max(1, killCount);
            PlayInternal(progress => LoadPreferredAssetAsync(normalizedKillCount, isHeadshot, progress));
        }

        public void PlayNamed(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            PlayInternal(progress => LoadNamedAssetAsync(assetKey, progress));
        }

        public void PlayCode2Kill()
        {
            PlayCodeKill("multi2");
        }

        public void PlayCodeKill(string assetName, string weaponBadgeKey = null)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            PlayInternal(progress => LoadCodeKillAssetAsync(assetName, weaponBadgeKey, progress));
        }

        public Task PreloadCommonAnimationsAsync()
        {
            if (_preloadTask == null)
            {
                _preloadTask = PreloadCommonAnimationsCoreAsync();
            }

            return _preloadTask;
        }

        public Task PreloadStartupAnimationsAsync()
        {
            if (_startupPreloadTask == null)
            {
                _startupPreloadTask = PreloadGameplayAnimationsAsync(null);
            }

            return _startupPreloadTask;
        }

        public Task PreloadGameplayAnimationsAsync(IProgress<int> progress)
        {
            return PreloadSelectedAnimationsAsync(
                new[]
                {
                    OneKillRemasterAssetKey,
                    TwoKillRemasterAssetKey,
                    ThreeKillRemasterAssetKey,
                    FourKillRemasterAssetKey,
                    FiveKillRemasterAssetKey,
                    SixKillRemasterAssetKey,
                    HeadshotAssetKey,
                    GoldHeadshotAssetKey,
                    FirstKillAssetKey,
                    KnifeKillAssetKey,
                    LastKillAssetKey
                },
                progress);
        }

        public static void ConfigureRenderSettings(double brightnessBoost, double contrastBoost)
        {
            double normalizedBrightness = Math.Max(0.0, Math.Min(1.0, brightnessBoost));
            double normalizedContrast = Math.Max(0.0, Math.Min(1.0, contrastBoost));

            if (Math.Abs(_brightnessBoost - normalizedBrightness) < 0.0001
                && Math.Abs(_contrastBoost - normalizedContrast) < 0.0001)
            {
                return;
            }

            _brightnessBoost = normalizedBrightness;
            _contrastBoost = normalizedContrast;
            CodeKillCache.Clear();
            if (string.Equals(_iconPack, "legacy", StringComparison.OrdinalIgnoreCase))
            {
                SheetCache.Clear();
                _startupPreloadTask = null;
                _preloadTask = null;
            }
        }

        public static void ConfigurePlaybackFps(double playbackFps)
        {
            _targetPlaybackFps = Math.Max(1.0, Math.Min(240.0, playbackFps));
        }

        public static void ConfigureIconPack(string iconPack)
        {
            string normalized = string.IsNullOrWhiteSpace(iconPack)
                ? "default"
                : iconPack.Trim().ToLowerInvariant();
            if (normalized != "angelic_beast" && normalized != "legacy" && normalized != "vip")
            {
                normalized = "default";
            }

            if (string.Equals(_iconPack, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool legacyTransition = string.Equals(_iconPack, "legacy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "legacy", StringComparison.OrdinalIgnoreCase);
            _iconPack = normalized;
            CodeKillCache.Clear();
            if (legacyTransition)
            {
                SheetCache.Clear();
                _startupPreloadTask = null;
                _preloadTask = null;
            }
        }

        public static void ConfigureEliteEffectLevel(int eliteLevel)
        {
            int normalized = Math.Max(0, Math.Min(3, eliteLevel));
            if (_eliteEffectLevel == normalized)
            {
                return;
            }

            _eliteEffectLevel = normalized;
            CodeKillCache.Clear();
        }

        public static void ConfigureWeaponBadgeEnabled(bool enabled)
        {
            if (_weaponBadgeEnabled == enabled)
            {
                return;
            }

            _weaponBadgeEnabled = enabled;
            CodeKillCache.Clear();
        }

        public static void ConfigureMainAnimationStyle(int style)
        {
            int normalized = Math.Max(1, Math.Min(2, style));
            if (_mainAnimationStyle == normalized)
            {
                return;
            }

            _mainAnimationStyle = normalized;
        }

        private async void PlayInternal(Func<IProgress<int>, Task<AnimationAsset>> assetLoader)
        {
            int token = ++_playToken;
            bool isLoading = true;
            var progress = new Progress<int>(value =>
            {
                if (isLoading && token == _playToken)
                {
                    ShowLoadingProgress(value);
                }
            });

            try
            {
                _ = ShowLoadingProgressIfStillLoadingAsync(token, progress);
                AnimationAsset asset = await assetLoader(progress);

                if (token != _playToken)
                {
                    return;
                }

                isLoading = false;
                _timer.Stop();
                _currentMetadata = asset.Metadata;
                _currentSheets = asset.Sheets;
                _currentCodeAsset = asset.CodeAsset;
                _currentSheet = null;
                _currentFrame = 0;

                Viewport.Width = asset.Metadata.FrameWidth;
                Viewport.Height = asset.Metadata.FrameHeight;
                ViewportClip.Rect = new Rect(0, 0, asset.Metadata.FrameWidth, asset.Metadata.FrameHeight);

                SpriteCanvas.Width = asset.Metadata.FrameWidth;
                SpriteCanvas.Height = asset.Metadata.FrameHeight;

                HideLoadingProgress();
                Visibility = Visibility.Visible;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / FrameSequenceFps);
                ShowFrame(0);
                _playbackClock.Restart();
                _timer.Start();
            }
            catch
            {
                isLoading = false;
                HideLoadingProgress();
                Visibility = Visibility.Collapsed;
            }
        }

        private async Task<AnimationAsset> LoadPreferredAssetAsync(int spriteNumber, bool isHeadshot, IProgress<int> progress)
        {
            if (isHeadshot)
            {
                try
                {
                    return await LoadNamedAssetAsync(HeadshotAssetKey, progress);
                }
                catch
                {
                }
            }

            string remasteredAssetKey = GetRemasteredKillAssetKey(spriteNumber);
            if (!string.IsNullOrWhiteSpace(remasteredAssetKey))
            {
                try
                {
                    return await LoadNamedAssetAsync(remasteredAssetKey, progress);
                }
                catch
                {
                }
            }

            throw new FileNotFoundException("No animation asset was found for kill count " + spriteNumber);
        }

        private static string GetRemasteredKillAssetKey(int killCount)
        {
            switch (Math.Max(1, Math.Min(9, killCount)))
            {
                case 1:
                    return OneKillRemasterAssetKey;
                case 2:
                    return TwoKillRemasterAssetKey;
                case 3:
                    return ThreeKillRemasterAssetKey;
                case 4:
                    return FourKillRemasterAssetKey;
                case 5:
                    return FiveKillRemasterAssetKey;
                case 6:
                case 7:
                case 8:
                case 9:
                default:
                    return SixKillRemasterAssetKey;
            }
        }

        private async Task PreloadCommonAnimationsCoreAsync()
        {
            string[] extraAssets =
            {
                OneKillRemasterAssetKey,
                TwoKillRemasterAssetKey,
                ThreeKillRemasterAssetKey,
                FourKillRemasterAssetKey,
                FiveKillRemasterAssetKey,
                SixKillRemasterAssetKey,
                GoldHeadshotAssetKey,
                HeadshotAssetKey
            };

            foreach (string assetKey in extraAssets)
            {
                try
                {
                    await LoadNamedAssetAsync(assetKey, null);
                }
                catch
                {
                }
            }
        }

        private async Task PreloadSelectedAnimationsAsync(IEnumerable<string> assetKeys, IProgress<int> progress = null)
        {
            string[] keys = assetKeys.ToArray();
            int loaded = 0;
            progress?.Report(0);

            foreach (string assetKey in keys)
            {
                try
                {
                    await LoadNamedAssetAsync(assetKey, null);
                }
                catch
                {
                }

                loaded++;
                int percent = keys.Length == 0
                    ? 100
                    : (int)Math.Round(loaded * 100.0 / keys.Length);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }
        }

        private async Task<AnimationAsset> LoadNamedAssetAsync(string assetKey, IProgress<int> progress = null)
        {
            switch (assetKey)
            {
                case HeadshotAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(HeadshotAssetKey, progress);
                case OneKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(OneKillRemasterAssetKey, progress);
                case TwoKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(TwoKillRemasterAssetKey, progress);
                case ThreeKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(ThreeKillRemasterAssetKey, progress);
                case FourKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FourKillRemasterAssetKey, progress);
                case FiveKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FiveKillRemasterAssetKey, progress);
                case SixKillRemasterAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(SixKillRemasterAssetKey, progress);
                case FirstKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(FirstKillAssetKey, progress);
                case GoldHeadshotAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(GoldHeadshotAssetKey, progress);
                case KnifeKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(KnifeKillAssetKey, progress);
                case LastKillAssetKey:
                    return await LoadTiledSpriteSheetAssetAsync(LastKillAssetKey, progress);
                default:
                    throw new FileNotFoundException("Unsupported animation asset: " + assetKey);
            }
        }

        private async Task<AnimationAsset> LoadTiledSpriteSheetAssetAsync(string assetName, IProgress<int> progress = null)
        {
            SpriteMetadata metadata = await LoadTiledSpriteSheetMetadataAsync(assetName);
            IReadOnlyList<SpriteSheetSegment> sheets = await LoadTiledSpriteSheetSegmentsAsync(assetName, metadata, progress);
            return new AnimationAsset(metadata, sheets);
        }

        private async Task<SpriteMetadata> LoadTiledSpriteSheetMetadataAsync(string assetName)
        {
            string cacheKey = "tiled-sheet:" + assetName;
            if (MetadataCache.TryGetValue(cacheKey, out SpriteMetadata cached))
            {
                return cached;
            }

            var uri = new Uri($"ms-appx:///Assets/KillConfirmSheets/{assetName}.json");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            string jsonText = await FileIO.ReadTextAsync(file);
            JsonObject json = JsonObject.Parse(jsonText);

            var metadata = new SpriteMetadata
            {
                FrameWidth = (int)json.GetNamedNumber("frame_width", 400),
                FrameHeight = (int)json.GetNamedNumber("frame_height", 300),
                Frames = (int)json.GetNamedNumber("frames", 1),
                Fps = Math.Max(1, (int)json.GetNamedNumber("fps", FrameSequenceFps)),
                SheetSegments = json.GetNamedArray("sheets", new JsonArray())
            };

            MetadataCache[cacheKey] = metadata;
            return metadata;
        }

        private async Task<IReadOnlyList<SpriteSheetSegment>> LoadTiledSpriteSheetSegmentsAsync(string assetName, SpriteMetadata metadata, IProgress<int> progress)
        {
            string cacheKey = "tiled-sheet:" + assetName;
            if (SheetCache.TryGetValue(cacheKey, out IReadOnlyList<SpriteSheetSegment> cached))
            {
                progress?.Report(100);
                return cached;
            }

            var segments = new List<SpriteSheetSegment>();
            JsonArray sheetArray = metadata.SheetSegments ?? new JsonArray();
            for (uint index = 0; index < sheetArray.Count; index++)
            {
                JsonObject item = sheetArray.GetObjectAt(index);
                string fileName = item.GetNamedString("file", string.Empty);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                CanvasBitmap bitmap = await LoadSheetBitmapAsync(fileName);
                segments.Add(new SpriteSheetSegment
                {
                    Image = bitmap,
                    StartFrame = (int)item.GetNamedNumber("start_frame", 0),
                    Frames = (int)item.GetNamedNumber("frames", 0),
                    Cols = Math.Max(1, (int)item.GetNamedNumber("cols", 1)),
                    Rows = Math.Max(1, (int)item.GetNamedNumber("rows", 1)),
                    Width = (int)item.GetNamedNumber("width", bitmap.SizeInPixels.Width),
                    Height = (int)item.GetNamedNumber("height", bitmap.SizeInPixels.Height)
                });

                int percent = sheetArray.Count == 0
                    ? 100
                    : (int)Math.Round(((index + 1) * 100.0) / sheetArray.Count);
                progress?.Report(Math.Max(1, Math.Min(100, percent)));
            }

            SheetCache[cacheKey] = segments;
            return segments;
        }

        private async Task ShowLoadingProgressIfStillLoadingAsync(int token, IProgress<int> progress)
        {
            await Task.Delay(LoadingIndicatorDelayMs);
            if (token == _playToken)
            {
                progress?.Report(0);
            }
        }

        private async Task<AnimationAsset> LoadCodeKillAssetAsync(string assetName, string weaponBadgeKey, IProgress<int> progress = null)
        {
            string normalizedAssetName = assetName.Trim().ToLowerInvariant();
            string normalizedWeaponBadgeKey = NormalizeWeaponBadgeKey(weaponBadgeKey);
            if (!TryGetCodeKillFiles(
                normalizedAssetName,
                out string mainFileName,
                out string mainFolder,
                out string alternatePackFolder,
                out string fxFileName,
                out string fxFolder))
            {
                throw new FileNotFoundException("Unsupported code kill asset: " + assetName);
            }

            string cacheKey = _iconPack + ":" + normalizedAssetName + ":" + normalizedWeaponBadgeKey;
            if (!CodeKillCache.TryGetValue(cacheKey, out Code2KillAsset asset))
            {
                string effectiveMainFileName = GetEffectiveMainFileName(normalizedAssetName, mainFileName);
                CanvasBitmap main = await LoadCodeKillBitmapAsync(effectiveMainFileName, mainFolder, alternatePackFolder, true);
                progress?.Report(50);
                CanvasBitmap fx = string.IsNullOrWhiteSpace(fxFileName) || !SupportsKillFxOverlay()
                    ? null
                    : await LoadCodeKillBitmapAsync(fxFileName, fxFolder, null, false);
                CanvasBitmap eliteOverlay = await LoadEliteOverlayBitmapAsync(normalizedAssetName);
                CanvasBitmap weaponBadgeOverlay = await LoadWeaponBadgeOverlayBitmapAsync(normalizedAssetName, normalizedWeaponBadgeKey);
                asset = new Code2KillAsset(main, fx, eliteOverlay, weaponBadgeOverlay);
                CodeKillCache[cacheKey] = asset;
            }

            progress?.Report(100);
            return new AnimationAsset(
                new SpriteMetadata
                {
                    FrameWidth = (int)CodeKillFrameWidth,
                    FrameHeight = (int)CodeKillFrameHeight,
                    Frames = 77,
                    Fps = FrameSequenceFps
                },
                asset);
        }

        private static bool TryGetCodeKillFiles(
            string assetName,
            out string mainFileName,
            out string mainFolder,
            out string alternatePackFolder,
            out string fxFileName,
            out string fxFolder)
        {
            switch (assetName)
            {
                case "multi1":
                    mainFileName = "badge_multi1.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "multi2":
                case "code2kill":
                    mainFileName = "badge_multi2.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi2_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi3":
                    mainFileName = "badge_multi3.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi3_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi4":
                    mainFileName = "badge_multi4.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi4_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi5":
                    mainFileName = "badge_multi5.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi5_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "multi6":
                    mainFileName = "badge_multi6.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = "multi6_fx.png";
                    fxFolder = CommonFxCodeFolder;
                    return true;
                case "headshot":
                    mainFileName = "badge_headshot.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_gold":
                    mainFileName = "badge_headshot_gold.png";
                    mainFolder = DefaultCodeFolder;
                    alternatePackFolder = AngelicBeastCodeFolder;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "knife":
                    mainFileName = "badge_knife.png";
                    mainFolder = KnifeCodeFolder;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "firstkill":
                    mainFileName = "FIRSTKILL.png";
                    mainFolder = FirstLastCodeFolder;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "lastkill":
                    mainFileName = "LASTKILL.png";
                    mainFolder = FirstLastCodeFolder;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_vvip":
                    mainFileName = "badge_headshot_vvip.png";
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                case "headshot_gold_vvip":
                    mainFileName = "badge_headshot_gold_vvip.png";
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return true;
                default:
                    mainFileName = null;
                    mainFolder = null;
                    alternatePackFolder = null;
                    fxFileName = null;
                    fxFolder = null;
                    return false;
            }
        }

        private static async Task<CanvasBitmap> LoadCodeKillBitmapAsync(
            string fileName,
            string folder,
            string alternatePackFolder,
            bool allowDefaultFallback)
        {
            if (PackCatalogService.IsImportedIconPackKey(_iconPack))
            {
                CanvasBitmap imported = await TryLoadImportedIconBitmapAsync(fileName);
                if (imported != null)
                {
                    return imported;
                }
            }

            string iconPackFolder = GetIconPackFolder();
            if (!string.IsNullOrWhiteSpace(alternatePackFolder)
                && !string.IsNullOrWhiteSpace(iconPackFolder))
            {
                try
                {
                    return await LoadBitmapFromApplicationUriAsync(
                        $"ms-appx:///Assets/KillConfirmCode/{iconPackFolder}/{fileName}");
                }
                catch
                {
                    if (!allowDefaultFallback)
                    {
                        throw;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(folder))
            {
                return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmCode/{folder}/{fileName}");
            }

            return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmCode/{fileName}");
        }

        private static async Task<CanvasBitmap> TryLoadImportedIconBitmapAsync(string fileName)
        {
            try
            {
                StorageFolder folder = await PackCatalogService.GetImportedIconFolderAsync(_iconPack);
                if (folder == null)
                {
                    return null;
                }

                StorageFile file = await folder.GetFileAsync(fileName);
                return await LoadBitmapFromStorageFileAsync(file);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<CanvasBitmap> LoadEliteOverlayBitmapAsync(string assetName)
        {
            if (_eliteEffectLevel <= 0 || !SupportsEliteOrWeaponBadges())
            {
                return null;
            }

            if (!assetName.StartsWith("multi", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string fileName = $"KillMark_Upgrade{_eliteEffectLevel}.png";
            return await LoadCodeKillBitmapAsync(fileName, EliteUpgradeCodeFolder, null, false);
        }

        private static async Task<CanvasBitmap> LoadWeaponBadgeOverlayBitmapAsync(string assetName, string weaponBadgeKey)
        {
            if (!_weaponBadgeEnabled
                || !SupportsEliteOrWeaponBadges()
                || !SupportsWeaponBadgeForAsset(assetName)
                || string.IsNullOrWhiteSpace(weaponBadgeKey))
            {
                return null;
            }

            string suffix = GetWeaponBadgeVariantSuffix();
            string fileName;
            switch (weaponBadgeKey)
            {
                case "assault":
                    fileName = $"badge_Assault{suffix}.png";
                    break;
                case "elite":
                    fileName = $"badge_Elite{suffix}.png";
                    break;
                case "scout":
                    fileName = $"badge_Scout{suffix}.png";
                    break;
                case "sniper":
                    fileName = $"badge_Sniper{suffix}.png";
                    break;
                case "knife":
                    fileName = $"badge_Knife{suffix}.png";
                    break;
                default:
                    return null;
            }

            return await LoadCodeKillBitmapAsync(fileName, WeaponBadgeCodeFolder, null, false);
        }

        private static bool SupportsWeaponBadgeForAsset(string assetName)
        {
            return assetName.StartsWith("multi", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetEffectiveMainFileName(string assetName, string defaultMainFileName)
        {
            if (string.Equals(assetName, "knife", StringComparison.OrdinalIgnoreCase)
                && SupportsEliteOrWeaponBadges()
                && _eliteEffectLevel > 0)
            {
                return $"badge_knife_{Math.Min(3, _eliteEffectLevel)}.png";
            }

            return defaultMainFileName;
        }

        private static string GetIconPackFolder()
        {
            switch ((_iconPack ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vip":
                    return VipCodeFolder;
                case "angelic_beast":
                    return AngelicBeastCodeFolder;
                default:
                    return null;
            }
        }

        private static bool SupportsEliteOrWeaponBadges()
        {
            return string.Equals(_iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_iconPack, "vip", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SupportsKillFxOverlay()
        {
            return string.Equals(_iconPack, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_iconPack, "vip", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetWeaponBadgeVariantSuffix()
        {
            return Math.Max(1, Math.Min(3, _eliteEffectLevel)).ToString();
        }

        private static string NormalizeWeaponBadgeKey(string weaponBadgeKey)
        {
            if (string.IsNullOrWhiteSpace(weaponBadgeKey))
            {
                return string.Empty;
            }

            switch (weaponBadgeKey.Trim().ToLowerInvariant())
            {
                case "assault":
                case "elite":
                case "scout":
                case "sniper":
                case "knife":
                    return weaponBadgeKey.Trim().ToLowerInvariant();
                default:
                    return string.Empty;
            }
        }

        private static void ClearSheetCache()
        {
            SheetCache.Clear();
            CodeKillCache.Clear();
        }

        private static async Task<CanvasBitmap> LoadSheetBitmapAsync(string fileName)
        {
            return await LoadBitmapFromApplicationUriAsync($"ms-appx:///Assets/KillConfirmSheets/{fileName}");
        }

        private static async Task<CanvasBitmap> LoadBitmapFromApplicationUriAsync(string uriText)
        {
            var uri = new Uri(uriText);
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            return await LoadBitmapFromStorageFileAsync(file);
        }

        private static async Task<CanvasBitmap> LoadBitmapFromStorageFileAsync(StorageFile file)
        {
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                byte[] data = pixels.DetachPixelData();
                ApplyColorBoost(data);
                return CanvasBitmap.CreateFromBytes(
                    CanvasDevice.GetSharedDevice(),
                    data,
                    (int)decoder.PixelWidth,
                    (int)decoder.PixelHeight,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized);
            }
        }

        private void OnSpriteCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.Clear(Colors.Transparent);

            if (_currentCodeAsset != null)
            {
                DrawCode2KillFrame(args.DrawingSession, _currentFrame);
                return;
            }

            if (_currentMetadata == null || _currentSheet == null || _currentSheet.Image == null)
            {
                return;
            }

            int localFrame = _currentFrame - _currentSheet.StartFrame;
            if (localFrame < 0 || localFrame >= _currentSheet.Frames)
            {
                return;
            }

            int col = localFrame % _currentSheet.Cols;
            int row = localFrame / _currentSheet.Cols;
            var sourceRect = new Rect(
                col * _currentMetadata.FrameWidth,
                row * _currentMetadata.FrameHeight,
                _currentMetadata.FrameWidth,
                _currentMetadata.FrameHeight);
            var targetRect = new Rect(0, 0, _currentMetadata.FrameWidth, _currentMetadata.FrameHeight);

            args.DrawingSession.DrawImage(
                _currentSheet.Image,
                targetRect,
                sourceRect,
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
        }

        private void DrawCode2KillFrame(CanvasDrawingSession drawingSession, int frame)
        {
            if (_currentCodeAsset == null)
            {
                return;
            }

            double timeSec = frame / (double)FrameSequenceFps;
            double mainProgress = Clamp01(timeSec / 1.2833);
            double fxProgress = Clamp01(timeSec / 0.48);

            TransformSample main = SampleMainTrack(frame, mainProgress);

            TransformSample fxTrack = SampleTrack(new[]
            {
                new TransformKey(0.0000, 0, 0, 4.55, 0.94),
                new TransformKey(0.0222, 0, 0, 2.95, 1.00),
                new TransformKey(0.0444, 0, 0, 2.62, 1.00),
                new TransformKey(0.0667, 0, 0, 2.42, 1.00),
                new TransformKey(0.0889, 0, 0, 2.08, 0.98),
                new TransformKey(0.1111, 0, 0, 1.94, 0.96),
                new TransformKey(0.1333, 0, 0, 1.66, 0.92),
                new TransformKey(0.1556, 0, 0, 1.56, 0.88),
                new TransformKey(0.1778, 0, 0, 1.32, 0.82),
                new TransformKey(0.2000, 0, 0, 1.28, 0.78),
                new TransformKey(0.2222, 0, 0, 1.12, 0.74),
                new TransformKey(0.2444, 0, 0, 1.12, 0.70),
                new TransformKey(0.2667, 0, 0, 1.04, 0.68),
                new TransformKey(0.2889, 0, 0, 1.00, 0.66),
                new TransformKey(0.3500, 0, 0, 1.00, 0.66),
                new TransformKey(0.7000, 0, 0, 1.00, 0.62),
                new TransformKey(0.8600, 0, 0, 1.00, 0.24),
                new TransformKey(1.0000, 0, 0, 1.12, 0.00)
            }, fxProgress);

            if (_mainAnimationStyle == 1 || frame >= 5)
            {
                ApplyCode2KillFramePatch(frame, ref main);
            }

            double fillWindow = Math.Max(0, 1 - Math.Abs(fxProgress - 0.24) / 0.14);
            TransformSample fx = new TransformSample(
                main.X + 70,
                main.Y + 70,
                fxTrack.Scale * (1 + 0.28 * fillWindow),
                fxTrack.Opacity);

            if ((_mainAnimationStyle == 1 || frame >= 5) && frame >= 5 && frame <= 15)
            {
                main.Scale = 1.0;
            }

            if ((_mainAnimationStyle == 1 || frame >= 5) && frame >= 16)
            {
                main.X = -180;
                main.Y = -180;
                main.Scale = 1.0;
                fx.X = -110;
                fx.Y = -110;
                fx.Scale = 1.0;
            }

            double fxStackScale = 1.0;
            int fxVisibleLayers = 1;
            double extraAlpha1 = 0;
            double extraAlpha2 = 0;
            double fxOpacityMultiplier = 1.0;

            if (frame >= 0 && frame <= 15)
            {
                double growT = Clamp01(frame / 15.0);
                fxVisibleLayers = frame <= 6 ? 1 : 3;
                fxStackScale = Lerp(1.0, 1.30, growT);
                if (frame >= 7)
                {
                    extraAlpha1 = 0.92;
                    extraAlpha2 = 0.78;
                }
            }
            else if (frame >= 16)
            {
                if (frame <= 35)
                {
                    double settleT = Clamp01((frame - 16) / 19.0);
                    fxVisibleLayers = 3;
                    fxStackScale = Lerp(1.30, 1.0, settleT);
                    extraAlpha1 = 0.92;
                    extraAlpha2 = 0.78;
                    fx.Opacity = 0.66;
                    fxOpacityMultiplier = 1.0 - settleT;
                }
                else
                {
                    fxVisibleLayers = 0;
                    fxStackScale = 1.0;
                    fx.Opacity = 0;
                    fxOpacityMultiplier = 0;
                }
            }

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.Main,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.Overlay,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            DrawCenteredScaledImage(
                drawingSession,
                _currentCodeAsset.WeaponBadge,
                main.X,
                main.Y,
                360,
                360,
                main.Scale,
                main.Opacity);

            CanvasBlend previousBlend = drawingSession.Blend;
            drawingSession.Blend = CanvasBlend.Add;

            double[] layerOpacityMultipliers = { 1, extraAlpha1, extraAlpha2 };
            for (int i = 0; i < 3; i++)
            {
                if (i >= fxVisibleLayers)
                {
                    continue;
                }

                DrawCenteredScaledImage(
                    drawingSession,
                    _currentCodeAsset.Fx,
                    fx.X,
                    fx.Y,
                    220,
                    220,
                    fx.Scale * fxStackScale,
                    fx.Opacity * layerOpacityMultipliers[i] * fxOpacityMultiplier);
            }

            drawingSession.Blend = previousBlend;
        }

        private void ShowSheetFrame(int frame)
        {
            if (_currentSheets == null)
            {
                return;
            }

            SpriteSheetSegment sheet = _currentSheets.FirstOrDefault(value =>
                frame >= value.StartFrame && frame < value.StartFrame + value.Frames);
            if (sheet == null)
            {
                return;
            }

            if (!ReferenceEquals(_currentSheet, sheet))
            {
                _currentSheet = sheet;
            }

            SpriteCanvas.Invalidate();
        }

        private sealed class SpriteSheetSegment
        {
            public CanvasBitmap Image { get; set; }
            public int StartFrame { get; set; }
            public int Frames { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private void ShowLoadingProgress(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            _timer.Stop();
            _playbackClock.Stop();
            _currentSheet = null;
            SpriteCanvas.Invalidate();
            Viewport.Width = MaxCachedFrameWidth;
            Viewport.Height = MaxCachedFrameHeight;
            SpriteCanvas.Width = MaxCachedFrameWidth;
            SpriteCanvas.Height = MaxCachedFrameHeight;
            ViewportClip.Rect = new Rect(0, 0, MaxCachedFrameWidth, MaxCachedFrameHeight);
            LoadingText.Text = $"Loading {percent}%";
            LoadingRing.IsActive = true;
            LoadingOverlay.Visibility = Visibility.Visible;
            Visibility = Visibility.Visible;
        }

        private void HideLoadingProgress()
        {
            LoadingRing.IsActive = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void OnTick(object sender, object e)
        {
            if (_currentMetadata == null || (_currentSheets == null && _currentCodeAsset == null))
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            double targetDurationSeconds = TargetPlaybackFrames / Math.Max(1.0, _targetPlaybackFps);
            double playbackProgress = _playbackClock.Elapsed.TotalSeconds / targetDurationSeconds;
            int elapsedFrame = (int)Math.Floor(playbackProgress * _currentMetadata.Frames);
            if (elapsedFrame <= _currentFrame)
            {
                return;
            }

            if (elapsedFrame >= _currentMetadata.Frames)
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            _currentFrame = elapsedFrame;
            ShowFrame(_currentFrame);
        }

        private void ShowFrame(int frame)
        {
            if (frame < 0)
            {
                return;
            }

            if (_currentCodeAsset != null)
            {
                SpriteCanvas.Invalidate();
                return;
            }

            ShowSheetFrame(frame);
        }

        private static void DrawCenteredScaledImage(
            CanvasDrawingSession drawingSession,
            CanvasBitmap image,
            double x,
            double y,
            double width,
            double height,
            double scale,
            double opacity)
        {
            if (image == null || opacity <= 0 || scale <= 0)
            {
                return;
            }

            double imageWidth = image.SizeInPixels.Width;
            double imageHeight = image.SizeInPixels.Height;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return;
            }

            double fitScale = Math.Min(width / imageWidth, height / imageHeight);
            double scaledWidth = imageWidth * fitScale * scale;
            double scaledHeight = imageHeight * fitScale * scale;
            double anchoredX = (CodeKillFrameWidth / 2.0) + x;
            double anchoredY = (CodeKillFrameHeight / 2.0) + y;
            var target = new Rect(
                anchoredX + (width - scaledWidth) / 2.0,
                anchoredY + (height - scaledHeight) / 2.0,
                scaledWidth,
                scaledHeight);

            var source = new Rect(0, 0, image.SizeInPixels.Width, image.SizeInPixels.Height);
            drawingSession.DrawImage(image, target, source, (float)Math.Max(0.0, Math.Min(1.0, opacity)));
        }

        private static void ApplyCode2KillFramePatch(int frame, ref TransformSample main)
        {
            switch (frame)
            {
                case 0:
                    main.Y += 160;
                    main.Scale *= 0.94;
                    break;
                case 1:
                    main.Scale *= 0.78;
                    break;
                case 2:
                    main.X += 48;
                    main.Y += 83;
                    main.Scale *= 0.69;
                    break;
                case 3:
                    main.Y += 28;
                    main.Scale *= 0.55;
                    break;
                case 4:
                    main.Scale *= 0.55;
                    break;
                case 5:
                    main.X += 6;
                    main.Y += 38;
                    main.Scale *= 0.63;
                    break;
                case 6:
                    main.X += 25;
                    main.Y += 28;
                    main.Scale *= 0.69;
                    break;
                case 7:
                    main.X += 23;
                    main.Y += 33;
                    main.Scale *= 0.77;
                    break;
                case 8:
                    main.X -= 20;
                    main.Y += 20;
                    main.Scale *= 0.87;
                    break;
                case 9:
                    main.X += 6;
                    main.Y += 29;
                    main.Scale *= 0.93;
                    break;
                case 10:
                    main.X -= 6;
                    main.Y += 25;
                    break;
                case 11:
                    main.X -= 18;
                    main.Y += 20;
                    break;
            }
        }

        private static TransformSample SampleTrack(IReadOnlyList<TransformKey> keys, double progress)
        {
            if (progress <= keys[0].Progress)
            {
                return keys[0].ToSample();
            }

            for (int i = 1; i < keys.Count; i++)
            {
                TransformKey previous = keys[i - 1];
                TransformKey next = keys[i];
                if (progress <= next.Progress)
                {
                    double local = (progress - previous.Progress) / Math.Max(0.0001, next.Progress - previous.Progress);
                    return new TransformSample(
                        Lerp(previous.X, next.X, local),
                        Lerp(previous.Y, next.Y, local),
                        Lerp(previous.Scale, next.Scale, local),
                        Lerp(previous.Opacity, next.Opacity, local));
                }
            }

            return keys[keys.Count - 1].ToSample();
        }

        private static TransformSample SampleMainTrack(int frame, double progress)
        {
            if (_mainAnimationStyle == 2 && frame <= 4)
            {
                return SampleTrack(new[]
                {
                    new TransformKey(0.0000, -180, -180, 0.16, 0.00),
                    new TransformKey(0.0180, -180, -180, 0.34, 0.35),
                    new TransformKey(0.0360, -180, -180, 0.58, 0.68),
                    new TransformKey(0.0540, -180, -180, 0.82, 0.90),
                    new TransformKey(0.0720, -180, -180, 1.00, 1.00),
                    new TransformKey(1.0000, -180, -180, 1.00, 1.00)
                }, progress);
            }

            return SampleTrack(new[]
            {
                new TransformKey(0.0000, -180, 96, 4.80, 1.00),
                new TransformKey(0.0222, -180, -180, 2.75, 1.00),
                new TransformKey(0.0444, -164, -196, 2.28, 1.00),
                new TransformKey(0.0667, -159, -202, 2.02, 1.00),
                new TransformKey(0.0889, -167, -194, 1.80, 1.00),
                new TransformKey(0.1111, -162, -198, 1.62, 1.00),
                new TransformKey(0.1333, -170, -191, 1.46, 1.00),
                new TransformKey(0.1556, -166, -194, 1.32, 1.00),
                new TransformKey(0.1778, -172, -188, 1.22, 1.00),
                new TransformKey(0.2000, -169, -190, 1.15, 1.00),
                new TransformKey(0.2222, -174, -186, 1.10, 1.00),
                new TransformKey(0.2444, -172, -187, 1.06, 1.00),
                new TransformKey(0.2667, -176, -184, 1.03, 1.00),
                new TransformKey(0.2889, -175, -184, 1.01, 1.00),
                new TransformKey(0.3111, -179, -181, 1.00, 1.00),
                new TransformKey(0.3500, -180, -180, 1.00, 1.00),
                new TransformKey(0.7143, -180, -180, 1.00, 1.00),
                new TransformKey(0.8571, -180, -180, 1.00, 0.55),
                new TransformKey(1.0000, -180, -180, 1.00, 0.00)
            }, progress);
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private static void ApplyColorBoost(byte[] pixelData)
        {
            if (pixelData == null || pixelData.Length < 4)
            {
                return;
            }

            double brightnessBoost = _brightnessBoost;
            double contrastBoost = _contrastBoost;
            if (brightnessBoost <= 0 && contrastBoost <= 0)
            {
                return;
            }

            for (int index = 0; index <= pixelData.Length - 4; index += 4)
            {
                byte alpha = pixelData[index + 3];
                if (alpha == 0)
                {
                    continue;
                }

                pixelData[index] = AdjustChannel(pixelData[index], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 1] = AdjustChannel(pixelData[index + 1], alpha, brightnessBoost, contrastBoost);
                pixelData[index + 2] = AdjustChannel(pixelData[index + 2], alpha, brightnessBoost, contrastBoost);
            }
        }

        private static byte AdjustChannel(byte premultipliedChannel, byte alpha, double brightnessBoost, double contrastBoost)
        {
            double normalizedAlpha = alpha / 255.0;
            if (normalizedAlpha <= 0)
            {
                return 0;
            }

            double unpremultiplied = Math.Min(1.0, premultipliedChannel / (255.0 * normalizedAlpha));
            if (brightnessBoost > 0)
            {
                double gamma = 1.0 - (brightnessBoost * 0.4);
                unpremultiplied = Math.Pow(unpremultiplied, gamma);
            }

            if (contrastBoost > 0)
            {
                double contrastFactor = 1.0 + (contrastBoost * 1.35);
                unpremultiplied = ((unpremultiplied - 0.5) * contrastFactor) + 0.5;
            }

            unpremultiplied = Math.Max(0.0, Math.Min(1.0, unpremultiplied));
            double repremultiplied = unpremultiplied * normalizedAlpha * 255.0;

            if (repremultiplied <= 0)
            {
                return 0;
            }

            if (repremultiplied >= alpha)
            {
                return alpha;
            }

            return (byte)Math.Round(repremultiplied);
        }

        private sealed class SpriteMetadata
        {
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int Frames { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int Fps { get; set; }
            public JsonArray SheetSegments { get; set; }
        }

        private sealed class AnimationAsset
        {
            public AnimationAsset(SpriteMetadata metadata, IReadOnlyList<SpriteSheetSegment> sheets)
            {
                Metadata = metadata;
                Sheets = sheets;
            }

            public AnimationAsset(SpriteMetadata metadata, Code2KillAsset codeAsset)
            {
                Metadata = metadata;
                Sheets = null;
                CodeAsset = codeAsset;
            }

            public SpriteMetadata Metadata { get; }
            public IReadOnlyList<SpriteSheetSegment> Sheets { get; }
            public Code2KillAsset CodeAsset { get; }
        }

        private sealed class Code2KillAsset
        {
            public Code2KillAsset(CanvasBitmap main, CanvasBitmap fx, CanvasBitmap overlay, CanvasBitmap weaponBadge)
            {
                Main = main;
                Fx = fx;
                Overlay = overlay;
                WeaponBadge = weaponBadge;
            }

            public CanvasBitmap Main { get; }
            public CanvasBitmap Fx { get; }
            public CanvasBitmap Overlay { get; }
            public CanvasBitmap WeaponBadge { get; }
        }

        private readonly struct TransformKey
        {
            public TransformKey(double progress, double x, double y, double scale, double opacity)
            {
                Progress = progress;
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double Progress { get; }
            public double X { get; }
            public double Y { get; }
            public double Scale { get; }
            public double Opacity { get; }

            public TransformSample ToSample()
            {
                return new TransformSample(X, Y, Scale, Opacity);
            }
        }

        private struct TransformSample
        {
            public TransformSample(double x, double y, double scale, double opacity)
            {
                X = x;
                Y = y;
                Scale = scale;
                Opacity = opacity;
            }

            public double X;
            public double Y;
            public double Scale;
            public double Opacity;
        }

    }
}
