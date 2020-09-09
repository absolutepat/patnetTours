//using Android.Media;
using Plugin.SimpleAudioPlayer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Maps;

namespace pnTours
{
    [XmlRoot(ElementName = "pnTour")]
    public class pnTour
    {
        [XmlElement(ElementName = "tourName")]
        public string tourName { get; set; }

        [XmlElement(ElementName = "tourDescription")]
        public string tourDescription { get; set; }

        [XmlElement(ElementName = "tourImage")]
        public string tourImage { get; set; }

        [XmlElement(ElementName = "tourNaration")]
        public string tourNaration { get; set; }

        [XmlElement(ElementName = "stop")]
        public List<stop> stops { get; set; }

        [XmlElement(ElementName = "languageNumber")]
        public int languageNumber { get; set; }

        [XmlElement(ElementName = "languageDisplayName")]
        public string languageDisplayName { get; set; }

        [XmlIgnore]
        public ImageSource tourImageSource { get { return ImageSource.FromResource(returnResourceName(tourImage)); } }

        [XmlIgnore]
        public naration narationPlayer { get; set; }

        [XmlIgnore]
        public List<(int number, string name, string location)> availableLanguages { get; set; }

        [XmlIgnore]
        public route route { get; set; }

        public pnTour()
        {
            tourName = string.Empty;
            tourDescription = string.Empty;
            tourImage = string.Empty;
            tourNaration = string.Empty;
            stops = new List<stop>();
            languageNumber = new int();
            languageDisplayName = string.Empty;
            narationPlayer = null;
            availableLanguages = new List<(int number, string name, string location)>();
        }
        public pnTour(int languageNumber)
        {
            availableLanguages = createListofLanguages();

            var serializer = new XmlSerializer(typeof(pnTour));
            var assembly = IntrospectionExtensions.GetTypeInfo(typeof(App)).Assembly;
            Stream stream = assembly.GetManifestResourceStream(availableLanguages.Find(x => x.number == languageNumber).location);
            pnTour deserializedTour = (pnTour)serializer.Deserialize(stream);

            tourName = deserializedTour.tourName;
            tourDescription = deserializedTour.tourDescription;
            tourImage = deserializedTour.tourImage;
            tourNaration = deserializedTour.tourNaration;
            stops = deserializedTour.stops;
            languageNumber = deserializedTour.languageNumber;
            languageDisplayName = deserializedTour.languageDisplayName;
            narationPlayer = new naration();
            route = new route();
        }

        public string returnResourceName(string resource)
        {
            return typeof(App).GetTypeInfo().Assembly.GetManifestResourceNames().First(X => X.Contains(resource));
        }

        public List<(int number, string name, string location)> createListofLanguages()
        {
            List<(int number, string name, string location)> returnMe = new List<(int number, string name, string location)>();

            var assembly = typeof(App).GetTypeInfo().Assembly;
            var list = assembly.GetManifestResourceNames();

            foreach (var item in list)
            {
                if (item.Contains(".languages.") && item.EndsWith(".xml"))
                {
                    XDocument xdoc = XDocument.Load(assembly.GetManifestResourceStream(item));
                    returnMe.Add((Int32.Parse(xdoc.Descendants("languageNumber").First().Value), xdoc.Descendants("languageDisplayName").First().Value, item));
                }
            }

            return returnMe;
        }
    }

    [XmlRoot(ElementName = "stop")]
    public class stop
    {
        [XmlElement(ElementName = "stopNumber")]
        public int stopNumber { get; set; }
        [XmlElement(ElementName = "stopName")]
        public string stopName { get; set; }
        [XmlElement(ElementName = "stopDesc")]
        public string stopDesc { get; set; }
        [XmlElement(ElementName = "stopImage")]
        public string stopImage { get; set; }

        [XmlElement(ElementName = "stopNaration")]
        public string stopNaration { get; set; }

        [XmlElement(ElementName = "stopLat")]
        public double stopLat { get; set; }

        [XmlElement(ElementName = "stopLng")]
        public double stopLng { get; set; }

        [XmlIgnore]
        public ImageSource stopImageSource { get { return ImageSource.FromResource(returnResourceName(stopImage)); } }

        public string returnResourceName(string resource)
        {
            return typeof(App).GetTypeInfo().Assembly.GetManifestResourceNames().First(X => X.Contains(resource));
        }
    }
}
