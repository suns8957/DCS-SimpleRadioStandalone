using Android.Runtime;
using Java.Lang;
using Microsoft.Maui;

namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
        try
        {
            //MUST BE LOADED IN MAIN THREAD
            JavaSystem.LoadLibrary("opus");
            JavaSystem.LoadLibrary("speexdsp");
        }
        catch (UnsatisfiedLinkError ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}