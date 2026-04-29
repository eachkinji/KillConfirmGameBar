using System;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace TestXboxGameBar
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            KillAnimation.Play(1);
        }

        private async void OnOpenGameBarClick(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-gamebar:"));
        }
    }
}
