using System.Collections.Generic;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.RadioChannels;

public interface IPresetChannelsStore
{
    IEnumerable<PresetChannel> LoadFromStore(string radioName);
}