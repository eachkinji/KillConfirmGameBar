using System;
using System.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestXboxGameBar.Controls
{
    public sealed partial class ClockFace : UserControl
    {
        private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("zh-CN");
        private readonly DispatcherTimer _timer;

        public ClockFace()
        {
            InitializeComponent();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateClock();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClock();
            _timer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
        }

        private void OnTimerTick(object sender, object e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            TimeText.Text = now.ToString("HH:mm", _culture);
            SecondsText.Text = now.ToString(":ss", _culture);
            DateText.Text = now.ToString("yyyy-MM-dd dddd", _culture);
        }
    }
}
