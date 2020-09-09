using Java.Nio.Channels;
using Plugin.Geolocator;
using Plugin.Geolocator.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Maps;
using Color = Xamarin.Forms.Color;
using Distance = Xamarin.Forms.Maps.Distance;
using Polyline = Xamarin.Forms.Maps.Polyline;

namespace pnTours
{
    public class route
    {
        public Location currentLocation { get; set; }

        public googleHelper googleHelper { get; set; }

        public googleHelper overviewRoute { get; set; }

        public bool startedAsWalk { get; set; }

        public Xamarin.Forms.Maps.Distance thisFar { get; set; }

        public IGeolocator locator { get; set; }

        public route()
        {
            currentLocation = new Location();
            googleHelper = new googleHelper();
            overviewRoute = new googleHelper();
            locator = CrossGeolocator.Current;
            locator.PositionChanged += PositionChanged;
        }

        //Event handler for locator        
        public void PositionChanged(object sender, PositionEventArgs e)
        {
            var position = e.Position;

            //Sometimes the event is fired twice. This checks that the current location really changed.
            if (e.Position.Latitude != currentLocation.Latitude && e.Position.Longitude != currentLocation.Longitude)
            {
                currentLocation.Latitude = e.Position.Latitude;
                currentLocation.Longitude = e.Position.Longitude;
                MessagingCenter.Send(this, "newPosition");
            }
        }

        //Locator methods
        public async Task StopListening()
        {
            if (locator.IsListening)
                await locator.StopListeningAsync();
        }
        public async Task StartListening(int minDist)
        {
            var locationInUse = Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            var locationAlways = Permissions.CheckStatusAsync<Permissions.LocationAlways>();

            try
            {
                if (locator.IsGeolocationAvailable & locator.IsGeolocationEnabled
                     & (locationInUse.Result.Equals(PermissionStatus.Granted) | locationAlways.Result.Equals(PermissionStatus.Granted)))
                    await locator.StartListeningAsync(TimeSpan.FromSeconds(5), minDist);
            }
            finally { }
        }

        //If they start within 2km, it started as a walk.
        //Why 2km? Becuase I said so. If this were a widely used app,
        //it would be worth doing customer research about how far away they're willing
        //to walk to a location. But it's not, so I just used my preference. 
        public async Task<bool> SetStartedAsWalk(stop stop)
        {
            await SetThisFar(stop);

            return thisFar.Meters < 2000 ? true : false;
        }

        //If they started close enough to walk, it returns walking directions.
        //If they started as a walk, but get more than 5km's away, it changes to driving directions.
        //If they started far enough away to be a drive, it changes to walking when they get within 1000 meters
        //Eventually, you have to get out of a car to actualy get to any of these spots, so switching the route
        // to walking, at some point, makes sense. Why 1000 meters? Becuase. 
        public string DriveOrWalk()
        {
            string returnMe = "driving";
            if (startedAsWalk)
            {
                if (thisFar.Meters < 5000)
                {
                    returnMe = "walking";
                }
                else
                    returnMe = "driving";
            }
            else
            {
                if (thisFar.Meters < 1000)
                {
                    returnMe = "walking";
                }
                else
                    returnMe = "driving";
            }

            return returnMe;
        }

        //This is an "as the crow flys" distance. Used to determine if google should return driving or walking directions.
        //Also used to set the zoom of the map.
        //It would make sense to base that decision on the google-route distance instead, but that would result in more
        // google-api calls. 
        public async Task SetThisFar(stop stop)
        {
            var locationInUse = Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            var locationAlways = Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            //locationAlways.
            try
            {
                if (locator.IsGeolocationAvailable & locator.IsGeolocationEnabled
                    & (locationInUse.Result.Equals(PermissionStatus.Granted) | locationAlways.Result.Equals(PermissionStatus.Granted)))
                {
                    Plugin.Geolocator.Abstractions.Position lastLocationGot = await locator.GetLastKnownLocationAsync();
                    if (lastLocationGot != null)
                    {
                        //If it's more than 60 seconds old, we'll go ahead and get a new one. Otherwise it's fine.             
                        if ((DateTimeOffset.Now.UtcDateTime - lastLocationGot.Timestamp.UtcDateTime).Seconds > 60)
                        {
                            await GetCurrentLocation();
                        }
                        else
                        {
                            currentLocation.Latitude = lastLocationGot.Latitude;
                            currentLocation.Longitude = lastLocationGot.Longitude;
                        }
                    }
                }
            }
            finally
            {
                if (currentLocation != null)
                {
                    thisFar = Xamarin.Forms.Maps.Distance.BetweenPositions(new Xamarin.Forms.Maps.Position(currentLocation.Latitude, currentLocation.Longitude), new Xamarin.Forms.Maps.Position(stop.stopLat, stop.stopLng));
                }
                else
                    thisFar = Xamarin.Forms.Maps.Distance.FromMeters(0);
            }
        }

        public async Task GetCurrentLocation()
        {
            try
            {
                Location location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best));

                if (location != null)
                {
                    currentLocation.Latitude = location.Latitude;
                    currentLocation.Longitude = location.Longitude;
                }
            }
            //Look, the OS asks the user. If they so no, I'm not going to do rub it in thier face.
            //Same if it just messes up - it's not big deal, just keep going.
            //These catches are usefull for debugging, but in production, they're more likely to to just cuause anoyoance. 
            catch (FeatureNotSupportedException fnsEx)
            {
                // Handle not supported on device exception
            }
            catch (FeatureNotEnabledException fneEx)
            {
                // Handle not enabled on device exception
            }
            catch (PermissionException pEx)
            {
                // Handle permission exception
            }
            catch (Exception ex)
            {
                // Unable to get location
            }
        }

        public Xamarin.Forms.Maps.Polyline MakePolyline(List<Xamarin.Forms.Maps.Position> polylinePoints)
        {
            int maxPoints = 5000;

            //Make new route line
            Xamarin.Forms.Maps.Polyline routeLine = new Xamarin.Forms.Maps.Polyline
            {
                StrokeColor = Color.Blue,
                StrokeWidth = 8
            };

            //shape the route line
            foreach (Xamarin.Forms.Maps.Position routePoint in polylinePoints)
            {
                routeLine.Geopath.Add(routePoint);
            }

            //Polylines with too many points (I saw problems starting at about 40,000 points ... routes longer than about 1500 miles) causes
            // notable responsiveness problems when added to the map. Removing points makes the line "snap" on roads less accurately,             
            // but the line will get provgressively get tighter as the route gets shorter. Again ... only an isue on very long/complex routes.
            //Infact, this works so well I moved the max points much lower than where I saw problems with responiveness, just to help low end devices with long routes.
            if (routeLine.Geopath.Count > maxPoints)
            {
                //It's possible to remove points from the whole line, and still be too big.
                //In that case, we loop again and remove more again.
                while (routeLine.Geopath.Count > maxPoints)
                {
                    int mid = routeLine.Geopath.Count / 2;
                    int backward = mid - 2;

                    //Remove every-other point, starting at middle, working out in both directions, leaving first 30% and last 30% untouched.
                    //Should result in least notcable change in line accuracy to user becuase
                    // it will just get less "snappy to roads" in the middle, and progressively more "snapp" towards the start/end points.
                    while (routeLine.Geopath.Count > maxPoints & backward > routeLine.Geopath.Count * .3)
                    {
                        routeLine.Geopath.RemoveAt(mid);
                        routeLine.Geopath.RemoveAt(backward);
                        backward -= 2;
                    }
                }
            }

            return routeLine;
        }

        //Reutnrs a geographic midpoint from a list of positions. 
        //Accounts for curavture of the earth (assuming earth is round), becuase ... why not? 
        //I followed a good guide I found online for the calcs, so it wasn't much harder than not acounting for that.
        //For this app, a simple averaging of lats/lngs would have been good enough, but this is more fun.
        public Xamarin.Forms.Maps.Position calculateMidPoint(List<Xamarin.Forms.Maps.Position> positions)
        {
            double x = 0;
            double y = 0;
            double z = 0;

            foreach (Xamarin.Forms.Maps.Position thisPosition in positions)
            {
                x += Math.Cos(thisPosition.Latitude.ToRadians()) * Math.Cos(thisPosition.Longitude.ToRadians());
                y += Math.Cos(thisPosition.Latitude.ToRadians()) * Math.Sin(thisPosition.Longitude.ToRadians());
                z += Math.Sin(thisPosition.Latitude.ToRadians());
            }

            x /= positions.Count;
            y /= positions.Count;
            z /= positions.Count;

            double lng = Math.Atan2(y, x);
            double hyp = Math.Sqrt(x * x + y * y);
            double lat = Math.Atan2(z, hyp);

            lng *= (180 / Math.PI);
            lat *= (180 / Math.PI);

            return new Xamarin.Forms.Maps.Position(lat, lng);
        }
    }
}
