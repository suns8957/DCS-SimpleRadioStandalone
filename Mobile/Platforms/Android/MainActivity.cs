// using Android.App;
// using Android.Content.PM;
// using Android.Views;
// using Ciribob.DCS.SimpleRadio.Standalone.Common.Network.Singletons;
// using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
// using Ciribob.DCS.SimpleRadio.Standalone.Mobile.Utility;
//
// namespace Ciribob.DCS.SimpleRadio.Standalone.Mobile;
//
// [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
//     ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
//                            ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
//     LaunchMode = LaunchMode.SingleTask)]
// public class MainActivity : MauiAppCompatActivity
// {
//     public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
//     {
//         switch (keyCode)
//         {
//             case Keycode.VolumeUp:
//               
//                 EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = true });
//                 return true;
//             
//
//                 break;
//         }
//
//         return base.OnKeyDown(keyCode, e);
//     }
//
//     public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
//     {
//         switch (keyCode)
//         {
//             case Keycode.VolumeUp:
//
//                 if (GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys
//                         .VolumeUpAsPTT))
//                 {
//                     EventBus.Instance.PublishOnBackgroundThreadAsync(new PTTState { PTTPressed = false });
//                     return true;
//                 }
//
//                 break;
//         }
//
//         return base.OnKeyUp(keyCode, e);
//     }
// }