using Plugin.SimpleAudioPlayer;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xamarin.Essentials;
using System.ComponentModel;

namespace pnTours
{
    public class naration : INotifyPropertyChanged
    {
        ISimpleAudioPlayer player = CrossSimpleAudioPlayer.CreateSimpleAudioPlayer();

        public double timeElapsedInSeconds { get { return player.CurrentPosition; } set { player.Seek(value); } }

        public double lengthInSeconds { get { return player.Duration; } }

        public string timeElapsedString { get { return convertSecondsToString(timeElapsedInSeconds); } }

        public string lengthString { get { return convertSecondsToString(lengthInSeconds); } }

        public bool isPlaying { get { return player.IsPlaying; } }

        private double _volume { get; set; }

        public double volume
        {
            get { return _volume; }
            set
            {
                _volume = value;
                player.Volume = value;
                Preferences.Set("volume", value);
                NotifyPropertyChanged("volume");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public naration()
        {
            volume = Preferences.Get("volume", 0.5);
        }

        public void PlayOrPause()
        {
            if (player.IsPlaying)
                player.Pause();
            else
                player.Play();
        }

        public void Stop()
        {
            player.Stop();
        }

        public void SkipForward(double seconds = 10)
        {
            this.player.Seek(this.player.CurrentPosition + seconds);
        }

        public void SkipBackward(double seconds = 10)
        {
            this.player.Seek(this.player.CurrentPosition - seconds);
        }

        public void Load(string resourceName)
        {
            var assembly = typeof(App).GetTypeInfo().Assembly;
            var list = assembly.GetManifestResourceNames();

            foreach (var item in list)
            {
                if (item.Contains(resourceName) && item.Contains(".naration.") && item.EndsWith(".mp3"))
                {

                    this.player.Load(assembly.GetManifestResourceStream(item));
                }
            }
        }

        private string convertSecondsToString(double rawSeconds)
        {
            string returnMe = string.Empty;
            int seconds = Convert.ToInt32(rawSeconds % 60);
            int minutes = Convert.ToInt32((rawSeconds - seconds) / 60);
            int hours = Convert.ToInt32(((rawSeconds - seconds) - minutes) / 3600);

            if (hours == 0)
            {
                returnMe = minutes.ToString() + ":" + seconds.ToString("D2");
            }
            else
            {
                returnMe = hours.ToString() + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D4");
            }

            return returnMe;
        }
    }
}