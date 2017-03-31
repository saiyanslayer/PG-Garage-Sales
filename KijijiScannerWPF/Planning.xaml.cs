using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Maps.MapControl.WPF;

namespace KijijiScannerWPF
{
    /// <summary>
    /// Interaction logic for Planning.xaml
    /// </summary>
    public partial class Planning : Window
    {
        public List<Posting> postings;
        private List<int> planning;
        DateTime planTime;
        private Posting post;

        public Planning()
        {
            InitializeComponent();
            postings = new List<Posting>();
            planning = new List<int>();
            planTime = DateTime.Today;
            planTime = planTime.Date.AddHours(8);
            post = new Posting();
        }

        public Planning(List<Posting> postings)
        {
            InitializeComponent();
            this.postings = postings;
            planning = new List<int>();
            planTime = DateTime.Today;
            planTime = planTime.Date.AddHours(8);
            post = new Posting();
            UpdateMap();
        }

        //clears the map and replaces the pins
        private void UpdateMap()
        {
            BingMap.Children.Clear();

            //add pins with Tapped events
            int index = 0;
            foreach (Posting post in postings)
            {
                //if the address has a geolocation
                if (post.Geolocation)
                {
                    int value = planning.IndexOf(index);
                    if (value > -1)
                    {
                        AddPushpinToMap(value+1, post.Latitude, post.Longitude, post.StartTime, index);
                    }
                    else
                    {
                        AddPushpinToMap(0, post.Latitude, post.Longitude, post.StartTime, index);
                    }
                }
                index++;
            }
        }

        private void MoveToPushPin(Posting post)
        {
            //move map to coordinates if they exist
            if (post.Geolocation)
            {
                int zoom = Convert.ToInt16(BingMap.ZoomLevel);

                Location loc = new Location(post.Latitude, post.Longitude);
                BingMap.SetView(loc, zoom);
            }
        }

        //Add a pushpin with a label to the map
        private void AddPushpinToMap(int label, double latitude, double longitude, DateTime time, int index)
        {
            Location location = new Location(latitude, longitude);
            Pushpin pushpin = new Pushpin();
            pushpin.Location = location;
            
            pushpin.ToolTip = post.Description;
            pushpin.MouseDown += new MouseButtonEventHandler(PushpinClicked);

            if (label > 0)
            {
                pushpin.Content = label;
            }

            bool postExists = false;
            foreach(int i in planning)
            {
                if (i ==index)
                {
                    postExists = true;
                    break;
                }
            }

            if(postExists)
            {
                pushpin.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 150));
            }
            else if (time != null)
            {
                var color = PinColor(time);
                pushpin.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color[0], color[1], color[2]));
            }
            else
            {
                pushpin.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 0));
            }
            BingMap.Children.Add(pushpin);

        }

        //used to simplify colors for pins
        //colour depends on posting's time's proximity to the time
        private byte[] PinColor(DateTime startTime)
        {
            byte[] color = new byte[3] { 0, 0, 0 };

            if (startTime != DateTime.Today)
            {
                //converting times to minutes to make processing easier
                int planMinutes = planTime.Hour * 60 + planTime.Minute;
                int timeMinutes = startTime.Hour * 60 + startTime.Minute;
                int time = timeMinutes - planMinutes; 

                byte red, green;
                byte blue = 0;
                if (time <= 0) //the post has started
                {
                    red = 0;
                    green = 200;
                }
                else if (time <= 30)
                {
                    red = 0;
                    green = 255;
                }
                else if (time <= 60)
                {

                    red = 220;
                    green = 220;
                }
                else if (time <= 90)
                {
                    red = 255;
                    green = 160;
                }
                else
                {
                    red = 0;
                    green = 0;

                }
                color[0] = red;
                color[1] = green;
                color[2] = blue;

            }

            return color;

        }

        //add the pin to the plan
        private void PushpinClicked(object sender, MouseButtonEventArgs e)
        {
            Pushpin pin = (Pushpin)sender;
            string name = pin.Name;

            //figure out which pin was pressed
            int index = postings.FindIndex(x => x.Latitude == pin.Location.Latitude && x.Longitude == pin.Location.Longitude);

            //find if the index is currently in the list
            bool postExists = false;
            foreach(int i in planning)
            {
                if (i == index)
                {
                    postExists = true;
                    break;
                }
            }

            if (!postExists)
            {
                post = postings[index];
                planning.Add(index);
                UpdatePostings();

                //centre the map on the selected pin
                MoveToPushPin(post);

                //increment the time
                if (post.Address != null)
                {
                    TimeToTravel(pin);
                }

                UpdateMap();
            }

        }

        private void UpdatePostings()
        {
            postingsListBox.Items.Clear();
            foreach(int index in planning)
            {
                string address = postings[index].Address;
                postingsListBox.Items.Add(address);
            }
        }

        private void TimeToTravel(Pushpin pin)
        {
            //calculating diatnce to geo points
            double distance = 0;

            double lat1, lat2, long1, long2;
            lat1 = post.Latitude;
            long1 = post.Longitude;
            lat2 = pin.Location.Latitude;
            long2 = pin.Location.Longitude;

            double dLat = (lat2 - lat1) / 180 * Math.PI;
            double dLong = (long2 - long1) / 180 * Math.PI;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 / 180 * Math.PI) * Math.Cos(lat2 / 180 * Math.PI)
                * Math.Sin(dLong / 2) * Math.Sin(dLong / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double radiusE = 6378135;
            double radiusP = 6356750;

            double nr = Math.Pow(radiusE * radiusP * Math.Cos(lat1 / 180 * Math.PI), 2);
            double dr = Math.Pow(radiusE * radiusP * Math.Cos(lat1 / 180 * Math.PI), 2)
                + Math.Pow(radiusP * Math.Sin(lat1 / 180 * Math.PI), 2);
            double radius = Math.Sqrt(nr / dr);

            distance = radius * c; //result in meters

            double metersPerMinute = 50000 / 60;

            double minute = distance / metersPerMinute + 2;

            planTime = planTime.AddMinutes(minute);
            timeLabel.Content = planTime.ToString("HH:mm");

        }

        private void postingsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int index = postingsListBox.SelectedIndex;

            if (index > -1)
            {
                var refPost = postingsListBox.Items[index];
                Posting moveToPost = postings.Find(x => x.Address == refPost);

                MoveToPushPin(moveToPost);
            }
        }
    }
}
