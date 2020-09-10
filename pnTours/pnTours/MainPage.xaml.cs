using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Xamarin.Forms.Markup;
using static pnTours.App;

namespace pnTours
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BindingContext = tour;
            
            //Invoke request for location permision earlier
            _ = tour.route.GetCurrentLocation();

            //make map inside scrollview work like a user expects
            if (Device.RuntimePlatform == Device.iOS)
            {
                scrollView.InputTransparent = false;
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                scrollView.InputTransparent = true;
            }

            //Set height of map to 70% of screen.
            mapGridBox.Height = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) / 1.7;

            //Set height of picture to create 4:3 aspect ratio with fill width of screen. Minimises cropping with "aspect fill" display property. 
            tourPicture.HeightRequest = ((DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density) / 4) * 3;

            //load the language picker up with available langauges. Could be used to load differnt tours instead, depending on whats needed.
            foreach (var language in tour.availableLanguages.OrderBy(x => x.number))
            {
                languagePicker.Items.Add(language.name);
            }
            //Show the loaded language as the selected langage in the pickedr.
            languagePicker.SelectedItem = tour.availableLanguages.Find(x => x.number == Preferences.Get("language", 0)).name;

            //Move map, add pins, add routeline
            MoveMap();

            //The main page shows a pin for each stop and draws a line connecting them in order. 
            //It does not show current position on map or distance to the stop like the stop detail page does.
            //This is just a UI decision. This way even if someone is using the app from far away,
            //they can get an idea of how much time to plan and an overview where the tour goes. 
            foreach (stop stop in tour.stops)
            {
                //The "-1" is becuase the XAML is 1 based to make it more human-natural, 
                //but arrays made by deserialization are zero based, becuase it's more computer natural.
                AddPinToMap(stop.stopNumber - 1);
            }
            AddPolylineToMap();

            //Add the stop buttons to the gridview
            AddButtonsToGrid(tour.stops.Count);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            tour.narationPlayer.Load(tour.tourNaration);
            play.Text = "Play (" + tour.narationPlayer.timeElapsedString + " / " + tour.narationPlayer.lengthString + ") ";
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (tour.narationPlayer.isPlaying)
            {
                tour.narationPlayer.PlayOrPause();
            }
        }

        private void AddButtonsToGrid(int thisManyCells, int thisManyCols = 2)
        {
            int thisManyRows = (int)Math.Ceiling((double)thisManyCells / (double)thisManyCols);
            int i = 0;
            for (int r = 0; r < thisManyRows; r++)
            {
                for (int c = 0; c < thisManyCols; c++)
                {
                    if (i < thisManyCells)
                    {
                        stopsGrid.Children.Add(CreateImageForButton(i), c, r);
                        //stopsGrid.Children.Add(CreateLabelForButton(i), c, r);
                        stopsGrid.Children.Add(CreateStopButton(i), c, r);

                        i++;
                    }
                }
            }
        }

        private Label CreateLabelForButton(int stopNumb)
        {
            return new Label { Text = tour.stops[stopNumb].stopNumber.ToString() + ". " + tour.stops[stopNumb].stopName, BackgroundColor = Color.Red, HeightRequest = 40 };
        }

        private Image CreateImageForButton(int stopNumb)
        {
            Image addMe = new Image { 
                Source = tour.stops[stopNumb].stopImageSource,
                Aspect = Aspect.AspectFill
            };
            return addMe;
        }

        private Button CreateStopButton(int stopNumb)
        {
            //Create a button to show stop detail, add button to UI
            Button addMe = new Button
            {
                Text = tour.stops[stopNumb].stopNumber.ToString() + ". " + tour.stops[stopNumb].stopName,
                CommandParameter = stopNumb,
                HeightRequest = 75,
                BorderColor = Color.Black,
                BorderWidth = 1,
                BackgroundColor = Color.FromRgba(175, 225, 225, 0.7),
                //Opacity = .2,
                TextColor = Color.Black,
                FontAttributes = FontAttributes.Bold
                
            };
            addMe.Clicked += stopDetail_Clicked;
            return addMe;
        }

        private void MoveMap()
        {
            List<Position> positions = new List<Position>();

            foreach (stop stop in tour.stops)
            {
                positions.Add(new Position(stop.stopLat, stop.stopLng));
            }

            //Midpoint of all stops is calculated for centering the map 
            Position mid = tour.route.calculateMidPoint(positions);

            //The stop with the maxmimum distance from the midpoint 
            //is used to determine how much to set the intial map zoom
            double dist = 0;
            double maxDist = 0;
            foreach (Position pos in positions)
            {
                dist = Xamarin.Forms.Maps.Distance.BetweenPositions(pos, mid).Meters;
                maxDist = (dist > maxDist ? dist : maxDist);
            }

            //And finally, we actually move the map.
            map.MoveToRegion(MapSpan.FromCenterAndRadius(mid, Xamarin.Forms.Maps.Distance.FromMeters((maxDist < 100 ? 100 : maxDist * 1.25))));
        }

        protected async void AddPolylineToMap()
        {
            //Get route from google
            await tour.route.overviewRoute.SetGoogleRoute(tour.stops);

            //Remove old route lines, if any.
            foreach (MapElement element in map.MapElements.ToList())
            {
                if (element.GetType() == typeof(Xamarin.Forms.Maps.Polyline))
                {
                    map.MapElements.Remove(element);
                }
            }

            //Put new line on map
            if(tour.route.overviewRoute.polylinePointsList != null)
                map.MapElements.Add(tour.route.MakePolyline(tour.route.overviewRoute.polylinePointsList));
        }

        private void AddPinToMap(int stopNumb)
        {
            //Create a position from lat/lng, create a pin from position, add pin to map
            Position stopPosition = new Position(tour.stops[stopNumb].stopLat, tour.stops[stopNumb].stopLng);
            Pin stopPin = new Pin { Label = tour.stops[stopNumb].stopName, Position = stopPosition };
            map.Pins.Add(stopPin);
        }

        private void stopDetail_Clicked(object sender, EventArgs e)
        {
            //Push stop detail page to top, passing it the stop number to show the detail of.           
            Navigation.PushAsync(new StopDetail((int)((Button)sender).CommandParameter));            
        }

        private void UpdatePlayerUI()
        {
            //simpleAudioPlayer doesn't support binding, so this keeps the UI updated.
            //It's very lightwieght, so it doesn't cause a problem with responsiveness.
            if (!tour.narationPlayer.isPlaying)
                play.Text = "Play (" + tour.narationPlayer.timeElapsedString + " / " + tour.narationPlayer.lengthString + ")";
            else
            {
                Device.StartTimer(TimeSpan.FromSeconds(1), () =>
                {
                    if (tour.narationPlayer.isPlaying)
                    {
                        play.Text = "Pause (" + tour.narationPlayer.timeElapsedString + " / " + tour.narationPlayer.lengthString + ")";
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
            if (!tour.narationPlayer.isPlaying)
                tour.narationPlayer.Stop();
            else
                tour.narationPlayer.timeElapsedInSeconds = 0;

            UpdatePlayerUI();
        }

        private void languagePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            //When a new language/tour is selected from eeh picker, a new "tour" object is made
            //and the main page is recreated. This is all a bit 'expensive', but it's not something
            //a user should be doing very often.
            if (tour.narationPlayer.isPlaying)
            {
                tour.narationPlayer.PlayOrPause();
            }

            int selectedLanguageNumber = tour.availableLanguages.Find(x => x.name == languagePicker.SelectedItem.ToString()).number;

            if (selectedLanguageNumber != Preferences.Get("language", 0))
            {
                Preferences.Set("language", selectedLanguageNumber);
                tour = new pnTour(selectedLanguageNumber);
                (Application.Current).MainPage = new NavigationPage(new MainPage());
            }
        }

        private async void openInMapsButton_Clicked(object sender, EventArgs e)
        {
            string endLat = tour.stops.Last().stopLat.ToString();
            string endlng = tour.stops.Last().stopLng.ToString();
            string waypoints = "&waypoints=";

            if (tour.stops.Count > 1)
            {
                foreach (stop stop in tour.stops)
                {
                    if (stop != tour.stops.Last())
                        //trailing pipe doesn't matter
                        waypoints += stop.stopLat.ToString() + "," + stop.stopLng.ToString() + "|";
                }
            }

            if (Device.RuntimePlatform == Device.iOS)
            {
                //Can't do waypoints with apple maps, so just go to first point.
                await Launcher.OpenAsync("http://maps.apple.com/?daddr=" + tour.stops[0].stopLat + "," + tour.stops[0].stopLng);
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                //if waypoints paramter is empty, it doesnt matter
                await Launcher.OpenAsync("https://www.google.com/maps/dir/?api=1&destination=" + endLat + "," + endlng + waypoints);
            }
        }
    }
}
