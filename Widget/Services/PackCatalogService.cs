using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

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
        public bool IsVisibleInWidget { get; set; }

        [DataMember]
        public bool IsBuiltIn { get; set; }

        [DataMember]
        public string FolderPath { get; set; }

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
        public bool IsVisibleInWidget { get; set; }

        [DataMember]
        public bool IsBuiltIn { get; set; }

        [DataMember]
        public string FolderToken { get; set; }

        [DataMember]
        public string FolderPath { get; set; }

        [DataMember]
        public bool OwnsFolder { get; set; }
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

        public static async Task<IReadOnlyList<VoicePackItem>> GetVisibleVoicePacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.VoicePacks.Where(item => item.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetVisibleIconPacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.IconPacks.Where(item => item.IsVisibleInWidget).ToList();
        }

        public static async Task<IReadOnlyList<VoicePackItem>> GetAllVoicePacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.VoicePacks.ToList();
        }

        public static async Task<IReadOnlyList<IconPackItem>> GetAllIconPacksAsync()
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.IconPacks.ToList();
        }

        public static async Task<VoicePackItem> GetVoicePackAsync(string key)
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.VoicePacks.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<IconPackItem> GetIconPackAsync(string key)
        {
            PackCatalog catalog = await LoadAsync();
            return catalog.IconPacks.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<StorageFolder> GetImportedIconFolderAsync(string key)
        {
            IconPackItem item = await GetIconPackAsync(key);
            if (item == null || item.IsBuiltIn)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(item.FolderToken))
            {
                try
                {
                    return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(item.FolderToken);
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(item.FolderPath))
            {
                try
                {
                    return await StorageFolder.GetFolderFromPathAsync(item.FolderPath);
                }
                catch
                {
                }
            }

            return null;
        }

        public static bool IsImportedIconPackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.StartsWith("custom_icon_", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsImportedVoicePackKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.StartsWith("custom_voice_", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetVoicePackDisplayName(VoicePackItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!item.IsBuiltIn)
            {
                return item.DisplayName ?? item.Key ?? string.Empty;
            }

            switch ((item.Key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "crossfire_swat_gr":
                    return LocalizationManager.Text("CrossfireSwatGr");
                case "crossfire_swat_bl":
                    return LocalizationManager.Text("CrossfireSwatBl");
                case "crossfire_flying_tiger_gr":
                    return LocalizationManager.Text("CrossfireFlyingTigerGr");
                case "crossfire_flying_tiger_bl":
                    return LocalizationManager.Text("CrossfireFlyingTigerBl");
                case "crossfire_women_gr":
                    return LocalizationManager.Text("CrossfireWomenGr");
                case "crossfire_women_bl":
                    return LocalizationManager.Text("CrossfireWomenBl");
                default:
                    return item.DisplayName ?? item.Key ?? string.Empty;
            }
        }

        public static async Task ImportVoicePackAsync(StorageFolder folder)
        {
            if (folder == null)
            {
                return;
            }

            PackCatalog catalog = await LoadAsync();
            if (catalog.VoicePacks.Any(item => !item.IsBuiltIn
                && string.Equals(item.FolderPath, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

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
            if (folder == null)
            {
                return;
            }

            PackCatalog catalog = await LoadAsync();
            if (catalog.IconPacks.Any(item => !item.IsBuiltIn
                && string.Equals(item.FolderPath, folder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string token = "ImportedIconPack_" + Guid.NewGuid().ToString("N");
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, folder);

            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = folder.DisplayName,
                FolderToken = token,
                FolderPath = folder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = false
            });

            await SaveAsync(catalog);
        }

        public static async Task SetVoicePackVisibilityAsync(string key, bool isVisible)
        {
            PackCatalog catalog = await LoadAsync();
            VoicePackItem item = catalog.VoicePacks.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                return;
            }

            item.IsVisibleInWidget = isVisible;
            EnsureAtLeastOneVisibleVoice(catalog);
            await SaveAsync(catalog);
        }

        public static async Task SetIconPackVisibilityAsync(string key, bool isVisible)
        {
            PackCatalog catalog = await LoadAsync();
            IconPackItem item = catalog.IconPacks.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                return;
            }

            item.IsVisibleInWidget = isVisible;
            EnsureAtLeastOneVisibleIcon(catalog);
            await SaveAsync(catalog);
        }

        public static async Task RemoveCustomVoicePackAsync(string key)
        {
            PackCatalog catalog = await LoadAsync();
            VoicePackItem item = catalog.VoicePacks.FirstOrDefault(entry => !entry.IsBuiltIn && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item != null && item.OwnsFolder && !string.IsNullOrWhiteSpace(item.FolderPath))
            {
                await TryDeleteOwnedFolderAsync(item.FolderPath);
            }

            catalog.VoicePacks.RemoveAll(entry => !entry.IsBuiltIn && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            EnsureAtLeastOneVisibleVoice(catalog);
            await SaveAsync(catalog);
        }

        public static async Task RemoveCustomIconPackAsync(string key)
        {
            PackCatalog catalog = await LoadAsync();
            IconPackItem item = catalog.IconPacks.FirstOrDefault(entry => !entry.IsBuiltIn
                && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            if (item != null && !string.IsNullOrWhiteSpace(item.FolderToken))
            {
                try
                {
                    StorageApplicationPermissions.FutureAccessList.Remove(item.FolderToken);
                }
                catch
                {
                }
            }

            if (item != null && item.OwnsFolder && !string.IsNullOrWhiteSpace(item.FolderPath))
            {
                await TryDeleteOwnedFolderAsync(item.FolderPath);
            }

            catalog.IconPacks.RemoveAll(entry => !entry.IsBuiltIn && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
            EnsureAtLeastOneVisibleIcon(catalog);
            await SaveAsync(catalog);
        }

        public static async Task CreateVoicePackAsync(string displayName, IReadOnlyDictionary<string, StorageFile> selectedFiles)
        {
            if (selectedFiles == null || selectedFiles.Count == 0)
            {
                return;
            }

            StorageFolder root = await GetOrCreatePackRootAsync("GeneratedVoicePacks");
            StorageFolder packFolder = await root.CreateFolderAsync(
                SanitizeName(displayName),
                CreationCollisionOption.GenerateUniqueName);

            foreach (var pair in selectedFiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
            }

            await FileIO.WriteTextAsync(
                await packFolder.CreateFileAsync("sound.lua", CreationCollisionOption.ReplaceExisting),
                BuildGeneratedVoiceLua(selectedFiles.Keys));

            PackCatalog catalog = await LoadAsync();
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

                await pair.Value.CopyAsync(packFolder, pair.Key, NameCollisionOption.ReplaceExisting);
            }

            PackCatalog catalog = await LoadAsync();
            catalog.IconPacks.Add(new IconPackItem
            {
                Key = "custom_icon_" + Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FolderPath = packFolder.Path,
                IsBuiltIn = false,
                IsVisibleInWidget = true,
                OwnsFolder = true
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
                string json = await FileIO.ReadTextAsync(file);
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                    _cache = (PackCatalog)serializer.ReadObject(stream);
                }
            }
            catch
            {
                _cache = CreateDefaultCatalog();
                await SaveAsync(_cache, raiseChanged: false);
            }

            MergeMissingBuiltIns(_cache);
            ApplyBuiltInVisibilityDefaultsIfNeeded(_cache);
            EnsureAtLeastOneVisibleVoice(_cache);
            EnsureAtLeastOneVisibleIcon(_cache);
            return _cache;
        }

        private static async Task SaveAsync(PackCatalog catalog, bool raiseChanged = true)
        {
            _cache = catalog;
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(PackCatalog));
                serializer.WriteObject(stream, catalog);
                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                        CatalogFileName,
                        CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(file, json);
                }
            }

            if (raiseChanged)
            {
                CatalogChanged?.Invoke(null, EventArgs.Empty);
            }
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

        private static async Task TryDeleteOwnedFolderAsync(string folderPath)
        {
            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch
            {
            }
        }

        private static string BuildGeneratedVoiceLua(IEnumerable<string> selectedKeys)
        {
            string availabilityEntries = string.Join(
                ",\n    ",
                selectedKeys.Select(key => "[\"" + Path.GetFileNameWithoutExtension(key) + "\"] = true"));

            return
                "function get_sounds(ctx)\n" +
                "    local sounds = {}\n" +
                "    local base = ctx.base_dir .. \"/\"\n" +
                "    local available = {\n    " + availabilityEntries + "\n    }\n\n" +
                "    local function add_if_present(name)\n" +
                "        if available[name] then\n" +
                "            table.insert(sounds, base .. name .. \".wav\")\n" +
                "        end\n" +
                "    end\n\n" +
                "    if ctx.is_first_kill or ctx.is_last_kill then\n" +
                "        add_if_present(\"firstandlast\")\n" +
                "    end\n\n" +
                "    if ctx.play_main_audio and ctx.kill_count >= 2 then\n" +
                "        local voiced_kill_count = math.min(ctx.kill_count, 8)\n" +
                "        add_if_present(tostring(voiced_kill_count))\n" +
                "    elseif ctx.is_knife_kill then\n" +
                "        add_if_present(\"knife\")\n" +
                "    elseif ctx.is_headshot then\n" +
                "        add_if_present(\"headshot\")\n" +
                "    elseif ctx.play_main_audio and ctx.kill_count == 1 then\n" +
                "        add_if_present(\"common\")\n" +
                "    end\n\n" +
                "    return sounds\n" +
                "end\n";
        }

        private static void MergeMissingBuiltIns(PackCatalog catalog)
        {
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
    }
}
