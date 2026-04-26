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

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null)
            {
                return;
            }

            if (int.TryParse(button.Tag.ToString(), out int killCount))
            {
                KillAnimation.Play(killCount);
            }
        }
    }
}
