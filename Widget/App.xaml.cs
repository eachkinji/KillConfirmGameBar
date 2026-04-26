using System;
using System.IO;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace TestXboxGameBar
{
    sealed partial class App : Application
    {
        private const string WidgetId = "KillConfirmWidget";

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

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new InvalidOperationException("Failed to load page " + e.SourcePageType.FullName, e.Exception);
        }

        private void OnWidgetWindowClosed(object sender, CoreWindowEventArgs e)
        {
            Window.Current.Closed -= OnWidgetWindowClosed;
            _clockWidget = null;
            Log("Widget window closed.");
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            _clockWidget = null;
            Log("App suspending.");
            deferral.Complete();
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
                string logPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "gamebar-widget.log");
                string line = DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine;
                File.AppendAllText(logPath, line);
            }
            catch
            {
            }
        }
    }
}
