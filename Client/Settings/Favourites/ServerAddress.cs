using Ciribob.SRS.Common.Helpers;

namespace Ciribob.FS3D.SimpleRadio.Standalone.Client.Settings.Favourites;

public class ServerAddress : PropertyChangedBase
{
    private string _address;

    private bool _isDefault;

    private string _name;

    public ServerAddress(string name, string address, bool isDefault = false)
    {
        // Set private values directly so we don't trigger useless re-saving of favourites list when being loaded for the first time
        _name = name;
        _address = address;
        IsDefault = isDefault; // Explicitly use property setter here since IsDefault change includes additional logic
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                NotifyPropertyChanged();
            }
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (_address != value)
            {
                _address = value;
                NotifyPropertyChanged();
            }
        }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            _isDefault = value;
            NotifyPropertyChanged();
        }
    }
}