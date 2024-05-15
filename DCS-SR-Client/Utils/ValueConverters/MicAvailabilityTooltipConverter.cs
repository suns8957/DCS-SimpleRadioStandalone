using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    class MicAvailabilityTooltipConverter : IValueConverter
    {
		private static ToolTip _noMicAvailable = BuildTooltip();

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool micAvailable = (bool)value;
			if (micAvailable)
			{
				return null;
			}
			else
			{
				return _noMicAvailable;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		private static ToolTip BuildTooltip()
		{
			var NoMicAvailable = new ToolTip();
			StackPanel noMicAvailableContent = new StackPanel();

			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = Properties.Resources.ToolTipNoMic,
				FontWeight = FontWeights.Bold
			});
			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = Properties.Resources.ToolTipNoMicL1
            });
			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = Properties.Resources.ToolTipNoMicL2
            });

			NoMicAvailable.Content = noMicAvailableContent;
			return NoMicAvailable;
		}
	}
}
