using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
using TestXboxGameBar.Helpers;

namespace TestXboxGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly MediaPlayer _previewPlayer = new MediaPlayer();
        private static readonly string[] VoicePackImportFiles =
        {
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
            "firstandlast.wav"
        };
        private static readonly string[] IconPackImportFiles =
        {
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
            "LASTKILL.png",
            "KillMark_Upgrade1.png",
            "KillMark_Upgrade2.png",
            "KillMark_Upgrade3.png",
            "multi2_fx.png",
            "multi3_fx.png",
            "multi4_fx.png",
            "multi5_fx.png",
            "multi6_fx.png",
            "badge_knife_1.png",
            "badge_knife_2.png",
            "badge_knife_3.png",
            "badge_assault1.png",
            "badge_assault2.png",
            "badge_assault3.png",
            "badge_scout1.png",
            "badge_scout2.png",
            "badge_scout3.png",
            "badge_sniper1.png",
            "badge_sniper2.png",
            "badge_sniper3.png",
            "badge_elite1.png",
            "badge_elite2.png",
            "badge_elite3.png",
            "badge_knife1.png",
            "badge_knife2.png",
            "badge_knife3.png"
        };

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
            VoiceVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(items));

            VoicePackListPanel.Children.Clear();
            foreach (VoicePackItem item in items)
            {
                VoicePackListPanel.Children.Add(BuildVoicePackRow(item));
            }
        }

        private async Task RebuildIconPackListAsync()
        {
            var items = await PackCatalogService.GetAllIconPacksAsync();
            IconVisibleCountText.Text = string.Format(LocalizationManager.Text("VisibleCount"), CountVisible(items));

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
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            var meta = new TextBlock
            {
                Text = item.IsBuiltIn ? LocalizationManager.Text("BuiltIn") : LocalizationManager.Text("Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 170, 170, 170)),
                FontSize = 12
            };
            var editButton = new Button
            {
                Content = LocalizationManager.Text("Edit"),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 180, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 68, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 6, 0),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                var existingFiles = await CollectRecognizedFilesFromFolderAsync(
                    item.FolderPath,
                    "common.wav", "2.wav", "3.wav", "4.wav", "5.wav",
                    "6.wav", "7.wav", "8.wav", "headshot.wav", "knife.wav", "firstandlast.wav");
                await ShowCreateVoicePackDialogAsync(item.DisplayName, existingFiles);
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 77, 79)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 68, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomVoicePackAsync(item.Key);
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(deleteButton);
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(checkBox);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            Grid.SetColumn(buttonPanel, 2);
            row.Children.Add(buttonPanel);
            return new Border
            {
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 4),
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
                Text = PackCatalogService.GetIconPackDisplayName(item),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            var meta = new TextBlock
            {
                Text = item.IsBuiltIn ? LocalizationManager.Text("BuiltIn") : LocalizationManager.Text("Custom"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 170, 170, 170)),
                FontSize = 12
            };
            var editButton = new Button
            {
                Content = LocalizationManager.Text("Edit"),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 180, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 68, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 6, 0),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            editButton.Click += async (_, __) =>
            {
                var existingFiles = await CollectRecognizedFilesFromFolderAsync(
                    item.FolderPath,
                    "badge_multi1.png", "badge_multi2.png", "badge_multi3.png",
                    "badge_multi4.png", "badge_multi5.png", "badge_multi6.png",
                    "badge_headshot.png", "badge_headshot_gold.png", "badge_knife.png",
                    "FIRSTKILL.png", "LASTKILL.png",
                    "KillMark_Upgrade1.png", "KillMark_Upgrade2.png", "KillMark_Upgrade3.png",
                    "multi2_fx.png", "multi3_fx.png", "multi4_fx.png", "multi5_fx.png", "multi6_fx.png",
                    "badge_knife_1.png", "badge_knife_2.png", "badge_knife_3.png",
                    "badge_assault1.png", "badge_assault2.png", "badge_assault3.png",
                    "badge_scout1.png", "badge_scout2.png", "badge_scout3.png",
                    "badge_sniper1.png", "badge_sniper2.png", "badge_sniper3.png",
                    "badge_elite1.png", "badge_elite2.png", "badge_elite3.png",
                    "badge_knife1.png", "badge_knife2.png", "badge_knife3.png");
                await ShowCreateIconPackDialogAsync(item.DisplayName, existingFiles);
            };
            var deleteButton = new Button
            {
                Content = LocalizationManager.Text("Delete"),
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 77, 79)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 68, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Visibility = item.IsBuiltIn ? Visibility.Collapsed : Visibility.Visible
            };
            deleteButton.Click += async (_, __) => await PackCatalogService.RemoveCustomIconPackAsync(item.Key);
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(title);
            content.Children.Add(meta);
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
            buttonPanel.Children.Add(editButton);
            buttonPanel.Children.Add(deleteButton);
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(checkBox);
            Grid.SetColumn(content, 1);
            row.Children.Add(content);
            Grid.SetColumn(buttonPanel, 2);
            row.Children.Add(buttonPanel);
            return new Border
            {
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 4),
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
                await CollectRecognizedFilesAsync(folder, VoicePackImportFiles),
                await TryGetFileAsync(folder, "common_overlay.wav"));
        }

        private async void OnImportVoiceZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                VoicePackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateVoicePackDialogAsync(
                        folder.DisplayName,
                        files,
                        await TryGetFileAsync(folder, "common_overlay.wav"));
                });
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
                await CollectRecognizedFilesAsync(folder, IconPackImportFiles));
        }

        private async void OnImportIconZipClick(object sender, RoutedEventArgs e)
        {
            await ImportPackFromZipAsync(
                IconPackImportFiles,
                async (folder, files) =>
                {
                    await ShowCreateIconPackDialogAsync(folder.DisplayName, files);
                });
        }

        private async void OnCreateVoicePackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateVoicePackDialogAsync();
        }

        private async void OnCreateIconPackClick(object sender, RoutedEventArgs e)
        {
            await ShowCreateIconPackDialogAsync();
        }

        private async Task ImportPackFromZipAsync(
            IReadOnlyList<string> recognizedFileNames,
            Func<StorageFolder, IReadOnlyDictionary<string, StorageFile>, Task> showDialogAsync)
        {
            StorageFile zipFile = await PickSingleFileAsync(new[] { ".zip" });
            if (zipFile == null)
            {
                return;
            }

            StorageFolder extractedFolder = null;
            try
            {
                extractedFolder = await ExtractZipToTemporaryFolderAsync(zipFile);
                StorageFolder bestFolder = await FindBestPackFolderAsync(extractedFolder, recognizedFileNames);
                IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(bestFolder, recognizedFileNames.ToArray());
                if (files.Count == 0)
                {
                    await ShowMessageAsync(
                        LocalizationManager.Text("ZipImportFailedTitle"),
                        LocalizationManager.Text("ZipImportNoFilesMessage"));
                    return;
                }

                await showDialogAsync(bestFolder, files);
            }
            catch
            {
                await ShowMessageAsync(
                    LocalizationManager.Text("ZipImportFailedTitle"),
                    LocalizationManager.Text("ZipImportFailedMessage"));
            }
            finally
            {
                if (extractedFolder != null)
                {
                    try
                    {
                        await extractedFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task ShowCreateVoicePackDialogAsync(
            string initialDisplayName = null,
            IReadOnlyDictionary<string, StorageFile> initialFiles = null,
            StorageFile initialCommonOverlayFile = null)
        {
            var slots = new[]
            {
                ("common.wav", LocalizationManager.Text("SingleKill")),
                ("2.wav", LocalizationManager.Text("DoubleKill")),
                ("3.wav", LocalizationManager.Text("TripleKill")),
                ("4.wav", LocalizationManager.Text("QuadraKill")),
                ("5.wav", LocalizationManager.Text("PentaKill")),
                ("6.wav", LocalizationManager.Text("HexaKill")),
                ("7.wav", LocalizationManager.Text("HeptaKill")),
                ("8.wav", LocalizationManager.Text("OctaKill")),
                ("headshot.wav", LocalizationManager.Text("Headshot")),
                ("knife.wav", LocalizationManager.Text("KnifeKill")),
                ("firstandlast.wav", LocalizationManager.Text("FirstLastKill"))
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
                PlaceholderText = LocalizationManager.Text("VoicePackNamePlaceholder"),
                Text = initialDisplayName ?? string.Empty
            };

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("VoicePackCreationHint"),
                TextWrapping = TextWrapping.WrapWholeWords
            });
            layout.Children.Add(nameBox);

            var commonOverlayCard = new StackPanel { Spacing = 6 };
            commonOverlayCard.Children.Add(new TextBlock
            {
                Text = LocalizationManager.Text("OneKillSound"),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 247, 251, 255))
            });

            var commonOverlayMode = new ComboBox { MinWidth = 180 };
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("UseBuiltInCommon"),
                Tag = "builtin"
            });
            commonOverlayMode.Items.Add(new ComboBoxItem
            {
                Content = LocalizationManager.Text("ChooseCustomAudio"),
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
                    ?? LocalizationManager.Text("UseBuiltInCommon"),
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
                Content = LocalizationManager.Text("ChooseFile"),
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
                        ?? LocalizationManager.Text("NotSelected");
                }
                else
                {
                    commonOverlayFileText.Text = LocalizationManager.Text("UseBuiltInCommon");
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
                Content = LocalizationManager.Text("EnableAllOverlay"),
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
                Content = LocalizationManager.Text("DisableAllOverlay"),
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
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 142, 164, 184)),
                    TextWrapping = TextWrapping.WrapWholeWords,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(fileText, 1);
                row.Children.Add(fileText);

                var overlayCheckBox = new CheckBox
                {
                    Content = LocalizationManager.Text("LayerCommon"),
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
                    Content = LocalizationManager.Text("ChooseFile"),
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
                Title = LocalizationManager.Text("CreateVoicePack"),
                Content = layout,
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel")
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
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
                ("badge_multi1.png", LocalizationManager.Text("SingleKill")),
                ("badge_multi2.png", LocalizationManager.Text("DoubleKill")),
                ("badge_multi3.png", LocalizationManager.Text("TripleKill")),
                ("badge_multi4.png", LocalizationManager.Text("QuadraKill")),
                ("badge_multi5.png", LocalizationManager.Text("PentaKill")),
                ("badge_multi6.png", LocalizationManager.Text("HexaKill")),
                ("badge_headshot.png", LocalizationManager.Text("Headshot")),
                ("badge_headshot_gold.png", LocalizationManager.Text("FirstLastKill")),
                ("badge_knife.png", LocalizationManager.Text("KnifeKill")),
                ("FIRSTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("LASTKILL.png", LocalizationManager.Text("FirstLastKill")),
                ("KillMark_Upgrade1.png", LocalizationManager.Text("EliteLevel1")),
                ("KillMark_Upgrade2.png", LocalizationManager.Text("EliteLevel2")),
                ("KillMark_Upgrade3.png", LocalizationManager.Text("EliteLevel3")),
                ("multi2_fx.png", LocalizationManager.Text("DoubleKillFX")),
                ("multi3_fx.png", LocalizationManager.Text("TripleKillFX")),
                ("multi4_fx.png", LocalizationManager.Text("QuadraKillFX")),
                ("multi5_fx.png", LocalizationManager.Text("PentaKillFX")),
                ("multi6_fx.png", LocalizationManager.Text("HexaKillFX")),
                ("badge_knife_1.png", LocalizationManager.Text("EliteKnife1")),
                ("badge_knife_2.png", LocalizationManager.Text("EliteKnife2")),
                ("badge_knife_3.png", LocalizationManager.Text("EliteKnife3")),
                ("badge_assault1.png", LocalizationManager.Text("ClassAssault") + " 1"),
                ("badge_assault2.png", LocalizationManager.Text("ClassAssault") + " 2"),
                ("badge_assault3.png", LocalizationManager.Text("ClassAssault") + " 3"),
                ("badge_scout1.png", LocalizationManager.Text("ClassScout") + " 1"),
                ("badge_scout2.png", LocalizationManager.Text("ClassScout") + " 2"),
                ("badge_scout3.png", LocalizationManager.Text("ClassScout") + " 3"),
                ("badge_sniper1.png", LocalizationManager.Text("ClassSniper") + " 1"),
                ("badge_sniper2.png", LocalizationManager.Text("ClassSniper") + " 2"),
                ("badge_sniper3.png", LocalizationManager.Text("ClassSniper") + " 3"),
                ("badge_elite1.png", LocalizationManager.Text("ClassElite") + " 1"),
                ("badge_elite2.png", LocalizationManager.Text("ClassElite") + " 2"),
                ("badge_elite3.png", LocalizationManager.Text("ClassElite") + " 3"),
                ("badge_knife1.png", LocalizationManager.Text("ClassKnife") + " 1"),
                ("badge_knife2.png", LocalizationManager.Text("ClassKnife") + " 2"),
                ("badge_knife3.png", LocalizationManager.Text("ClassKnife") + " 3")
            };

            await ShowPackCreationDialogAsync(
                LocalizationManager.Text("CreateIconPack"),
                LocalizationManager.Text("IconPackCreationHint"),
                slots,
                new[] { ".png", ".tga" },
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
            bool supportsImagePreview = Array.Exists(fileFilters, filter => 
                string.Equals(filter, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, ".tga", StringComparison.OrdinalIgnoreCase));

            var nameBox = new TextBox
            {
                PlaceholderText = LocalizationManager.Text("IconPackNamePlaceholder"),
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
                    Text = existingFile?.Name ?? LocalizationManager.Text("NotSelected"),
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
                    Content = LocalizationManager.Text("ChooseFile"),
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
                PrimaryButtonText = LocalizationManager.Text("Create"),
                CloseButtonText = LocalizationManager.Text("Cancel")
            };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || selectedFiles.Count == 0)
            {
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(nameBox.Text)
                ? LocalizationManager.Text("NewPack")
                : nameBox.Text.Trim();
            await createHandler(displayName, selectedFiles);
        }

        private static async Task SetPreviewImageAsync(Image image, StorageFile file)
        {
            try
            {
                if (file.FileType.Equals(".tga", StringComparison.OrdinalIgnoreCase))
                {
                    var softwareBitmap = await TgaDecoder.GetSoftwareBitmapAsync(file);
                    if (softwareBitmap != null)
                    {
                        var source = new SoftwareBitmapSource();
                        await source.SetBitmapAsync(softwareBitmap);
                        image.Source = source;
                        image.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        image.Source = null;
                        image.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    var bitmap = new BitmapImage();
                    using (var stream = await file.OpenReadAsync())
                    {
                        await bitmap.SetSourceAsync(stream);
                    }
                    image.Source = bitmap;
                    image.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
        }

        private static async Task<StorageFolder> ExtractZipToTemporaryFolderAsync(StorageFile zipFile)
        {
            StorageFolder tempRoot = await ApplicationData.Current.TemporaryFolder.CreateFolderAsync(
                "ImportedPack_" + Guid.NewGuid().ToString("N"),
                CreationCollisionOption.FailIfExists);

            using (Stream zipStream = await zipFile.OpenStreamForReadAsync())
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.FullName))
                    {
                        continue;
                    }

                    string normalizedPath = entry.FullName.Replace('\\', '/');
                    string[] segments = normalizedPath
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0 || segments.Any(IsUnsafeZipPathSegment))
                    {
                        continue;
                    }

                    bool isDirectory = normalizedPath.EndsWith("/", StringComparison.Ordinal);
                    StorageFolder targetFolder = await CreateFolderPathAsync(
                        tempRoot,
                        isDirectory ? segments : segments.Take(segments.Length - 1));

                    if (isDirectory)
                    {
                        continue;
                    }

                    StorageFile targetFile = await targetFolder.CreateFileAsync(
                        segments[segments.Length - 1],
                        CreationCollisionOption.ReplaceExisting);
                    using (Stream entryStream = entry.Open())
                    using (Stream targetStream = await targetFile.OpenStreamForWriteAsync())
                    {
                        targetStream.SetLength(0);
                        await entryStream.CopyToAsync(targetStream);
                    }
                }
            }

            return tempRoot;
        }

        private static bool IsUnsafeZipPathSegment(string segment)
        {
            return string.IsNullOrWhiteSpace(segment)
                || segment == "."
                || segment == ".."
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }

        private static async Task<StorageFolder> CreateFolderPathAsync(StorageFolder root, IEnumerable<string> segments)
        {
            StorageFolder current = root;
            foreach (string segment in segments)
            {
                current = await current.CreateFolderAsync(segment, CreationCollisionOption.OpenIfExists);
            }

            return current;
        }

        private static async Task<StorageFolder> FindBestPackFolderAsync(StorageFolder root, IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = root;
            int bestScore = await CountRecognizedFilesAsync(root, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await root.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder folder, int score) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (score > bestScore)
                {
                    bestFolder = folder;
                    bestScore = score;
                }
            }

            return bestFolder;
        }

        private static async Task<(StorageFolder Folder, int Score)> FindBestPackFolderRecursiveAsync(
            StorageFolder folder,
            IReadOnlyList<string> recognizedFileNames)
        {
            StorageFolder bestFolder = folder;
            int bestScore = await CountRecognizedFilesAsync(folder, recognizedFileNames);
            IReadOnlyList<StorageFolder> subFolders = await folder.GetFoldersAsync();
            foreach (StorageFolder subFolder in subFolders)
            {
                (StorageFolder candidateFolder, int candidateScore) = await FindBestPackFolderRecursiveAsync(subFolder, recognizedFileNames);
                if (candidateScore > bestScore)
                {
                    bestFolder = candidateFolder;
                    bestScore = candidateScore;
                }
            }

            return (bestFolder, bestScore);
        }

        private static async Task<int> CountRecognizedFilesAsync(StorageFolder folder, IReadOnlyList<string> recognizedFileNames)
        {
            IReadOnlyDictionary<string, StorageFile> files = await CollectRecognizedFilesAsync(folder, recognizedFileNames.ToArray());
            return files.Count;
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

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = LocalizationManager.Text("Cancel")
            };
            await dialog.ShowAsync();
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesAsync(StorageFolder folder, params string[] fileNames)
        {
            var files = new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in fileNames)
            {
                StorageFile file = await TryGetFileAsync(folder, fileName);
                if (file == null && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // If .png not found, check for .tga
                    string tgaName = System.IO.Path.ChangeExtension(fileName, ".tga");
                    file = await TryGetFileAsync(folder, tgaName);

                    // If still not found, check subfolders like "badgeex"
                    if (file == null)
                    {
                        try
                        {
                            StorageFolder badgeex = await folder.GetFolderAsync("badgeex");
                            file = await TryGetFileAsync(badgeex, tgaName) 
                                ?? await TryGetFileAsync(badgeex, fileName);
                        }
                        catch { }
                    }
                }

                if (file != null)
                {
                    files[fileName] = file;
                }
            }

            return files;
        }

        private static async Task<IReadOnlyDictionary<string, StorageFile>> CollectRecognizedFilesFromFolderAsync(string folderPath, params string[] fileNames)
        {
            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
                return await CollectRecognizedFilesAsync(folder, fileNames);
            }
            catch
            {
                return new Dictionary<string, StorageFile>(StringComparer.OrdinalIgnoreCase);
            }
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
            TitleText.Text = LocalizationManager.Text("MainTitle");
            InstructionText.Text = LocalizationManager.Text("MainInstruction");
            ShortcutText.Text = LocalizationManager.Text("MainShortcut");
            
            VoiceCollectionsTitleText.Text = LocalizationManager.Text("VoiceCollectionsTitle");
            VoiceCollectionsHintText.Text = LocalizationManager.Text("VoiceCollectionsHint");
            IconCollectionsTitleText.Text = LocalizationManager.Text("IconCollectionsTitle");
            IconCollectionsHintText.Text = LocalizationManager.Text("IconCollectionsHint");
            
            ImportVoicePackButton.Content = LocalizationManager.Text("ImportVoicePack");
            ImportVoiceZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateVoicePackButton.Content = LocalizationManager.Text("CreateVoicePack");
            ImportIconPackButton.Content = LocalizationManager.Text("ImportIconPack");
            ImportIconZipButton.Content = LocalizationManager.Text("ImportZip");
            CreateIconPackButton.Content = LocalizationManager.Text("CreateIconPack");
            
            StructureTitleText.Text = LocalizationManager.Text("StructureTitle");
            StructureBodyText.Text = LocalizationManager.Text("StructureBody");
            StructureListText.Text = LocalizationManager.Text("StructureList");
            
            TipsTitleText.Text = LocalizationManager.Text("TipsTitle");
            TipsBodyText.Text = LocalizationManager.Text("TipsBody");
        }
    }
}
