using System;
using System.IO;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace TestXboxGameBar
{
    sealed partial class App : Application
    {
        private const string WidgetId = "KillConfirmWidget";
        private const string SettingsWindowTitle = "Kill Confirm Overlay Advanced Settings";
        private const string RuntimeLogFileName = "gamebar-widget.log";
        private const long MaxRuntimeLogBytes = 512 * 1024;
        private static int? _guideViewId;

        private XboxGameBarWidget _clockWidget;

        public App()
        {
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            Suspending += OnSuspending;
            Log("App constructed.");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                Log("OnLaunched.");
                Frame rootFrame = Window.Current.Content as Frame;

                if (rootFrame == null)
                {
                    rootFrame = CreateRootFrame();
                    Window.Current.Content = rootFrame;
                }

                if (!e.PrelaunchActivated)
                {
                    if (rootFrame.Content == null)
                    {
                        rootFrame.Navigate(typeof(MainPage), e.Arguments);
                    }

                    ApplySettingsWindowTitle();
                    Window.Current.Activate();
                }
            }
            catch (Exception ex)
            {
                ShowFallback("Launch failed", ex);
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            try
            {
                Log("OnActivated kind=" + args.Kind);
                XboxGameBarWidgetActivatedEventArgs widgetArgs = null;

                if (args.Kind == ActivationKind.Protocol)
                {
                    var protocolArgs = args as IProtocolActivatedEventArgs;
                    Log("Protocol uri=" + protocolArgs?.Uri);

                    if (string.Equals(protocolArgs?.Uri?.Scheme, "ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
                    {
                        widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                        Log("Widget args cast=" + (widgetArgs != null));
                    }
                }

                if (widgetArgs == null)
                {
                    if (args.Kind == ActivationKind.Protocol)
                    {
                        Frame guideFrame = Window.Current.Content as Frame;
                        if (guideFrame == null)
                        {
                            guideFrame = CreateRootFrame();
                            Window.Current.Content = guideFrame;
                        }

                        guideFrame.Navigate(typeof(MainPage));
                        ApplySettingsWindowTitle();
                        Window.Current.Activate();
                        return;
                    }

                    base.OnActivated(args);
                    return;
                }

                Log("Widget activation extension=" + widgetArgs.AppExtensionId + ", launch=" + widgetArgs.IsLaunchActivation);

                if (!widgetArgs.IsLaunchActivation || !string.Equals(widgetArgs.AppExtensionId, WidgetId, StringComparison.OrdinalIgnoreCase))
                {
                    Window.Current.Activate();
                    return;
                }

                var rootFrame = CreateRootFrame();
                Window.Current.Content = rootFrame;

                _clockWidget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
                Window.Current.Closed += OnWidgetWindowClosed;

                rootFrame.Navigate(typeof(ClockWidgetPage), _clockWidget);
                Window.Current.Activate();
                Log("Widget window activated.");
            }
            catch (Exception ex)
            {
                ShowFallback("Widget activation failed", ex);
            }
        }

        private Frame CreateRootFrame()
        {
            var rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            return rootFrame;
        }

        private static void ApplySettingsWindowTitle()
        {
            try
            {
                ApplicationView.GetForCurrentView().Title = SettingsWindowTitle;
            }
            catch (Exception ex)
            {
                Log("Failed to apply settings window title: " + ex.Message);
            }
        }

        internal static async System.Threading.Tasks.Task<bool> TryShowGuideWindowAsync()
        {
            try
            {
                if (_guideViewId.HasValue)
                {
                    bool shownExisting = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(_guideViewId.Value);
                    Log("TryShowGuideWindowAsync existing view shown=" + shownExisting);
                    if (shownExisting)
                    {
                        return true;
                    }

                    _guideViewId = null;
                }

                int newViewId = 0;
                CoreApplicationView newView = CoreApplication.CreateNewView();
                await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame guideFrame = new Frame();
                    guideFrame.NavigationFailed += Current_NavigationFailed;
                    guideFrame.Navigate(typeof(MainPage));
                    Window.Current.Content = guideFrame;
                    Window.Current.Activate();

                    ApplicationView view = ApplicationView.GetForCurrentView();
                    view.Title = SettingsWindowTitle;
                    view.Consolidated += OnGuideViewConsolidated;
                    newViewId = view.Id;
                });

                bool shown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
                Log("TryShowGuideWindowAsync new view shown=" + shown + ", viewId=" + newViewId);
                if (shown)
                {
                    _guideViewId = newViewId;
                }

                return shown;
            }
            catch (Exception ex)
            {
                Log("TryShowGuideWindowAsync failed: " + ex);
                return false;
            }
        }

        private static void Current_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to load page " + e.SourcePageType.FullName, e.Exception);
        }

        private static void OnGuideViewConsolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            if (_guideViewId == sender.Id)
            {
                _guideViewId = null;
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to load page " + e.SourcePageType.FullName, e.Exception);
        }

        private void OnWidgetWindowClosed(object sender, CoreWindowEventArgs e)
        {
            Window.Current.Closed -= OnWidgetWindowClosed;
            ShutdownCompanionFromCurrentFrame();
            _clockWidget = null;
            Log("Widget window closed.");
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                await ShutdownCompanionFromCurrentFrameAsync();
                _clockWidget = null;
                Log("App suspending.");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ShutdownCompanionFromCurrentFrame()
        {
            var ignored = ShutdownCompanionFromCurrentFrameAsync();
        }

        private async System.Threading.Tasks.Task ShutdownCompanionFromCurrentFrameAsync()
        {
            try
            {
                if (Window.Current.Content is Frame frame && frame.Content is ClockWidgetPage page)
                {
                    await page.ShutdownCompanionAsync();
                    return;
                }

                await ClockWidgetPage.RequestServiceShutdownAsync();
            }
            catch (Exception ex)
            {
                Log("Companion shutdown from app failed: " + ex.Message);
            }
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Log("Unhandled exception: " + e.Exception);
            ShowFallback("Unhandled exception", e.Exception);
            e.Handled = true;
        }

        private void ShowFallback(string title, Exception ex)
        {
            Log(title + ": " + ex);

            var panel = new StackPanel
            {
                Margin = new Thickness(24),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                Foreground = new SolidColorBrush(Windows.UI.Colors.White)
            });
            panel.Children.Add(new TextBlock
            {
                Text = ex.Message,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 190, 210, 230))
            });
            panel.Children.Add(new TextBlock
            {
                Text = "See LocalState\\gamebar-widget.log for details.",
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 120, 144, 168))
            });

            Window.Current.Content = new Grid
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 15, 23, 30)),
                Children = { panel }
            };
            Window.Current.Activate();
        }

        internal static void Log(string message)
        {
            try
            {
                string folderPath = ApplicationData.Current.LocalFolder.Path;
                Directory.CreateDirectory(folderPath);

                string logPath = Path.Combine(folderPath, RuntimeLogFileName);
                RotateLogIfNeeded(logPath);

                string line = string.Format(
                    "[{0:yyyy-MM-dd HH:mm:ss.fff}] pid={1} {2}{3}",
                    DateTimeOffset.Now,
                    Environment.CurrentManagedThreadId,
                    message,
                    Environment.NewLine);
                File.AppendAllText(logPath, line);
            }
            catch
            {
            }
        }

        private static void RotateLogIfNeeded(string logPath)
        {
            try
            {
                var info = new FileInfo(logPath);
                if (!info.Exists || info.Length <= MaxRuntimeLogBytes)
                {
                    return;
                }

                string oldPath = logPath + ".old";
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }

                File.Move(logPath, oldPath);
            }
            catch
            {
            }
        }
    }
}
