using TestXboxGameBar.Services;
using Windows.UI.Xaml.Controls;

namespace TestXboxGameBar
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            ApplyLanguage();
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
