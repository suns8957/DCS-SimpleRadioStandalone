using System;
using System.Globalization;
using System.Windows.Data;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.FS3D.SimpleRadio.Standalone.Client.UI;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Utils.ValueConverters;

internal class ConnectionStatusImageConverter : IValueConverter
{
    private ClientStateSingleton _clientState { get; } = ClientStateSingleton.Instance;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var connected = (bool)value;
        if (connected)
            return Images.IconConnected;
        return Images.IconDisconnected;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}