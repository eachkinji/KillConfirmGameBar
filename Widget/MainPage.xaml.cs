using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestXboxGameBar.Services;
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
            await BuildVoicePackListAsync();
            await BuildIconPackListAsync();
        }

        private async Task BuildVoicePackListAsync()
        {
            var items = await PackCatalogService.GetAllVoicePacksAsync();
            VoiceVisibleCountText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? $"Game Bar 里显示 {CountVisible(items)} 个语音包"
                : $"{CountVisible(items)} voice packs visible in Game Bar";
            VoicePackListPanel.Children.Clear();

            foreach (VoicePackItem item in items)
            {
                VoicePackListPanel.Children.Add(CreateVoiceRow(item));
            }
        }

        private async Task BuildIconPackListAsync()
        {
            var items = await PackCatalogService.GetAllIconPacksAsync();
            IconVisibleCountText.Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese
                ? $"Game Bar 里显示 {CountVisible(items)} 个图标包"
                : $"{CountVisible(items)} icon packs visible in Game Bar";
            IconPackListPanel.Children.Clear();

            foreach (IconPackItem item in items)
            {
                IconPackListPanel.Children.Add(CreateIconRow(item));
            }
        }

        private static int CountVisible<T>(IEnumerable<T> items)
        {
            int count = 0;
            foreach (object item in items)
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

        private UIElement CreateVoiceRow(VoicePackItem item)
        {
            var grid = CreatePackRowGrid();

            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetVoicePackVisibilityAsync(item.Key, false);
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);

            grid.Children.Add(CreatePackNameBlock(
                PackCatalogService.GetVoicePackDisplayName(item),
                item.IsBuiltIn
                    ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "内置" : "Built-in")
                    : (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "自定义" : "Custom"),
                item.FolderPath,
                1));

            if (!item.IsBuiltIn)
            {
                Button removeButton = CreateRemoveButton(async () =>
                {
                    await PackCatalogService.RemoveCustomVoicePackAsync(item.Key);
                });
                Grid.SetColumn(removeButton, 2);
                grid.Children.Add(removeButton);
            }

            return grid;
        }

        private UIElement CreateIconRow(IconPackItem item)
        {
            var grid = CreatePackRowGrid();

            var checkBox = new CheckBox
            {
                IsChecked = item.IsVisibleInWidget,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, true);
            checkBox.Unchecked += async (_, __) => await PackCatalogService.SetIconPackVisibilityAsync(item.Key, false);
            Grid.SetColumn(checkBox, 0);
            grid.Children.Add(checkBox);

            grid.Children.Add(CreatePackNameBlock(
                item.DisplayName,
                item.IsBuiltIn
                    ? (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "内置" : "Built-in")
                    : (LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "自定义" : "Custom"),
                item.FolderPath,
                1));

            if (!item.IsBuiltIn)
            {
                Button removeButton = CreateRemoveButton(async () =>
                {
                    await PackCatalogService.RemoveCustomIconPackAsync(item.Key);
                });
                Grid.SetColumn(removeButton, 2);
                grid.Children.Add(removeButton);
            }

            return grid;
        }

        private static Grid CreatePackRowGrid()
        {
            var grid = new Grid
            {
                ColumnSpacing = 10
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            return grid;
        }

        private static UIElement CreatePackNameBlock(string displayName, string kindLabel, string detail, int column)
        {
            var stack = new StackPanel
            {
                Spacing = 2
            };

            stack.Children.Add(new TextBlock
            {
                Text = displayName,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255))
            });

            stack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(detail) ? kindLabel : $"{kindLabel} · {detail}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                TextWrapping = TextWrapping.WrapWholeWords
            });

            Grid.SetColumn(stack, column);
            return stack;
        }

        private static Button CreateRemoveButton(Func<Task> onClick)
        {
            var button = new Button
            {
                Content = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "删除" : "Remove",
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            button.Click += async (_, __) => await onClick();
            return button;
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

            await PackCatalogService.ImportVoicePackAsync(folder);
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

            await PackCatalogService.ImportIconPackAsync(folder);
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateVoicePackDialogAsync();
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateIconPackDialogAsync();
        }

        private async Task ShowCreateVoicePackDialogAsync()
        {
            var slots = new[]
            {
                ("common.wav", "普通单杀"),
                ("2.wav", "双杀"),
                ("3.wav", "三杀"),
                ("4.wav", "四杀"),
                ("5.wav", "五杀"),
                ("6.wav", "六杀"),
                ("7.wav", "七杀"),
                ("8.wav", "八杀"),
                ("headshot.wav", "爆头"),
                ("knife.wav", "刀杀"),
                ("firstandlast.wav", "首杀/尾杀")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "图形化新建语音包" : "Create Voice Pack",
                LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? "把已有音频逐个填到这些槽位里。留空也能创建，没填的事件会自动跳过。"
                    : "Fill these slots with existing audio files. Empty slots are allowed and will simply be skipped.",
                slots,
                new[] { ".wav" },
                PackCatalogService.CreateVoicePackAsync);
        }

        private async Task ShowCreateIconPackDialogAsync()
        {
            var slots = new[]
            {
                ("badge_multi1.png", "单杀"),
                ("badge_multi2.png", "双杀"),
                ("badge_multi3.png", "三杀"),
                ("badge_multi4.png", "四杀"),
                ("badge_multi5.png", "五杀"),
                ("badge_multi6.png", "六杀"),
                ("badge_headshot.png", "爆头"),
                ("badge_headshot_gold.png", "首尾杀爆头"),
                ("badge_knife.png", "刀杀"),
                ("FIRSTKILL.png", "首杀牌"),
                ("LASTKILL.png", "尾杀牌")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "图形化新建图标包" : "Create Icon Pack",
                LocalizationManager.Current == UiLanguage.SimplifiedChinese
                    ? "把 PNG 图标逐个填到这些槽位里。留空也能创建，缺图会自动回退原版。"
                    : "Fill these PNG slots one by one. Empty slots are allowed and will fall back to Original.",
                slots,
                new[] { ".png" },
                PackCatalogService.CreateIconPackAsync);
        }

        private async Task ShowPackCreationDialogAsync(
            string title,
            string description,
            (string FileName, string Label)[] slots,
            string[] fileFilters,
            Func<string, IReadOnlyDictionary<string, StorageFile>, Task> createHandler)
        {
            var selectedFiles = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            bool supportsImagePreview = Array.Exists(fileFilters, filter => string.Equals(filter, ".png", StringComparison.OrdinalIgnoreCase));

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "输入包名称" : "Pack name"
            };

            var layout = new StackPanel
            {
                Spacing = 10
            };
            layout.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var scroll = new ScrollViewer
            {
                MaxHeight = 420
            };
            var slotPanel = new StackPanel
            {
                Spacing = 8
            };
            scroll.Content = slotPanel;

            foreach (var slot in slots)
            {
                var fileNameText = new TextBlock
                {
                    Text = LocalizationManager.Current == UiLanguage.SimplifiedChinese ? "未选择" : "Not selected",
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
                        Visibility = Visibility.Collapsed
                    };
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

        private void ApplyLanguage()
        {
            if (LocalizationManager.Current == UiLanguage.SimplifiedChinese)
            {
                TitleText.Text = "击杀确认悬浮窗高级设置";
                InstructionText.Text = "这里先专注做语音包和图标包的管理、导入和创建。";
                ShortcutText.Text = "文件夹导入时，按右侧推荐结构准备内容会最省事。";
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
                    "  sound.lua\n" +
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
            InstructionText.Text = "This page now focuses on managing, importing, and creating voice packs and icon packs.";
            ShortcutText.Text = "For folder import, the structure on the right is the safest layout to follow.";
            VoiceCollectionsTitleText.Text = "Voice Collections";
            VoiceCollectionsHintText.Text = "Use folder import or the graphical creator. Only checked packs appear in Game Bar.";
            IconCollectionsTitleText.Text = "Icon Collections";
            IconCollectionsHintText.Text = "Use folder import or the graphical creator. Only checked packs appear in Game Bar.";
            ImportVoicePackButton.Content = "Import Folder";
            CreateVoicePackButton.Content = "Create Visually";
            ImportIconPackButton.Content = "Import Folder";
            CreateIconPackButton.Content = "Create Visually";
            StructureTitleText.Text = "Recommended Folder Layout";
            StructureBodyText.Text = "For folder import, this structure is the safest. Both voice packs and icon packs can be partial; missing items will skip or fall back.";
            StructureListText.Text =
                "voice pack folder\n" +
                "  sound.lua\n" +
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
