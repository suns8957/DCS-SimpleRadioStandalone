using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile.Models.RadioChannels;

public class PresetChannel : PropertyChangedBaseClass
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