using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using TestXboxGameBar.Helpers;

namespace TestXboxGameBar.Services
{
    [DataContract]
    public sealed class PackCatalog
    {
        [DataMember]
        public List<VoicePackItem> VoicePacks { get; set; } = new List<VoicePackItem>();

        [DataMember]
        public List<IconPackItem> IconPacks { get; set; } = new List<IconPackItem>();
    }

    [DataContract]
    public sealed class VoicePackItem
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public string FolderPath { get; set; }
        [DataMember]
        public bool IsBuiltIn { get; set; }
        [DataMember]
        public bool IsVisibleInWidget { get; set; }
        [DataMember]
        public bool OwnsFolder { get; set; }
    }

    [DataContract]
    public sealed class IconPackItem
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string DisplayName { get; set; }
        [DataMember]
        public string FolderPath { get; set; }
        [DataMember]
        public string FolderToken { get; set; }
        [DataMember]
        public bool IsBuiltIn { get; set; }
        [DataMember]
        public bool IsVisibleInWidget { get; set; }
        [DataMember]
        public bool OwnsFolder { get; set; }
        [DataMember]
        public bool HasFxOverlay { get; set; }
        [DataMember]
        public bool HasKillFxOverlay { get; set; }
        [DataMember]
        public bool HasEliteOverlay { get; set; }
        [DataMember]
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class IconPackCapabilities
    {
        public bool HasKillFxOverlay { get; set; }
        public bool HasEliteOverlay { get; set; }
        public bool HasWeaponBadgeOverlay { get; set; }
    }

    public sealed class VoicePackBuildOptions
    {
        public IReadOnlyDictionary<string, StorageFile> SelectedFiles { get; set; }
        public IReadOnlyDictionary<string, bool> CommonOverlayEnabled { get; set; }
        public bool UseBuiltInDefaultCommonOverlay { get; set; }
    }

    public static class PackCatalogService
    {
        private const string CatalogFileName = "pack-catalog.json";
        private const string VisibilityDefaultsVersionKey = "PackCatalogVisibilityDefaultsVersion";
        private const int CurrentVisibilityDefaultsVersion = 2;
        private const string DefaultVoiceKey = "crossfire_swat_gr";
        private const string DefaultIconKey = "default";
        private static PackCatalog _cache;

        public static event EventHandler CatalogChanged;

        public static string GetVoicePackDisplayName(VoicePackItem item)
        {
            if (item == null) return string.Empty;
            if (item.IsBuiltIn)
            {
                return LocalizationManager.Text(item.Key);
            }
            return item.DisplayName;
        }

        public static string GetIconPackDisplayName(IconPackItem item)
        {
            if (item == null) return string.Empty;
            if (item.IsBuiltIn)
            {
                return LocalizationManager.Text(item.Key);
            }
            return item.DisplayName;
        }

        public static bool IsImportedVoicePackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_voice_", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetVisibleVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.Where(p => p.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetVisibleIconPacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks.Where(p => p.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetAllVoicePacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks;
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetAllIconPacksAsync()
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks;
        }

        public static async Task<VoicePackItem> GetVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
        }

        public static async Task<IconPackItem> GetIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            return catalog.IconPacks.FirstOrDefault(p => p.Key == key);
        }

        public static bool IsImportedIconPackKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return key.StartsWith("custom_icon_", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<StorageFolder> GetImportedIconFolderAsync(string key)
        {
            var item = await GetIconPackAsync(key);
            if (item == null || string.IsNullOrEmpty(item.FolderPath)) return null;

            try
            {
                return await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IconPackItem> RefreshImportedIconPackCapabilitiesAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.IsBuiltIn || string.IsNullOrWhiteSpace(item.FolderPath))
            {
                return item;
            }

            StorageFolder folder;
            try
            {
                folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
            }
            catch
            {
                return item;
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            bool changed = item.HasFxOverlay != capabilities.HasKillFxOverlay
                || item.HasKillFxOverlay != capabilities.HasKillFxOverlay
                || item.HasEliteOverlay != capabilities.HasEliteOverlay
                || item.HasWeaponBadgeOverlay != capabilities.HasWeaponBadgeOverlay;

            item.HasFxOverlay = capabilities.HasKillFxOverlay;
            item.HasKillFxOverlay = capabilities.HasKillFxOverlay;
            item.HasEliteOverlay = capabilities.HasEliteOverlay;
            item.HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay;

            if (changed)
            {
                await SaveAsync(catalog);
            }

            return item;
        }

        public static async Task ImportVoicePackAsync(StorageFolder folder)
        {
            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });
            await SaveAsync(catalog);
        }

        public static async Task ImportIconPackAsync(StorageFolder folder)
        {
            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(folder);
            var catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        public static async Task<IconPackCapabilities> DetectIconPackCapabilitiesAsync(StorageFolder folder)
        {
            return new IconPackCapabilities
            {
                HasKillFxOverlay = await ContainsAnyFileAsync(folder,
                    "multi2_fx.png", "multi2_fx.tga",
                    "multi3_fx.png", "multi3_fx.tga",
                    "multi4_fx.png", "multi4_fx.tga",
                    "multi5_fx.png", "multi5_fx.tga",
                    "multi6_fx.png", "multi6_fx.tga"),
                HasEliteOverlay = await ContainsAnyFileAsync(folder,
                    "KillMark_Upgrade1.png", "KillMark_Upgrade1.tga",
                    "KillMark_Upgrade2.png", "KillMark_Upgrade2.tga",
                    "KillMark_Upgrade3.png", "KillMark_Upgrade3.tga",
                    "badge_knife_1.png", "badge_knife_1.tga",
                    "badge_knife_2.png", "badge_knife_2.tga",
                    "badge_knife_3.png", "badge_knife_3.tga"),
                HasWeaponBadgeOverlay = await ContainsAnyFileAsync(folder,
                    "badge_assault1.png", "badge_assault1.tga",
                    "badge_assault2.png", "badge_assault2.tga",
                    "badge_assault3.png", "badge_assault3.tga",
                    "badge_scout1.png", "badge_scout1.tga",
                    "badge_scout2.png", "badge_scout2.tga",
                    "badge_scout3.png", "badge_scout3.tga",
                    "badge_sniper1.png", "badge_sniper1.tga",
                    "badge_sniper2.png", "badge_sniper2.tga",
                    "badge_sniper3.png", "badge_sniper3.tga",
                    "badge_elite1.png", "badge_elite1.tga",
                    "badge_elite2.png", "badge_elite2.tga",
                    "badge_elite3.png", "badge_elite3.tga",
                    "badge_knife1.png", "badge_knife1.tga",
                    "badge_knife2.png", "badge_knife2.tga",
                    "badge_knife3.png", "badge_knife3.tga")
            };
        }

        private static async Task<bool> ContainsAnyFileAsync(StorageFolder folder, params string[] fileNames)
        {
            foreach (string name in fileNames)
            {
                try
                {
                    await folder.GetFileAsync(name);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        public static async Task SetVoicePackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
                await SaveAsync(catalog);
            }
        }

        public static async Task SetIconPackVisibilityAsync(string key, bool isVisible)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null)
            {
                item.IsVisibleInWidget = isVisible;
                await SaveAsync(catalog);
            }
        }

        public static async Task RemoveCustomVoicePackAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.VoicePacks.FirstOrDefault(p => p.Key == key);
            if (item != null && !item.IsBuiltIn)
            {
                catalog.VoicePacks.Remove(item);
                await SaveAsync(catalog);
                if (item.OwnsFolder)
                {
                    try
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                        await folder.DeleteAsync();
                    }
                    catch { }
                }
            }
        }

        public static async Task RemoveCustomIconPackAsync(string key)
        {
            var catalog = await LoadAsync();
            var item = catalog.IconPacks.FirstOrDefault(p => p.Key == key);
            if (item != null && !item.IsBuiltIn)
            {
                catalog.IconPacks.Remove(item);
                await SaveAsync(catalog);
                if (item.OwnsFolder)
                {
                    try
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                        await folder.DeleteAsync();
                    }
                    catch { }
                }
            }
        }

        public static async Task CreateVoicePackAsync(string displayName, VoicePackBuildOptions options)
        {
            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedVoicePacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in options.SelectedFiles)
            {
                if (pair.Value != null)
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            // Simple common overlay logic for now
            if (options.UseBuiltInDefaultCommonOverlay)
            {
                // Logic to copy built-in common overlay if needed
            }

            var catalog = await LoadAsync();
            catalog.VoicePacks.Add(new VoicePackItem
            {
                Key = "custom_voice_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
            });
            await SaveAsync(catalog);
        }

        public static async Task CreateIconPackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedIconPacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Value.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    await TgaDecoder.ConvertTgaToPngAsync(pair.Value, packFolder, pair.Key);
                }
                else
                {
                    await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
                }
            }

            IconPackCapabilities capabilities = await DetectIconPackCapabilitiesAsync(packFolder);

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true,
                HasFxOverlay = capabilities.HasKillFxOverlay,
                HasKillFxOverlay = capabilities.HasKillFxOverlay,
                HasEliteOverlay = capabilities.HasEliteOverlay,
                HasWeaponBadgeOverlay = capabilities.HasWeaponBadgeOverlay
            });
            await SaveAsync(catalog);
        }

        private static async Task<PackCatalog> LoadAsync()
        {
            if (_cache != null)
            {
                return _cache;
            }

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            try
            {
                StorageFile file = await localFolder.GetFileAsync(CatalogFileName);
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                    _cache = (PackCatalog)serializer.ReadObject(stream);
                }
            }
            catch
            {
                _cache = CreateDefaultCatalog();
                await SaveAsync(_cache);
            }

            MergeMissingBuiltIns(_cache);
            ApplyBuiltInVisibilityDefaultsIfNeeded(_cache);
            EnsureAtLeastOneVisibleVoice(_cache);
            EnsureAtLeastOneVisibleIcon(_cache);
            return _cache;
        }

        private static async Task SaveAsync(PackCatalog catalog)
        {
            _cache = catalog;
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile file = await localFolder.CreateFileAsync(CatalogFileName, CreationCollisionOption.ReplaceExisting);
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                    serializer.WriteObject(stream, catalog);
                }

                CatalogChanged?.Invoke(null, EventArgs.Empty);
            }
            catch { }
        }

        private static PackCatalog CreateDefaultCatalog()
        {
            return new PackCatalog
            {
                VoicePacks = new List<VoicePackItem>
                {
                    CreateBuiltInVoice("crossfire_swat_gr", "swat GR", true),
                    CreateBuiltInVoice("crossfire_swat_bl", "swat BL", true),
                    CreateBuiltInVoice("crossfire_flying_tiger_gr", "tiger GR", true),
                    CreateBuiltInVoice("crossfire_flying_tiger_bl", "tiger BL", true),
                    CreateBuiltInVoice("crossfire_v_sex", "cfsex", true),
                    CreateBuiltInVoice("crossfire_women_gr", "women GR", true),
                    CreateBuiltInVoice("crossfire_women_bl", "women BL", true)
                },
                IconPacks = new List<IconPackItem>
                {
                    CreateBuiltInIcon("default", "原版", true),
                    CreateBuiltInIcon("vip", "VIP", true),
                    CreateBuiltInIcon("legacy", "老版", false),
                    CreateBuiltInIcon("angelic_beast", "示例", false)
                }
            };
        }

        private static VoicePackItem CreateBuiltInVoice(string key, string name, bool visible)
        {
            return new VoicePackItem
            {
                Key = key,
                DisplayName = name,
                IsBuiltIn = true,
                IsVisibleInWidget = visible
            };
        }

        private static IconPackItem CreateBuiltInIcon(string key, string name, bool visible)
        {
            return new IconPackItem
            {
                Key = key,
                DisplayName = name,
                IsBuiltIn = true,
                IsVisibleInWidget = visible
            };
        }

        private static void MergeMissingBuiltIns(PackCatalog catalog)
        {
            if (catalog.VoicePacks == null)
            {
                catalog.VoicePacks = new List<VoicePackItem>();
            }
            if (catalog.IconPacks == null)
            {
                catalog.IconPacks = new List<IconPackItem>();
            }

            foreach (VoicePackItem item in CreateDefaultCatalog().VoicePacks)
            {
                if (!catalog.VoicePacks.Any(entry => string.Equals(entry.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.VoicePacks.Add(item);
                }
            }

            foreach (IconPackItem item in CreateDefaultCatalog().IconPacks)
            {
                if (!catalog.IconPacks.Any(entry => string.Equals(entry.Key, item.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    catalog.IconPacks.Add(item);
                }
            }
        }

        private static void ApplyBuiltInVisibilityDefaultsIfNeeded(PackCatalog catalog)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            object rawVersion = localSettings.Values[VisibilityDefaultsVersionKey];
            if (rawVersion is int version && version >= CurrentVisibilityDefaultsVersion)
            {
                return;
            }

            foreach (VoicePackItem item in catalog.VoicePacks)
            {
                if (item.IsBuiltIn)
                {
                    item.IsVisibleInWidget = true;
                }
            }

            foreach (IconPackItem item in catalog.IconPacks)
            {
                if (!item.IsBuiltIn)
                {
                    continue;
                }

                item.IsVisibleInWidget = string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Key, "vip", StringComparison.OrdinalIgnoreCase);
            }

            localSettings.Values[VisibilityDefaultsVersionKey] = CurrentVisibilityDefaultsVersion;
        }

        private static void EnsureAtLeastOneVisibleVoice(PackCatalog catalog)
        {
            if (catalog.VoicePacks.Any(item => item.IsVisibleInWidget))
            {
                return;
            }

            VoicePackItem fallbackVoice = catalog.VoicePacks.FirstOrDefault(entry => string.Equals(entry.Key, DefaultVoiceKey, StringComparison.OrdinalIgnoreCase))
                ?? catalog.VoicePacks.FirstOrDefault();
            if (fallbackVoice != null)
            {
                fallbackVoice.IsVisibleInWidget = true;
            }
        }

        private static void EnsureAtLeastOneVisibleIcon(PackCatalog catalog)
        {
            if (catalog.IconPacks.Any(item => item.IsVisibleInWidget))
            {
                return;
            }

            IconPackItem fallbackIcon = catalog.IconPacks.FirstOrDefault(entry => string.Equals(entry.Key, DefaultIconKey, StringComparison.OrdinalIgnoreCase))
                ?? catalog.IconPacks.FirstOrDefault();
            if (fallbackIcon != null)
            {
                fallbackIcon.IsVisibleInWidget = true;
            }
        }

        private static async Task<StorageFolder> GetOrCreatePackRootAsync(string folderName)
        {
            return await ApplicationData.Current.LocalFolder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
        }

        private static string SanitizeName(string displayName)
        {
            string value = string.IsNullOrWhiteSpace(displayName) ? "NewPack" : displayName.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(ch, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "NewPack" : value;
        }
    }
}
