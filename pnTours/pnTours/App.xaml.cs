using Plugin.SimpleAudioPlayer;
using System;
using System.Reflection;
using System.Resources;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace pnTours
{
    public partial class App : Application
    {
        public static pnTour tour { get; set; }

        public App()
        {
            InitializeComponent();

            if (!Preferences.ContainsKey("guid"))
                Preferences.Set("guid", Guid.NewGuid().ToString());

            if (!Preferences.ContainsKey("volume"))
                Preferences.Set("volume", 0.5);             

            if (!Preferences.ContainsKey("language"))            
                Preferences.Set("language", 0);            
            
            tour = new pnTour(Preferences.Get("language", 0));         

            MainPage = new NavigationPage(new MainPage( ));
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
