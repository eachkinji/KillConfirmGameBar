using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

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
        private const int FrameSequenceFps = 30;
        private const int MaxCachedFrameWidth = 400;
        private const int MaxCachedFrameHeight = 300;
        private static double _brightnessBoost;
        private static double _contrastBoost;

        private static readonly Dictionary<string, SpriteMetadata> MetadataCache = new Dictionary<string, SpriteMetadata>();
        private static readonly Dictionary<string, IReadOnlyList<WriteableBitmap>> FrameCache = new Dictionary<string, IReadOnlyList<WriteableBitmap>>();
        private readonly DispatcherTimer _timer;

        private SpriteMetadata _currentMetadata;
        private IReadOnlyList<WriteableBitmap> _currentFrames;
        private static ReferenceAnimationProfile _referenceProfile;
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
            PlayInternal(() => LoadPreferredAssetAsync(normalizedKillCount, isHeadshot));
        }

        public void PlayNamed(string assetKey)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            PlayInternal(() => LoadNamedAssetAsync(assetKey));
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
            FrameCache.Clear();
            _startupPreloadTask = null;
            _preloadTask = null;
        }

        private async void PlayInternal(Func<Task<AnimationAsset>> assetLoader)
        {
            int token = ++_playToken;

            try
            {
                AnimationAsset asset = await assetLoader();

                if (token != _playToken)
                {
                    return;
                }

                _timer.Stop();
                _currentMetadata = asset.Metadata;
                _currentFrames = asset.Frames;
                _currentFrame = 0;

                Viewport.Width = asset.Metadata.FrameWidth;
                Viewport.Height = asset.Metadata.FrameHeight;
                ViewportClip.Rect = new Rect(0, 0, asset.Metadata.FrameWidth, asset.Metadata.FrameHeight);

                SpriteImage.Width = asset.Metadata.FrameWidth;
                SpriteImage.Height = asset.Metadata.FrameHeight;

                Visibility = Visibility.Visible;
                _timer.Interval = TimeSpan.FromMilliseconds(1000.0 / asset.Metadata.Fps);
                ShowFrame(0);
                _timer.Start();
            }
            catch
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private async Task<AnimationAsset> LoadPreferredAssetAsync(int spriteNumber, bool isHeadshot)
        {
            if (isHeadshot)
            {
                try
                {
                    return await LoadNamedAssetAsync(HeadshotAssetKey);
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
                    return await LoadNamedAssetAsync(remasteredAssetKey);
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
                HeadshotAssetKey,
                GoldHeadshotAssetKey,
                FirstKillAssetKey,
                KnifeKillAssetKey,
                LastKillAssetKey
            };

            foreach (string assetKey in extraAssets)
            {
                try
                {
                    await LoadNamedAssetAsync(assetKey);
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
                    await LoadNamedAssetAsync(assetKey);
                }
                catch
                {
                }
            }
        }

        private async Task<AnimationAsset> LoadNamedAssetAsync(string assetKey)
        {
            switch (assetKey)
            {
                case HeadshotAssetKey:
                    return await LoadFrameSequenceAssetAsync(HeadshotAssetKey, HeadshotAssetKey, FrameSequenceFps, 0);
                case OneKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(OneKillRemasterAssetKey, OneKillRemasterAssetKey, FrameSequenceFps, 0);
                case TwoKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(TwoKillRemasterAssetKey, TwoKillRemasterAssetKey, FrameSequenceFps, 0);
                case ThreeKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(ThreeKillRemasterAssetKey, ThreeKillRemasterAssetKey, FrameSequenceFps, 0);
                case FourKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(FourKillRemasterAssetKey, FourKillRemasterAssetKey, FrameSequenceFps, 0);
                case FiveKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(FiveKillRemasterAssetKey, FiveKillRemasterAssetKey, FrameSequenceFps, 0);
                case SixKillRemasterAssetKey:
                    return await LoadFrameSequenceAssetAsync(SixKillRemasterAssetKey, SixKillRemasterAssetKey, FrameSequenceFps, 0);
                case FirstKillAssetKey:
                    return await LoadFrameSequenceAssetAsync(FirstKillAssetKey, FirstKillAssetKey, FrameSequenceFps, 0);
                case GoldHeadshotAssetKey:
                    return await LoadFrameSequenceAssetAsync(GoldHeadshotAssetKey, GoldHeadshotAssetKey, FrameSequenceFps, 0);
                case KnifeKillAssetKey:
                    return await LoadFrameSequenceAssetAsync(KnifeKillAssetKey, KnifeKillAssetKey, FrameSequenceFps, 0);
                case LastKillAssetKey:
                    return await LoadFrameSequenceAssetAsync(LastKillAssetKey, LastKillAssetKey, FrameSequenceFps, 0);
                default:
                    throw new FileNotFoundException("Unsupported animation asset: " + assetKey);
            }
        }

        private async Task<AnimationAsset> LoadReferencedFrameSequenceAssetAsync(string cacheKey, string folderName, int? referenceSpriteNumber = null)
        {
            ReferenceAnimationProfile referenceProfile = await LoadReferenceAnimationProfileAsync(referenceSpriteNumber);
            return await LoadFrameSequenceAssetAsync(
                cacheKey,
                folderName,
                referenceProfile.TargetFps,
                referenceProfile.TargetFrameCount);
        }

        private async Task<AnimationAsset> LoadSpriteSheetAssetAsync(string assetName)
        {
            SpriteMetadata metadata = await LoadSpriteSheetMetadataAsync(assetName);
            IReadOnlyList<WriteableBitmap> frames = await LoadSpriteSheetFramesAsync(assetName, metadata);
            return new AnimationAsset(metadata, frames);
        }

        private async Task<SpriteMetadata> LoadSpriteSheetMetadataAsync(string assetName)
        {
            string cacheKey = "sheet:" + assetName;
            if (MetadataCache.TryGetValue(cacheKey, out SpriteMetadata cached))
            {
                return cached;
            }

            var uri = new Uri($"ms-appx:///Assets/KillConfirm/{assetName}.json");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            string jsonText = await FileIO.ReadTextAsync(file);
            JsonObject json = JsonObject.Parse(jsonText);

            var metadata = new SpriteMetadata
            {
                FrameWidth = (int)json.GetNamedNumber("frame_width", 400),
                FrameHeight = (int)json.GetNamedNumber("frame_height", 300),
                Frames = (int)json.GetNamedNumber("frames", 1),
                Cols = (int)json.GetNamedNumber("cols", 1),
                Rows = (int)json.GetNamedNumber("rows", 1),
                Fps = Math.Max(1, (int)json.GetNamedNumber("fps", 30))
            };

            MetadataCache[cacheKey] = metadata;
            return metadata;
        }

        private async Task<IReadOnlyList<WriteableBitmap>> LoadSpriteSheetFramesAsync(string assetName, SpriteMetadata metadata)
        {
            string cacheKey = "sheet:" + assetName;
            if (FrameCache.TryGetValue(cacheKey, out IReadOnlyList<WriteableBitmap> cached))
            {
                return cached;
            }

            var uri = new Uri($"ms-appx:///Assets/KillConfirm/{assetName}.png");
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var frames = new List<WriteableBitmap>(metadata.Frames);

            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                for (int frame = 0; frame < metadata.Frames; frame++)
                {
                    int col = frame % metadata.Cols;
                    int row = frame / metadata.Cols;

                    var transform = new BitmapTransform
                    {
                        Bounds = new BitmapBounds
                        {
                            X = (uint)(col * metadata.FrameWidth),
                            Y = (uint)(row * metadata.FrameHeight),
                            Width = (uint)metadata.FrameWidth,
                            Height = (uint)metadata.FrameHeight
                        }
                    };

                    PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        transform,
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    byte[] data = pixels.DetachPixelData();
                    ApplyColorBoost(data);
                    var bitmap = new WriteableBitmap(metadata.FrameWidth, metadata.FrameHeight);

                    using (Stream pixelStream = bitmap.PixelBuffer.AsStream())
                    {
                        await pixelStream.WriteAsync(data, 0, data.Length);
                    }

                    bitmap.Invalidate();
                    frames.Add(bitmap);
                }
            }

            FrameCache[cacheKey] = frames;
            return frames;
        }

        private async Task<AnimationAsset> LoadFrameSequenceAssetAsync(string cacheKey, string folderName, int fps, int targetFrameCount)
        {
            SpriteMetadata metadata = await LoadFrameSequenceMetadataAsync(cacheKey, folderName, fps, targetFrameCount);
            IReadOnlyList<WriteableBitmap> frames = await LoadFrameSequenceFramesAsync(cacheKey, folderName, metadata, targetFrameCount);
            return new AnimationAsset(metadata, frames);
        }

        private async Task<SpriteMetadata> LoadFrameSequenceMetadataAsync(string cacheKey, string folderName, int fps, int targetFrameCount)
        {
            string resolvedCacheKey = $"{cacheKey}:{fps}:{targetFrameCount}";
            if (MetadataCache.TryGetValue(resolvedCacheKey, out SpriteMetadata cached))
            {
                return cached;
            }

            IReadOnlyList<StorageFile> files = await GetOrderedFrameFilesAsync(folderName);
            IReadOnlyList<StorageFile> selectedFiles = SelectFrameSubset(files, targetFrameCount);
            if (selectedFiles.Count == 0)
            {
                throw new FileNotFoundException("No headshot frames were found.");
            }

            using (IRandomAccessStream stream = await selectedFiles[0].OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                Size targetSize = GetTargetFrameSize(decoder.PixelWidth, decoder.PixelHeight);

                var metadata = new SpriteMetadata
                {
                    FrameWidth = (int)targetSize.Width,
                    FrameHeight = (int)targetSize.Height,
                    Frames = selectedFiles.Count,
                    Cols = selectedFiles.Count,
                    Rows = 1,
                    Fps = Math.Max(1, fps)
                };

                MetadataCache[resolvedCacheKey] = metadata;
                return metadata;
            }
        }

        private async Task<IReadOnlyList<WriteableBitmap>> LoadFrameSequenceFramesAsync(string cacheKey, string folderName, SpriteMetadata metadata, int targetFrameCount)
        {
            string resolvedCacheKey = $"{cacheKey}:{metadata.Fps}:{targetFrameCount}";
            if (FrameCache.TryGetValue(resolvedCacheKey, out IReadOnlyList<WriteableBitmap> cached))
            {
                return cached;
            }

            IReadOnlyList<StorageFile> files = await GetOrderedFrameFilesAsync(folderName);
            IReadOnlyList<StorageFile> selectedFiles = SelectFrameSubset(files, targetFrameCount);
            var frames = new List<WriteableBitmap>(selectedFiles.Count);

            foreach (StorageFile file in selectedFiles)
            {
                using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    var transform = new BitmapTransform();
                    if (decoder.PixelWidth != (uint)metadata.FrameWidth || decoder.PixelHeight != (uint)metadata.FrameHeight)
                    {
                        transform.ScaledWidth = (uint)metadata.FrameWidth;
                        transform.ScaledHeight = (uint)metadata.FrameHeight;
                        transform.InterpolationMode = BitmapInterpolationMode.Linear;
                    }

                    PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        transform,
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    byte[] data = pixels.DetachPixelData();
                    ApplyColorBoost(data);
                    var bitmap = new WriteableBitmap(metadata.FrameWidth, metadata.FrameHeight);

                    using (Stream pixelStream = bitmap.PixelBuffer.AsStream())
                    {
                        await pixelStream.WriteAsync(data, 0, data.Length);
                    }

                    bitmap.Invalidate();
                    frames.Add(bitmap);
                }
            }

            FrameCache[resolvedCacheKey] = frames;
            return frames;
        }

        private async Task<ReferenceAnimationProfile> LoadReferenceAnimationProfileAsync(int? spriteNumber = null)
        {
            if (spriteNumber.HasValue)
            {
                int resolvedSpriteNumber = Math.Max(1, Math.Min(5, spriteNumber.Value));
                SpriteMetadata metadata = await LoadSpriteSheetMetadataAsync(resolvedSpriteNumber.ToString());
                return new ReferenceAnimationProfile(metadata.Fps, metadata.Frames);
            }

            if (_referenceProfile != null)
            {
                return _referenceProfile;
            }

            double totalDuration = 0;
            double totalFps = 0;
            int count = 0;

            for (int spriteIndex = 1; spriteIndex <= 5; spriteIndex++)
            {
                SpriteMetadata metadata = await LoadSpriteSheetMetadataAsync(spriteIndex.ToString());
                totalDuration += (double)metadata.Frames / metadata.Fps;
                totalFps += metadata.Fps;
                count++;
            }

            if (count == 0)
            {
                _referenceProfile = new ReferenceAnimationProfile(30, 34);
                return _referenceProfile;
            }

            int targetFps = Math.Max(1, (int)Math.Round(totalFps / count));
            int targetFrameCount = Math.Max(1, (int)Math.Round((totalDuration / count) * targetFps));

            _referenceProfile = new ReferenceAnimationProfile(targetFps, targetFrameCount);
            return _referenceProfile;
        }

        private static Size GetTargetFrameSize(uint originalWidth, uint originalHeight)
        {
            if (originalWidth == 0 || originalHeight == 0)
            {
                return new Size(MaxCachedFrameWidth, MaxCachedFrameHeight);
            }

            double scale = Math.Min(
                1.0,
                Math.Min(
                    (double)MaxCachedFrameWidth / originalWidth,
                    (double)MaxCachedFrameHeight / originalHeight));

            return new Size(
                Math.Max(1, Math.Round(originalWidth * scale)),
                Math.Max(1, Math.Round(originalHeight * scale)));
        }

        private static async Task<IReadOnlyList<StorageFile>> GetOrderedFrameFilesAsync(string folderName)
        {
            StorageFolder folder = await Package.Current.InstalledLocation.GetFolderAsync(@"Assets\KillConfirm\" + folderName);
            return (await folder.GetFilesAsync())
                .Where(file => string.Equals(file.FileType, ".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<StorageFile> SelectFrameSubset(IReadOnlyList<StorageFile> files, int targetFrameCount)
        {
            if (files.Count <= targetFrameCount || targetFrameCount <= 0)
            {
                return files;
            }

            var selectedFiles = new List<StorageFile>(targetFrameCount);
            int lastIndex = -1;

            for (int frameIndex = 0; frameIndex < targetFrameCount; frameIndex++)
            {
                double ratio = targetFrameCount == 1
                    ? 0
                    : (double)frameIndex / (targetFrameCount - 1);
                int sourceIndex = (int)Math.Round(ratio * (files.Count - 1));

                if (sourceIndex <= lastIndex)
                {
                    sourceIndex = Math.Min(files.Count - 1, lastIndex + 1);
                }

                selectedFiles.Add(files[sourceIndex]);
                lastIndex = sourceIndex;
            }

            return selectedFiles;
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

        private void OnTick(object sender, object e)
        {
            if (_currentMetadata == null || _currentFrames == null)
            {
                _timer.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            _currentFrame++;
            if (_currentFrame >= _currentMetadata.Frames)
            {
                _timer.Stop();
                Visibility = Visibility.Collapsed;
                return;
            }

            ShowFrame(_currentFrame);
        }

        private void ShowFrame(int frame)
        {
            if (_currentFrames == null || frame < 0 || frame >= _currentFrames.Count)
            {
                return;
            }

            SpriteImage.Source = _currentFrames[frame];
        }

        private sealed class SpriteMetadata
        {
            public int FrameWidth { get; set; }
            public int FrameHeight { get; set; }
            public int Frames { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public int Fps { get; set; }
        }

        private sealed class AnimationAsset
        {
            public AnimationAsset(SpriteMetadata metadata, IReadOnlyList<WriteableBitmap> frames)
            {
                Metadata = metadata;
                Frames = frames;
            }

            public SpriteMetadata Metadata { get; }
            public IReadOnlyList<WriteableBitmap> Frames { get; }
        }

        private sealed class ReferenceAnimationProfile
        {
            public ReferenceAnimationProfile(int targetFps, int targetFrameCount)
            {
                TargetFps = targetFps;
                TargetFrameCount = targetFrameCount;
            }

            public int TargetFps { get; }
            public int TargetFrameCount { get; }
        }
    }
}
