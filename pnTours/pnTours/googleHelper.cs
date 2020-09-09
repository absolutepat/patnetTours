using Java.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms.Maps;

namespace pnTours
{
    
    public class googleHelper : INotifyPropertyChanged
    {
        public GoogleDirections googleRoute { get; set; }
        
        public DateTimeOffset timestamp { get; set; }

        public List<Position> polylinePointsList { get { return detailedPolyline(googleRoute); } set { } }
        
        private HttpClient client { get; set; }

        private string _distance { get; set; }

        public string distance
        {
            get { return _distance; }
            set
            {
                _distance = value;
                NotifyPropertyChanged("distance");
            }
        }

        private string _duration { get; set; }
        public string duration
        {
            get { return _duration; }
            set
            {
                _duration = value;
                NotifyPropertyChanged("duration");
            }
        }

        private string _mode { get; set; }
        public string mode
        {
            get { return _mode; }
            set
            {
                _mode = value;
                NotifyPropertyChanged("mode");
            }
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public googleHelper()
        {
            googleRoute = new GoogleDirections();
            polylinePointsList = new List<Position>();
            distance = string.Empty;
            duration = string.Empty;
            client = new HttpClient();
        }

        //By calling to our own API instead of to google directly, we gain several advantages. First, we hide our API key to prevent theft. 
        //Second, we can capture user data if we wanted to do something with it. Say, we see one user is making a ridiculous number of calls, we can throttle (or block) that user.
        //Or if we wanted to add advertisments, we could do it based on location gathered from start pos, or for things near endpos. Ect.
        //All the api does for now is return the route without any of that kind of logic, but by having the api in place, that sort of stuff can be added later.
        public async Task SetGoogleRoute(string startPos, string endPos, string mode = "walking")
        {
            googleRoute = new GoogleDirections();

            timestamp = DateTimeOffset.Now.UtcDateTime;
                        
            //--Inside a try, becuase it was chrashing if it coudln't get to internet to resolve hostname
            //  Which happens more than you'd think in the wild ... phone connects to wifi with a captive portal or loses signal for a moment, ect.
            //  It doesn't matter if this fails, we just don't get to draw a pretty route line on the map.
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://api.patnet.net/?origin=" + startPos + "&destination=" + endPos + "&mode=" + mode + "&guid=" + Preferences.Get("guid", "guid_unset"));

                if (response.IsSuccessStatusCode)
                {
                    // Get the response
                    var jsonString = response.Content.ReadAsStringAsync().Result;

                    // Deserialise the data (include the Newtonsoft JSON Nuget package if you don't already have it)
                    googleRoute = JsonConvert.DeserializeObject<GoogleDirections>(jsonString);

                    //ZERO_RESULTS happens when it can't find a route, like if someon needs to drive across the ocean.
                    if (googleRoute.status != "ZERO_RESULTS")
                    {
                        duration = googleRoute.routes.FirstOrDefault().legs.FirstOrDefault().duration.text ?? "n/a";
                        distance = googleRoute.routes.FirstOrDefault().legs.FirstOrDefault().distance.text ?? "n/a";
                        this.mode = mode;                        
                    }
                    else
                    {
                        duration = "n/a";
                        distance = "n/a";
                        this.mode = "n/a";
                    }
                }
            }
            catch {
                duration = "n/a";
                distance = "n/a";
                this.mode = "n/a";
            }

            client.CancelPendingRequests();
        }

        //Overload. Makes route between all the stops and back to first stop, for the main mage.
        public async Task SetGoogleRoute(List<stop> stops)
        {            
            if (stops.Count > 2)
            {
                //duration = "n/a";
                //distance = "n/a";
                //mode = "n/a";

                //GoogleDirections _
                googleRoute = new GoogleDirections();
               // HttpClient client = new HttpClient();
                string firstPoint = stops[0].stopLat.ToString() + "," + stops[0].stopLng.ToString();
                //string lastPoint = stops.Last().stopLat.ToString() + "," + stops.Last().stopLng.ToString();
                string waypoints = "&waypoints=";

                foreach(stop stop in stops.Skip(1))
                {
                    waypoints += "via:" + stop.stopLat.ToString() + "," + stop.stopLng.ToString() + "|";
                }

                //--Inside a try, becuase it was chrashing if it coudln't get to internet to resolve hostname.
                //  Which happens more than you'd think in the wild ... phone connects to wifi with a captive portal or loses signal for a moment, ect.
                //  It doesn't matter if this fails, we just don't get to draw a pretty route line on the map.
                try
                {
                    HttpResponseMessage response = await client.GetAsync("https://api.patnet.net/?origin=" + firstPoint + "&destination=" + firstPoint + waypoints.Trim('|') + "&mode=walking");

                    if (response.IsSuccessStatusCode)
                    {
                        // Get the response
                        var jsonString = response.Content.ReadAsStringAsync().Result;

                        // Deserialise the data (include the Newtonsoft JSON Nuget package if you don't already have it)
                        googleRoute = JsonConvert.DeserializeObject<GoogleDirections>(jsonString);

                        //ZERO_RESULTS happens when it can't find a route, like if someon needs to drive across the ocean.
                        if (googleRoute.status != "ZERO_RESULTS")
                        {
                            duration = googleRoute.routes.FirstOrDefault().legs.FirstOrDefault().duration.text ?? "n/a";
                            distance = googleRoute.routes.FirstOrDefault().legs.FirstOrDefault().distance.text ?? "n/a";
                            mode = "walking";
                        }
                    }
                }
                catch{ }                
            }

            //If 2 points or less, we can use the other "set google route"
            else if (stops.Count == 2)
            {
                await SetGoogleRoute(stops[0].stopLat.ToString() + "," + stops[0].stopLng.ToString(), stops[1].stopLat.ToString() + "," + stops[1].stopLng.ToString());
            }

            else if (stops.Count == 1)
            {
                await SetGoogleRoute(stops[0].stopLat.ToString() + "," + stops[0].stopLng.ToString(), stops[0].stopLat.ToString() + "," + stops[0].stopLng.ToString());
            }

            //client.CancelPendingRequests();
        }

        //To get a detailed line that lays on top of roads accurately, we need to get the enocded line points from each "step" that google returns.
        //This combines all those into one decoded line
        public List<Position> detailedPolyline(GoogleDirections fullRoute)
        {
            List<Position> returnMe = new List<Position>();

            if (fullRoute.status != "ZERO_RESULTS" & fullRoute.routes != null)
            {
                
                foreach (Step step in fullRoute.routes[0].legs[0].steps.ToList())
                {
                    foreach (Position position in FnDecodePolylinePoints(step.polyline.points))
                    {
                        returnMe.Add(position);
                    }
                }
            }



            return returnMe;
        }

        //Decode the google line points
        public List<Position> FnDecodePolylinePoints(string encodedPoints)
        {
            List<Position> returnMe = new List<Position>();
            char[] polylinechars = null;
            int index = 0;
            int currentLat = 0;
            int currentLng = 0;
            int next5bits = 0;
            int sum = 0;
            int shifter = 0;

            if (!string.IsNullOrEmpty(encodedPoints))
            {
                polylinechars = encodedPoints.ToCharArray();

                try
                {
                    while (index < polylinechars.Length)
                    {
                        // calculate next latitude
                        sum = 0;
                        shifter = 0;
                        do
                        {
                            next5bits = (int)polylinechars[index++] - 63;
                            sum |= (next5bits & 31) << shifter;
                            shifter += 5;
                        } while (next5bits >= 32 && index < polylinechars.Length);

                        if (index >= polylinechars.Length)
                            break;

                        currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                        //calculate next longitude
                        sum = 0;
                        shifter = 0;
                        do
                        {
                            next5bits = (int)polylinechars[index++] - 63;
                            sum |= (next5bits & 31) << shifter;
                            shifter += 5;
                        } while (next5bits >= 32 && index < polylinechars.Length);

                        if (index >= polylinechars.Length && next5bits >= 32)
                            break;

                        currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                        returnMe.Add(new Position(Convert.ToDouble(currentLat) / 100000.0, Convert.ToDouble(currentLng) / 100000.0));
                    }
                }
                catch
                {

                }
            }

            return returnMe;
        }
    }

    public class GeocodedWaypoint
    {
        public string geocoder_status { get; set; }
        public string place_id { get; set; }
        public List<string> types { get; set; }
    }

    public class Northeast
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Southwest
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Bounds
    {
        public Northeast northeast { get; set; }
        public Southwest southwest { get; set; }
    }

    public class Distance : INotifyPropertyChanged
    {
        
        private string _text { get; set; }        
        public string text
        {
            get { return _text; }
            set
            {
                _text = value;
                NotifyPropertyChanged("text");
            }
        }
        public int value { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Duration
    {
        public string text { get; set; }
        public int value { get; set; }
    }

    public class EndLocation
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class StartLocation
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Distance2
    {
        public string text { get; set; }
        public int value { get; set; }
    }

    public class Duration2
    {
        public string text { get; set; }
        public int value { get; set; }
    }

    public class EndLocation2
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Polyline
    {
        public string points { get; set; }
    }

    public class StartLocation2
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Step
    {
        public Distance2 distance { get; set; }
        public Duration2 duration { get; set; }
        public EndLocation2 end_location { get; set; }
        public string html_instructions { get; set; }
        public Polyline polyline { get; set; }
        public StartLocation2 start_location { get; set; }
        public string travel_mode { get; set; }
        public string maneuver { get; set; }
    }

    public class Leg
    {
        public Distance distance { get; set; }
        public Duration duration { get; set; }
        public string end_address { get; set; }
        public EndLocation end_location { get; set; }
        public string start_address { get; set; }
        public StartLocation start_location { get; set; }
        public List<Step> steps { get; set; }
        public List<object> via_waypoint { get; set; }
    }

    public class OverviewPolyline
    {
        public string points { get; set; }
    }

    public class Route
    {
        public Bounds bounds { get; set; }
        public string copyrights { get; set; }
        public List<Leg> legs { get; set; }
        public OverviewPolyline overview_polyline { get; set; }
        public string summary { get; set; }
        public List<object> warnings { get; set; }
        public List<object> waypoint_order { get; set; }
    }

    public class GoogleDirections
    {
        public List<GeocodedWaypoint> geocoded_waypoints { get; set; }
        public List<Route> routes { get; set; }
        public string status { get; set; }
    }
}
