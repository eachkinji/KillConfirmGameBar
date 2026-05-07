using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestXboxGameBar.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace TestXboxGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();

        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            PackCatalogService.CatalogChanged += OnCatalogChanged;
            await ReloadPackListsAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PackCatalogService.CatalogChanged -= OnCatalogChanged;
            _previewPlayer.Pause();
        }

        private async void OnCatalogChanged(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await ReloadPackListsAsync();
            });
        }

        private async Task ReloadPackListsAsync()
        {
            await RebuildVoicePackListAsync();
            await RebuildIconPackListAsync();
            ApplyLanguage();
        }

        private async Task RebuildVoicePackListAsync()
        {
            var items = await PackCatalogService.GetAllVoicePacksAsync();
            VoiceVisibleCountText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? $"Game Bar 里显示 {CountVisible(items)} 个语音包"
                : $"{CountVisible(items)} voice packs visible in Game Bar";

            VoicePackListPanel.Children.Clear();
            foreach (VoicePackItem item in items)
            {
                VoicePackListPanel.Children.Add(BuildVoicePackRow(item));
            }
        }

        private async Task RebuildIconPackListAsync()
        {
            var items = await PackCatalogService.GetAllIconPacksAsync();
            IconVisibleCountText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? $"Game Bar 里显示 {CountVisible(items)} 个图标包"
                : $"{CountVisible(items)} icon packs visible in Game Bar";

            IconPackListPanel.Children.Clear();
            foreach (IconPackItem item in items)
            {
                IconPackListPanel.Children.Add(BuildIconPackRow(item));
            }
        }

        private static int CountVisible<T>(IEnumerable<T> items)
        {
            int count = 0;
            foreach (T item in items)
            {
                switch (item)
                {
                    case VoicePackItem voice when voice.IsVisibleInWidget:
                        count++;
                        break;
                    case IconPackItem icon when icon.IsVisibleInWidget:
                        count++;
                        break;
                }
            }

            return count;
        }

        private UIElement BuildVoicePackRow(VoicePackItem item)
        {
            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, false);

            var title = new TextBlock
            {
                Text = PackCatalogService.GetVoicePackDisplayName(item),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255)),
                FontSize = 14
            };

            var meta = new TextBlock
            {
                Text = item.IsBuiltIn
                    ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "内置" : "Built-in")
                    : (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "自定义" : "Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                FontSize = 12
            };

            var deleteButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "删除" : "Delete",
                Padding = new Thickness(10, 4, 10, 4),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomVoicePackAsync(item.Key);

            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(meta);

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(checkBox);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            Grid.SetColumn(deleteButton, 2);
            row.Children.Add(deleteButton);

            return new Border
            {
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromArgb(255, 23, 36, 50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 55, 80, 102)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = row
            };
        }

        private UIElement BuildIconPackRow(IconPackItem item)
        {
            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, false);

            var title = new TextBlock
            {
                Text = item.DisplayName,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255)),
                FontSize = 14
            };

            var meta = new TextBlock
            {
                Text = item.IsBuiltIn
                    ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "内置" : "Built-in")
                    : (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "自定义" : "Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                FontSize = 12
            };

            var deleteButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "删除" : "Delete",
                Padding = new Thickness(10, 4, 10, 4),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomIconPackAsync(item.Key);

            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(meta);

            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(checkBox);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            Grid.SetColumn(deleteButton, 2);
            row.Children.Add(deleteButton);

            return new Border
            {
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromArgb(255, 23, 36, 50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 55, 80, 102)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = row
            };
        }

        private async void OnImportVoicePackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateVoicePackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(
                    folder,
                    "common.wav",
                    "2.wav",
                    "3.wav",
                    "4.wav",
                    "5.wav",
                    "6.wav",
                    "7.wav",
                    "8.wav",
                    "headshot.wav",
                    "knife.wav",
                    "firstandlast.wav"),
                await TryGetFileAsync(folder, "common_overlay.wav"));
        }

        private async void OnImportIconPackClick(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return;
            }

            await ShowCreateIconPackDialogAsync(
                folder.DisplayName,
                await CollectRecognizedFilesAsync(
                    folder,
                    "badge_multi1.png",
                    "badge_multi2.png",
                    "badge_multi3.png",
                    "badge_multi4.png",
                    "badge_multi5.png",
                    "badge_multi6.png",
                    "badge_headshot.png",
                    "badge_headshot_gold.png",
                    "badge_knife.png",
                    "FIRSTKILL.png",
                    "LASTKILL.png"));
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateVoicePackDialogAsync();
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateIconPackDialogAsync();
        }

        private async Task ShowCreateVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialCommonOverlayFile = null)
        {
            var slots = new[]
            {
                ("common.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "一杀音效" : "Single kill"),
                ("2.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "双杀" : "Double kill"),
                ("3.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "三杀" : "Triple kill"),
                ("4.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "四杀" : "4 kills"),
                ("5.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "五杀" : "5 kills"),
                ("6.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "六杀" : "6 kills"),
                ("7.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "七杀" : "7 kills"),
                ("8.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "八杀" : "8 kills"),
                ("headshot.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "爆头" : "Headshot"),
                ("knife.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "刀杀" : "Knife kill"),
                ("firstandlast.wav", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "首杀/尾杀" : "First / Last kill")
            };

            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            var overlayEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var overlayCheckBoxes = new List<CheckBox>();
            StorageFile customCommonOverlayFile = initialCommonOverlayFile;
            bool useBuiltInCommonOverlay = initialCommonOverlayFile == null;

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "输入语音包名称" : "Voice pack name",
                Text = initialDisplayName ?? string.Empty
            };

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? "一杀音效和通用击杀音效分开设置。每个项目都可以决定是否叠加通用击杀音效。"
                    : "Single-kill and shared kill sounds are configured separately. Each slot can decide whether to layer the shared kill sound.",
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var commonOverlayCard = new StackPanel { Spacing = 6 };
            commonOverlayCard.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "通用击杀音效" : "Shared kill sound",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255))
            });

            var commonOverlayMode = new ComboBox { MinWidth = 180 };
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "使用默认 common" : "Use built-in common",
                Tag = "builtin"
            });
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择自己的音频" : "Choose custom audio",
                Tag = "custom"
            });
            commonOverlayMode.SelectedIndex = useBuiltInCommonOverlay ? 0 : 1;
            commonOverlayCard.Children.Add(commonOverlayMode);

            var commonOverlayRow = new Grid { ColumnSpacing = 8 };
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            commonOverlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var commonOverlayFileText = new TextBlock
            {
                Text = customCommonOverlayFile?.Name
                    ?? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "默认 common" : "Built-in common"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center
            };
            commonOverlayRow.Children.Add(commonOverlayFileText);

            var commonOverlayPreviewButton = new Button
            {
                Content = "\uE768",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Padding = new Thickness(10, 4, 10, 4)
            };
            commonOverlayPreviewButton.Click += async (_, __) =>
            {
                StorageFile previewFile = customCommonOverlayFile ?? await GetBuiltInCommonOverlayFileAsync();
                if (previewFile != null)
                {
                    await PlayPreviewAsync(previewFile);
                }
            };
            Grid.SetColumn(commonOverlayPreviewButton, 1);
            commonOverlayRow.Children.Add(commonOverlayPreviewButton);

            var commonOverlayBrowseButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择文件" : "Choose File",
                Padding = new Thickness(10, 4, 10, 4),
                IsEnabled = !useBuiltInCommonOverlay
            };
            commonOverlayBrowseButton.Click += async (_, __) =>
            {
                StorageFile file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".m4a" });
                if (file == null)
                {
                    return;
                }

                customCommonOverlayFile = file;
                useBuiltInCommonOverlay = false;
                commonOverlayMode.SelectedIndex = 1;
                commonOverlayFileText.Text = file.Name;
            };
            Grid.SetColumn(commonOverlayBrowseButton, 2);
            commonOverlayRow.Children.Add(commonOverlayBrowseButton);
            commonOverlayCard.Children.Add(commonOverlayRow);

            commonOverlayMode.SelectionChanged += (_, __) =>
            {
                string mode = (commonOverlayMode.SelectedItem as ComboBoxItem)?.Tag as string;
                bool isCustom = string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase);
                commonOverlayBrowseButton.IsEnabled = isCustom;
                useBuiltInCommonOverlay = !isCustom;
                if (isCustom)
                {
                    commonOverlayFileText.Text = customCommonOverlayFile?.Name
                        ?? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "未选择" : "Not selected");
                }
                else
                {
                    commonOverlayFileText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "默认 common" : "Built-in common";
                }
            };

            layout.Children.Add(commonOverlayCard);

            var overlayToggleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            var overlayOnButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "通用叠加全开" : "Enable all overlay",
                Padding = new Thickness(10, 4, 10, 4)
            };
            overlayOnButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = true;
                }
            };
            var overlayOffButton = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "通用叠加全关" : "Disable all overlay",
                Padding = new Thickness(10, 4, 10, 4)
            };
            overlayOffButton.Click += (_, __) =>
            {
                foreach (CheckBox checkBox in overlayCheckBoxes)
                {
                    checkBox.IsChecked = false;
                }
            };
            overlayToggleRow.Children.Add(overlayOnButton);
            overlayToggleRow.Children.Add(overlayOffButton);
            layout.Children.Add(overlayToggleRow);

            var scroll = new ScrollViewer { MaxHeight = 460 };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = slotPanel;

            foreach (var slot in slots)
            {
                overlayEnabled[slot.Item1] = true;
                selectedFiles.TryGetValue(slot.Item1, out StorageFile existingFile);

                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Item2,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255))
                });

                var fileText = new TextBlock
                {
                    Text = existingFile?.Name ?? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "未选择" : "Not selected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                    TextWrapping = TextWrapping.WrapWholeWords,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(fileText, 1);
                row.Children.Add(fileText);

                var overlayCheckBox = new CheckBox
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "叠加通用" : "Layer common",
                    IsChecked = true,
                    VerticalAlignment = VerticalAlignment.Center
                };
                overlayCheckBox.Checked += (_, __) => overlayEnabled[slot.Item1] = true;
                overlayCheckBox.Unchecked += (_, __) => overlayEnabled[slot.Item1] = false;
                overlayCheckBoxes.Add(overlayCheckBox);
                Grid.SetColumn(overlayCheckBox, 2);
                row.Children.Add(overlayCheckBox);

                var previewButton = new Button
                {
                    Content = "\uE768",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Padding = new Thickness(10, 4, 10, 4)
                };
                previewButton.Click += async (_, __) =>
                {
                    if (selectedFiles.TryGetValue(slot.Item1, out StorageFile previewFile) && previewFile != null)
                    {
                        await PlayPreviewAsync(previewFile);
                    }
                };
                Grid.SetColumn(previewButton, 3);
                row.Children.Add(previewButton);

                var browseButton = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择文件" : "Choose File",
                    Padding = new Thickness(10, 4, 10, 4)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(new[] { ".wav", ".mp3", ".m4a" });
                    if (file == null)
                    {
                        return;
                    }

                    selectedFiles[slot.Item1] = file;
                    fileText.Text = file.Name;
                };
                Grid.SetColumn(browseButton, 4);
                row.Children.Add(browseButton);

                slotPanel.Children.Add(row);
            }

            layout.Children.Add(scroll);

            var dialog = new ContentDialog
            {
                Title = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "图形化新建语音包" : "Create Voice Pack",
                Content = layout,
                PrimaryButtonText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "创建" : "Create",
                CloseButtonText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "取消" : "Cancel"
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "新建语音包" : "New Voice Pack")
                : nameBox.Text.Trim();

            await PackCatalogService.CreateVoicePackAsync(
                displayName,
                new VoicePackBuildOptions
                {
                    SelectedFiles = selectedFiles,
                    CommonOverlayEnabled = overlayEnabled,
                    UseBuiltInDefaultCommonOverlay = useBuiltInCommonOverlay
                });
        }

        private async Task ShowCreateIconPackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var slots = new[]
            {
                ("badge_multi1.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "单杀" : "Single kill"),
                ("badge_multi2.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "双杀" : "Double kill"),
                ("badge_multi3.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "三杀" : "Triple kill"),
                ("badge_multi4.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "四杀" : "4 kills"),
                ("badge_multi5.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "五杀" : "5 kills"),
                ("badge_multi6.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "六杀" : "6 kills"),
                ("badge_headshot.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "爆头" : "Headshot"),
                ("badge_headshot_gold.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "首尾爆头" : "Gold headshot"),
                ("badge_knife.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "刀杀" : "Knife kill"),
                ("FIRSTKILL.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "首杀牌" : "First kill plate"),
                ("LASTKILL.png", LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "尾杀牌" : "Last kill plate")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "图形化新建图标包" : "Create Icon Pack",
                LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? "把 PNG 图标逐个填到这些槽位里。留空也能创建，缺图会自动回退原版。"
                    : "Fill these PNG slots one by one. Empty slots are allowed and will fall back to Original.",
                slots,
                new[] { ".png" },
                PackCatalogService.CreateIconPackAsync,
                initialDisplayName,
                initialFiles);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            string[] fileFilters,
            Func<string, IReadOnlyDictionary<string, StorageFile>, Task> createHandler,
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null)
        {
            var selectedFiles = initialFiles != null
                ? new Dictionary<string, StorageFile>(initialFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            bool supportsImagePreview = Array.Exists(fileFilters, filter => string.Equals(filter, ".png", StringComparison.OrdinalIgnoreCase));

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "输入包名称" : "Pack name",
                Text = initialDisplayName ?? string.Empty
            };

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var scroll = new ScrollViewer { MaxHeight = 420 };
            var slotPanel = new StackPanel { Spacing = 8 };
            scroll.Content = slotPanel;

            foreach (var slot in slots)
            {
                selectedFiles.TryGetValue(slot.FileName, out StorageFile existingFile);
                var fileNameText = new TextBlock
                {
                    Text = existingFile?.Name ?? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "未选择" : "Not selected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                    TextWrapping = TextWrapping.WrapWholeWords
                };

                Image previewImage = null;
                if (supportsImagePreview)
                {
                    previewImage = new Image
                    {
                        Width = 58,
                        Height = 58,
                        Stretch = Stretch.Uniform,
                        Visibility = existingFile != null ? Visibility.Visible : Visibility.Collapsed
                    };

                    if (existingFile != null)
                    {
                        await SetPreviewImageAsync(previewImage, existingFile);
                    }
                }

                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (supportsImagePreview)
                {
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                }
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                row.Children.Add(new TextBlock
                {
                    Text = slot.Label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255))
                });

                Grid.SetColumn(fileNameText, 1);
                row.Children.Add(fileNameText);

                if (previewImage != null)
                {
                    Grid.SetColumn(previewImage, 2);
                    row.Children.Add(previewImage);
                }

                var browseButton = new Button
                {
                    Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "选择文件" : "Choose File",
                    Padding = new Thickness(10, 4, 10, 4)
                };
                browseButton.Click += async (_, __) =>
                {
                    StorageFile file = await PickSingleFileAsync(fileFilters);
                    if (file == null)
                    {
                        return;
                    }

                    selectedFiles[slot.FileName] = file;
                    fileNameText.Text = file.Name;
                    if (previewImage != null)
                    {
                        await SetPreviewImageAsync(previewImage, file);
                    }
                };
                Grid.SetColumn(browseButton, supportsImagePreview ? 3 : 2);
                row.Children.Add(browseButton);

                slotPanel.Children.Add(row);
            }

            layout.Children.Add(scroll);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = layout,
                PrimaryButtonText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "创建" : "Create",
                CloseButtonText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "取消" : "Cancel"
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || selectedFiles.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "新建集合" : "New Pack")
                : nameBox.Text.Trim();
            await createHandler(displayName, selectedFiles);
        }

        private static async Task SetPreviewImageAsync(Image image, StorageFile file)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var stream = await file.OpenReadAsync())
                {
                    await bitmap.SetSourceAsync(stream);
                }

                image.Source = bitmap;
                image.Visibility = Visibility.Visible;
            }
            catch
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<StorageFile> PickSingleFileAsync(string[] fileFilters)
        {
            var picker = new FileOpenPicker();
            foreach (string filter in fileFilters)
            {
                picker.FileTypeFilter.Add(filter);
            }

            return await picker.PickSingleFileAsync();
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesAsync(StorageFolder folder, params string[] fileNames)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in fileNames)
            {
                StorageFile file = await TryGetFileAsync(folder, fileName);
                if (file != null)
                {
                    files[fileName] = file;
                }
            }

            return files;
        }

        private static async Task<StorageFile> TryGetFileAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await folder.GetFileAsync(fileName);
            }
            catch
            {
                return null;
            }
        }

        private async Task<StorageFile> GetBuiltInCommonOverlayFileAsync()
        {
            try
            {
                return await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri("ms-appx:///KillConfirmService/sounds/crossfire_swat_gr/common.wav"));
            }
            catch
            {
                return null;
            }
        }

        private async Task PlayPreviewAsync(StorageFile file)
        {
            if (file == null)
            {
                return;
            }

            try
            {
                _previewPlayer.Pause();
                _previewPlayer.Source = MediaSource.CreateFromStorageFile(file);
                _previewPlayer.Play();
            }
            catch
            {
                await Task.CompletedTask;
            }
        }

        private void ApplyLanguage()
        {
            if (LocalizationManager.Current == UiLanguage.SimplifiedChinese)
            {
                TitleText.Text = "击杀确认悬浮窗高级设置";
                InstructionText.Text = "这里主要用来管理、导入和创建语音包与图标包。";
                ShortcutText.Text = "如果走文件夹导入，按右侧推荐结构准备内容会最省事。";
                VoiceCollectionsTitleText.Text = "语音集合";
                VoiceCollectionsHintText.Text = "可以直接导入文件夹，也可以图形化新建。勾选后才会显示到 Game Bar。";
                IconCollectionsTitleText.Text = "图标集合";
                IconCollectionsHintText.Text = "可以直接导入文件夹，也可以图形化新建。勾选后才会显示到 Game Bar。";
                ImportVoicePackButton.Content = "导入语音包";
                CreateVoicePackButton.Content = "图形化新建";
                ImportIconPackButton.Content = "导入图标包";
                CreateIconPackButton.Content = "图形化新建";
                StructureTitleText.Text = "推荐文件夹结构";
                StructureBodyText.Text = "如果你走文件夹导入，这样组织内容最稳。语音包和图标包都可以只放一部分，缺的会跳过或回退。";
                StructureListText.Text =
                    "voice pack folder\n" +
                    "  sound.lua   (可选)\n" +
                    "  common_overlay.wav   (可选)\n" +
                    "  common.wav\n" +
                    "  2.wav ~ 8.wav\n" +
                    "  headshot.wav\n" +
                    "  knife.wav\n" +
                    "  firstandlast.wav\n\n" +
                    "icon pack folder\n" +
                    "  badge_multi1.png ~ badge_multi6.png\n" +
                    "  badge_headshot.png\n" +
                    "  badge_headshot_gold.png\n" +
                    "  badge_knife.png\n" +
                    "  FIRSTKILL.png\n" +
                    "  LASTKILL.png";
                return;
            }

            TitleText.Text = "Kill Confirm Overlay Advanced Settings";
            InstructionText.Text = "This page focuses on managing, importing, and creating voice packs and icon packs.";
            ShortcutText.Text = "For folder import, the structure on the right is the safest layout to follow.";
            VoiceCollectionsTitleText.Text = "Voice Collections";
            VoiceCollectionsHintText.Text = "Use folder import or the graphical creator. Only checked packs appear in Game Bar.";
            IconCollectionsTitleText.Text = "Icon Collections";
            IconCollectionsHintText.Text = "Use folder import or the graphical creator. Only checked packs appear in Game Bar.";
            ImportVoicePackButton.Content = "Import Voice Pack";
            CreateVoicePackButton.Content = "Create Visually";
            ImportIconPackButton.Content = "Import Icon Pack";
            CreateIconPackButton.Content = "Create Visually";
            StructureTitleText.Text = "Recommended Folder Layout";
            StructureBodyText.Text = "For folder import, this structure is the safest. Both voice packs and icon packs can be partial; missing items will skip or fall back.";
            StructureListText.Text =
                "voice pack folder\n" +
                "  sound.lua   (optional)\n" +
                "  common_overlay.wav   (optional)\n" +
                "  common.wav\n" +
                "  2.wav ~ 8.wav\n" +
                "  headshot.wav\n" +
                "  knife.wav\n" +
                "  firstandlast.wav\n\n" +
                "icon pack folder\n" +
                "  badge_multi1.png ~ badge_multi6.png\n" +
                "  badge_headshot.png\n" +
                "  badge_headshot_gold.png\n" +
                "  badge_knife.png\n" +
                "  FIRSTKILL.png\n" +
                "  LASTKILL.png";
        }
    }
}
