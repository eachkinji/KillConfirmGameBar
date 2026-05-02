using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
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
        private const int FrameSequenceFps = 60;
        private const double TargetPlaybackDurationSeconds = 77.0 / FrameSequenceFps;
        private const int LoadingIndicatorDelayMs = 250;
        private const int MaxCachedFrameWidth = 400;
        private const int MaxCachedFrameHeight = 300;
        private static double _brightnessBoost;
        private static double _contrastBoost;

        private static readonly Dictionary<string, SpriteMetadata> MetadataCache = new Dictionary<string, SpriteMetadata>();
        private static readonly Dictionary<string, IReadOnlyList<SpriteSheetSegment>> SheetCache = new Dictionary<string, IReadOnlyList<SpriteSheetSegment>>();
        private readonly DispatcherTimer _timer;
        private readonly Stopwatch _playbackClock = new Stopwatch();

        private SpriteMetadata _currentMetadata;
        private IReadOnlyList<SpriteSheetSegment> _currentSheets;
        private SpriteSheetSegment _currentSheet;
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
                _startupPreloadTask = PreloadSelectedAnimationsAsync(new[]
                {
                    OneKillRemasterAssetKey,
                    TwoKillRemasterAssetKey,
                    HeadshotAssetKey,
                    GoldHeadshotAssetKey,
                    FirstKillAssetKey,
                    KnifeKillAssetKey,
                    LastKillAssetKey
                });
            }

            return _startupPreloadTask;
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
            ClearSheetCache();
            _startupPreloadTask = null;
            _preloadTask = null;
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

        private async Task PreloadSelectedAnimationsAsync(IEnumerable<string> assetKeys)
        {
            foreach (string assetKey in assetKeys)
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

        private static void ClearSheetCache()
        {
            SheetCache.Clear();
        }

        private static async Task<CanvasBitmap> LoadSheetBitmapAsync(string fileName)
        {
            var uri = new Uri($"ms-appx:///Assets/KillConfirmSheets/{fileName}");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);

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

            args.DrawingSession.DrawImage(_currentSheet.Image, targetRect, sourceRect);
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
            if (_currentMetadata == null || _currentSheets == null)
            {
                _timer.Stop();
                _playbackClock.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            double playbackProgress = _playbackClock.Elapsed.TotalSeconds / TargetPlaybackDurationSeconds;
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

            ShowSheetFrame(frame);
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

            public SpriteMetadata Metadata { get; }
            public IReadOnlyList<SpriteSheetSegment> Sheets { get; }
        }

    }
}
