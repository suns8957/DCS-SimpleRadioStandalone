using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    public static class ToolTips
    {
        public static ToolTip ExternalAWACSMode;
        public static ToolTip ExternalAWACSModeName;
        public static ToolTip ExternalAWACSModePassword;

        public static void Init()
        {
            ExternalAWACSMode = new ToolTip();
            StackPanel externalAWACSModeContent = new StackPanel();

            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMButton,
                FontWeight = FontWeights.Bold
            });
            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMButtonL1
            });
            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMButtonL2
            });

            ExternalAWACSMode.Content = externalAWACSModeContent;


            ExternalAWACSModeName = new ToolTip();
            StackPanel externalAWACSModeNameContent = new StackPanel();

            externalAWACSModeNameContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMName,
                FontWeight = FontWeights.Bold
            });
            externalAWACSModeNameContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMNameL1
            });

            ExternalAWACSModeName.Content = externalAWACSModeNameContent;


            ExternalAWACSModePassword = new ToolTip();
            StackPanel externalAWACSModePasswordContent = new StackPanel();

            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMPassword,
                FontWeight = FontWeights.Bold
            });
            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMPasswordL1
            });
            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = Properties.Resources.ToolTipEAMPasswordL2
            });

            ExternalAWACSModePassword.Content = externalAWACSModePasswordContent;
        }
    }
}
