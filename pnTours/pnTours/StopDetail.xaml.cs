using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Xamarin.Forms.Xaml;
using Xamarin.Essentials;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Markup;
using static pnTours.App;
using Position = Xamarin.Forms.Maps.Position;

namespace pnTours
{
    //This page is used for all stops, just with differnt information taken from the XML.

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class StopDetail : ContentPage
    {
        private int stopNumber = 0;        

        public StopDetail(int stop = 0)
        {
            InitializeComponent();
            //Asks the user for permision when invoked
            _= tour.route.GetCurrentLocation();

            //Which stop's info is displayed is determined by passed in paramter.
            stopNumber = stop;
            
            //Set some binding contexts ...
            BindingContext = tour;
            stopNameLabel.BindingContext = tour.stops[stopNumber];
            stopDesc.BindingContext = tour.stops[stopNumber];
            stopPicture.BindingContext = tour.stops[stopNumber];

            //Make map inside scrollview work like a user expects.
            if (Device.RuntimePlatform == Device.iOS)
            {
                scrollView.InputTransparent = false;
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                scrollView.InputTransparent = true;
            }

            //Set map height to half height of screen
            mapGridBox.Height = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) / 2;
            
            //Setting the center/zoom on the stop makes the map usefull while waiting for location and route. 
            //In practice it usually goes by so fast it's not even noticable ... unless the user denied the location permision.
            //In that case, this is nice.
            map.MoveToRegion(MapSpan.FromCenterAndRadius((new Position(tour.stops[stopNumber].stopLat, tour.stops[stopNumber].stopLng)), Xamarin.Forms.Maps.Distance.FromMiles(1)));

            //We call it once regadless of if location has changed, so it always draws the route to the stop
            // even if the user hasn't moved.
            _= KeepUpdated(true);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            //Get "route"things ready
            await tour.route.SetStartedAsWalk(tour.stops[stopNumber]);

            //Load the correct naration into the player
            tour.narationPlayer.Load(tour.stops[stopNumber].stopNaration);
            play.Text = "Play (" + tour.narationPlayer.timeElapsedString + " / " + tour.narationPlayer.lengthString + ")";

            //Stick a pin in it
            AddPinToMap(stopNumber);

            //Start the location listener, minDist paramter is in meters.
            await tour.route.StartListening(10);

            //But after that, we only update if they move. 
            MessagingCenter.Subscribe<route>(this, "newPosition", async (sender) =>
            {
                await KeepUpdated();
            });
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            //Stop playing the naration
            if (tour.narationPlayer.isPlaying)
            {
                tour.narationPlayer.PlayOrPause();
            }
            //Preferences.Set("volume", tour.narationPlayer.volume);

            await tour.route.StopListening();
        }

        protected async Task KeepUpdated(bool skipCheck = false)
        {
            //Set our new distance to stop
            await tour.route.SetThisFar(tour.stops[stopNumber]);
            
            //This function also removes the old pin for us.
            AddPinForCurrentLocation("Thou ist here");
            
            //Movemap calculates midpoint and zoom too.
            moveMap(stopNumber);
            
            //Testing with real devices showed this would be invooked many times as GPS position got more locked in. Often several times in a second.
            //So this rate limits calls
            if ( Math.Abs( (DateTimeOffset.Now.UtcDateTime - tour.route.googleHelper.timestamp.UtcDateTime).Seconds ) > 10 | skipCheck)
            {
                //Get the new route from google
                await tour.route.googleHelper.SetGoogleRoute(tour.route.currentLocation.Latitude.ToString() + "," + tour.route.currentLocation.Longitude.ToString(), tour.stops[stopNumber].stopLat.ToString() + "," + tour.stops[stopNumber].stopLng.ToString(), tour.route.DriveOrWalk());
            
                //Draw the route on the map. Funtion also removes all old polylines.
                AddPolylineToMap(tour.route.MakePolyline(tour.route.googleHelper.polylinePointsList));
            }
        }

        protected void AddPolylineToMap(Xamarin.Forms.Maps.Polyline routeLine)
        {
            //remove old route lines
            foreach (MapElement element in map.MapElements.ToList())
            {
                if (element.GetType() == typeof(Xamarin.Forms.Maps.Polyline))
                {
                    map.MapElements.Remove(element);
                }
            }

            //put new line on map
            map.MapElements.Add(routeLine);
        }

        private void AddPinToMap(int stopNumb = 0)
        {
            Position stopPosition = new Position(App.tour.stops[stopNumb].stopLat, App.tour.stops[stopNumb].stopLng);
            Pin stopPin = new Pin { Label = App.tour.stops[stopNumb].stopName, Position = stopPosition };

            map.Pins.Add(stopPin);
        }

        protected void AddPinForCurrentLocation(string pinLabel)
        {
            //Remove current location pin if it exists, do nothing if this fails.
            try
            {
                map.Pins.Remove(map.Pins.First(x => x.Label.Equals(pinLabel)));
            }
            catch { }
            //Whether or not there was a pin to remove, add a new one.
            finally
            {
                if (App.tour.route.currentLocation != null)
                {
                    Pin currentPin = new Pin { Label = pinLabel, Position = new Position(App.tour.route.currentLocation.Latitude, App.tour.route.currentLocation.Longitude) };
                    map.Pins.Add(currentPin);
                }
            }
        }

        protected void moveMap(int stopNumb)
        {
            Position stopPosition = new Position(App.tour.stops[stopNumb].stopLat, App.tour.stops[stopNumb].stopLng);

            //Check we actually have a current location ...
            if (App.tour.route.currentLocation.Latitude != 0 && App.tour.route.currentLocation.Longitude != 0)
            {
                Position currentPosition = new Position(App.tour.route.currentLocation.Latitude, App.tour.route.currentLocation.Longitude);

                //... if we do, center map on midpoint and zoom appropriately
                map.MoveToRegion(MapSpan.FromCenterAndRadius(App.tour.route.calculateMidPoint(new List<Position>() { currentPosition, stopPosition }), Xamarin.Forms.Maps.Distance.FromMiles(App.tour.route.thisFar.Miles / 1.5)));
            }
            else
            {
                //..if we don't, just center on the stop.
                map.MoveToRegion(MapSpan.FromCenterAndRadius(stopPosition, Xamarin.Forms.Maps.Distance.FromMiles(2)));
            }
        }

        private void UpdatePlayerUI()
        {
            //SimpleAudioPlayer doesn't support binding, so this is an easy way to keep the UI updated.
            //It's very lightwieght, so it doesn't cause a problem with responsiveness.
            if (!App.tour.narationPlayer.isPlaying)
                play.Text = "Play (" + App.tour.narationPlayer.timeElapsedString + " / " + App.tour.narationPlayer.lengthString + ")";
            else
            {
                Device.StartTimer(TimeSpan.FromSeconds(1), () =>
                {
                    if (App.tour.narationPlayer.isPlaying)
                    {
                        play.Text = "Pause (" + App.tour.narationPlayer.timeElapsedString + " / " + App.tour.narationPlayer.lengthString + ")";
                        return true;
                    }
                    else
                        return false;
                });
            }
        }

        private void PlayPauseButton_Clicked(object sender, EventArgs e)
        {
            tour.narationPlayer.PlayOrPause();
            UpdatePlayerUI();
        }

        private void back10_Clicked(object sender, EventArgs e)
        {
            tour.narationPlayer.SkipBackward();
            UpdatePlayerUI();
        }

        private void forward10_Clicked(object sender, EventArgs e)
        {
            tour.narationPlayer.SkipForward();
            UpdatePlayerUI();
        }

        private void restart_Clicked(object sender, EventArgs e)
        {
            if (!App.tour.narationPlayer.isPlaying)
                tour.narationPlayer.Stop();
            else
                tour.narationPlayer.timeElapsedInSeconds = 0;
            UpdatePlayerUI();
        }

        private async void openInMapsButton_Clicked(object sender, EventArgs e)
        {
            string endLat = tour.stops[stopNumber].stopLat.ToString();
            string endlng = tour.stops[stopNumber].stopLng.ToString();

            if (Device.RuntimePlatform == Device.iOS)
            {
                await Launcher.OpenAsync("http://maps.apple.com/?daddr=" + endLat + "," + endlng);
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                await Launcher.OpenAsync("https://maps.google.com/?daddr=" + endLat + "," + endlng);
            }
        }

        private void previousStopButton_Clicked(object sender, EventArgs e)
        {            
            Navigation.InsertPageBefore(new StopDetail((stopNumber + App.tour.stops.Count - 1) % App.tour.stops.Count),this);
            Navigation.PopAsync();
        }

        private void homeButton_Clicked(object sender, EventArgs e)
        {         
            Navigation.PopAsync();            
        }

        private void nextStopButton_Clicked(object sender, EventArgs e)
        {
            Navigation.InsertPageBefore(new StopDetail((stopNumber + 1) % tour.stops.Count), this);
            Navigation.PopAsync();
        }
    }
}