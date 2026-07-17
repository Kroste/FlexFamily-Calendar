using Android.App;
using Android.Content;
using Android.OS;

namespace FlexFamilyCalendar.Android;

[Activity(
    Theme = "@style/MyTheme.Splash",
    NoHistory = true)]
public class SplashActivity : Activity
{
    protected override void OnResume()
    {
        base.OnResume();
        StartActivity(new Intent(this, typeof(MainActivity)));
    }
}
