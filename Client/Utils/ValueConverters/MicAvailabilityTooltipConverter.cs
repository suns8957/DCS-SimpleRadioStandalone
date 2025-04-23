using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils.ValueConverters;

internal class MicAvailabilityTooltipConverter : IValueConverter
{
    private static readonly ToolTip _noMicAvailable = BuildTooltip();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var micAvailable = (bool)value;
        if (micAvailable)
            return null;
        return _noMicAvailable;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static ToolTip BuildTooltip()
    {
        var NoMicAvailable = new ToolTip();
        var noMicAvailableContent = new StackPanel();

        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text = "No microphone available",
            FontWeight = FontWeights.Bold
        });
        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text = "No valid microphone is available - others will not be able to hear you."
        });
        noMicAvailableContent.Children.Add(new TextBlock
        {
            Text =
                "You can still use SRS to listen to radio calls, but will not be able to transmit anything yourself."
        });

        NoMicAvailable.Content = noMicAvailableContent;
        return NoMicAvailable;
    }
}