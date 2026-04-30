using System;
using TestXboxGameBar.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace TestXboxGameBar
{
    public sealed partial class MainPage : Page
    {
        private readonly DispatcherTimer _guideAnimationTimer;
        private int _guideAnimationIndex;

        public MainPage()
        {
            InitializeComponent();
            _guideAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _guideAnimationTimer.Tick += OnGuideAnimationTimerTick;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguage();
            PlayNextGuideAnimation();
            _guideAnimationTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _guideAnimationTimer.Stop();
        }

        private void OnGuideAnimationTimerTick(object sender, object e)
        {
            PlayNextGuideAnimation();
        }

        private void PlayNextGuideAnimation()
        {
            switch (_guideAnimationIndex % 11)
            {
                case 0:
                    KillAnimation.Play(1);
                    break;
                case 1:
                    KillAnimation.Play(2);
                    break;
                case 2:
                    KillAnimation.Play(3);
                    break;
                case 3:
                    KillAnimation.Play(4);
                    break;
                case 4:
                    KillAnimation.Play(5);
                    break;
                case 5:
                    KillAnimation.Play(6);
                    break;
                case 6:
                    KillAnimation.Play(1, true);
                    break;
                case 7:
                    KillAnimation.PlayNamed("goldheadshot");
                    break;
                case 8:
                    KillAnimation.PlayNamed("firstkill");
                    break;
                case 9:
                    KillAnimation.PlayNamed("knife_kill");
                    break;
                default:
                    KillAnimation.PlayNamed("last_kill");
                    break;
            }

            _guideAnimationIndex++;
        }

        private void ApplyLanguage()
        {
            TitleText.Text = LocalizationManager.Text("MainTitle");
            InstructionText.Text = LocalizationManager.Text("MainInstruction");
            ShortcutText.Text = LocalizationManager.Text("MainShortcut");
            GuideSetupTitleText.Text = LocalizationManager.Text("GuideSetupTitle");
            GuidePinText.Text = LocalizationManager.Text("GuidePin");
            GuideLightsText.Text = LocalizationManager.Text("GuideLights");
            GuideCfgText.Text = LocalizationManager.Text("GuideCfg");
            GuideTestText.Text = LocalizationManager.Text("GuideTest");
            GuideControlsTitleText.Text = LocalizationManager.Text("GuideControlsTitle");
            GuideVoiceText.Text = LocalizationManager.Text("GuideVoice");
            GuideViewText.Text = LocalizationManager.Text("GuideView");
            GuideServiceText.Text = LocalizationManager.Text("GuideService");
            GuidePreviewTitleText.Text = LocalizationManager.Text("GuidePreviewTitle");
            MockPinText.Text = LocalizationManager.Text("MockPin");
            MockReadyText.Text = LocalizationManager.Text("MockReady");
            MockCfgText.Text = LocalizationManager.Text("CfgSelectRootHint");
        }
    }
}
