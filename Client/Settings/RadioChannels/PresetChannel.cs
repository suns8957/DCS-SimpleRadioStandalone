using Ciribob.SRS.Common.Helpers;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;

public class PresetChannel : PropertyChangedBase
{
    public string Text { get; set; }

    //will be a double of the frequency
    public object Value { get; set; }
    public int Channel { get; set; }

    public override string ToString()
    {
        return Text;
    }
}