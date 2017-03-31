using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Printing;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Maps.MapControl.WPF;
using Microsoft.Maps.MapControl.WPF.Design;

using Microsoft.Win32;


namespace KijijiScannerWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        /*public struct Posting
        {
            public string AdID;
            public string Title { get; set; }
            public string DateListed;
            public string Address { get; set; }
            public string EventDate;
            public string EventTime;
            public string Description;
            public string URL;
            public bool Disabled;
            public DateTime StartTime;
            public bool Geolocation;
            public double Latitude;
            public double Longitude;
        }*/

        struct Website
        {
            public string WebAddress;
            public string WebDomain;
            public string NextWebAddress;
            public string PostingURL;
            public string AdId;
            public string Title;
            public string DateListed;
            public string Address;
            public string EventDate;
            public string EventTime;
            public string Description;
            public string URL;
            public int PostingDayWindow;
        }

        Website kijiji, PGCitizen; //TODO: turn into a list

        public List<Posting> postings, posts;
        int numberOfPostings, completedPostings;

        bool saveEdits = false;

        string BingMapsKey = "co5HlO5MDTfFdfQV2KMU~lWLwr4zWvyByiisRMvJzyQ~Ajr3X4HZOoln0paw7U5i0mXNxVNi07MUlAPTpKsVIf42QSDSDH9EwfQU7NSssJXy";

        public MainWindow()
        {
            InitializeComponent();
            postings = new List<Posting>();
            posts = new List<Posting>();

            numberOfPostings = 0;
            completedPostings = 0;

            WebRequest.DefaultWebProxy = null;

            kijiji = new Website();
            kijiji.WebAddress = "http://www.kijiji.ca/b-garage-sale-yard-sale/prince-george/c638l1700143";
            kijiji.WebDomain = "http://www.kijiji.ca";
            kijiji.NextWebAddress = @"<a title=""Next"" href=""(?<next>.+?)\s*""";
            kijiji.PostingURL = @"<div class=""title"">\s*<a href=""(?<link>.+?)\s*""";
            kijiji.AdId = null;
            kijiji.Address = @"<th>\s*Address\s*</th>\s*<td>\s*(?<search>.+?)\s*<br";
            kijiji.DateListed = @"<th>\s*Date Listed\s*</th>\s*<td>\s*(?<search>.+?)\s*</td>";
            kijiji.Title = @"<span itemprop=""name""><h1>\s*(?<search>.+?)\s*</h1>";
            kijiji.EventDate = @"<th>\s*Event Date\(s\)\s*</th>\s*<td>\s*(?<search>.+?)\s*</td>";
            kijiji.EventTime = @"<th>\s*Start Time\s*</th>\s*<td>\s*(?<search>.+?)\s*</td>";
            kijiji.Description = @"<td><span itemprop\W*description\W*(?<search>.+?)\s*</span>";
            kijiji.URL = @"<meta property=""og:url"" content=""\s*(?<search>.+?)\s*""/>";
            kijiji.PostingDayWindow = 0;

            PGCitizen = new Website();
            PGCitizen.WebAddress = "http://classifieds.princegeorgecitizen.com/prince-george/garage-sales/search?limit=100";
            PGCitizen.WebDomain = "http://classifieds.princegeorgecitizen.com";
            PGCitizen.NextWebAddress = @"<li class=""ap_page_link ap_paginator_next_page""><a href=""(?<next>.+?)\s*""";
            PGCitizen.PostingURL = @"<div class=""ap_summary_title"">\s*<a href=""(?<link>.+?)\s*""";
            PGCitizen.AdId = null;
            PGCitizen.Address = @"<div class=""ap_detail_ad_title"">\s*(?<search>.+?)\s*</div>";

            PGCitizen.DateListed = @"<div class=""ap_detail_ad_postdate"">\s*Post Date:\s*(?<search>.+?)\s*</div>";
            PGCitizen.Title = @"<div class=""ap_detail_ad_title"">\s*(?<search>.+?)\s*</div>";
            PGCitizen.EventDate = @"<div class=""garagesale1"">Date 1:\s*<span>\s*(?<search>.+?)\s*</span>";
            PGCitizen.Description = @"<div class=""ap_detail_ad_body"">\s*(?<search>.+?)\s*</div>";
            PGCitizen.URL = @"<input type=""hidden value=""\s*(?<search>.+?)\s*""";
            PGCitizen.PostingDayWindow = 5;

            //parse the combobox
            DateTime start = DateTime.Now;
            start = start.Date + new TimeSpan(07, 00, 00);
            DateTime finish = start.Date + new TimeSpan(12, 00, 00);
            for (DateTime dt = start; dt <= finish; dt = dt.AddMinutes(15))
            {
                StartTimeCombobox.Items.Add(dt.ToString("HH:mm"));
            }
        }

        //button to open url and read the webpage
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ScanButton.IsEnabled = false;
            ScanMenuItem.IsEnabled = false;
            PrintMenuItem.IsEnabled = false;
            SavePlanMenuItem.IsEnabled = false;

            //clear records of previous searches
            PostListbox.ItemsSource = null;
            AdIdTextblock.Text = "Ad: ";
            PostingDateTextblock.Text = "Posting Date: ";
            TitleTextbox.Text = null;
            DateTextbox.Text = null;
            AddressTextbox.Text = null;
            DetailsTextbox.Text = null;
            PostLabel.Content = "0 posts of 0 total";

            postings.Clear();
            posts.Clear();

            numberOfPostings = 0;
            completedPostings = 0;

            //adds the date from the calendar
            DateTime dt = new DateTime();

            //configuring kijiji's dates
            if (KijijiCalendar.SelectedDate.HasValue)
            {
                dt = KijijiCalendar.SelectedDate.Value;
            }
            else
            {
                dt = DateTime.Today;
            }

            string date = String.Format("/b-garage-sale-yard-sale/prince-george/c638l1700143?sort=startDateAsc&event-end-date={0}-{1}-{2}T00:00:00Z__",
                dt.Year,
                dt.Month,
                dt.Day);

            //kijiji
            kijiji.WebAddress = kijiji.WebDomain + date;
            await GetMainSiteAsync(kijiji);

            //PG Citizen website
            await GetMainSiteAsync(PGCitizen);

            ScanButton.IsEnabled = true;
            ScanMenuItem.IsEnabled = true;
            SavePlanMenuItem.IsEnabled = true;

            if (postings.Count > 0)
            {
                PrintButton.IsEnabled = true;
                PrintMenuItem.IsEnabled = true;
            }
        }

        //grabs all posts from a website
        private async Task GetMainSiteAsync(Website website)
        {
            string webpage = await GetWebpageAsync(website.WebAddress);

            if (webpage != null)
            {
                //used to determine each posting's url
                //const string links = @"<td class=""description"">\s*<a href=""(?<link>.+?)\s*""";

                Regex regex = new Regex(website.PostingURL, RegexOptions.IgnoreCase);
                MatchCollection matches = regex.Matches(webpage);

                //used to store the new Posting before adding it to Postings list
                Posting p;
                List<Posting> posts = new List<Posting>();

                foreach (Match m in matches)
                {
                    p = new Posting();
                    p.URL = m.Groups["link"].Value;
                    posts.Add(p);
                }

                numberOfPostings += posts.Count;
                PostLabel.Content = String.Format("{0} posts of {1} total", completedPostings, numberOfPostings);

                //get each posting and , if within the specified date, put it into the postings list Bound to the listbox
                foreach (Posting post in posts)
                {
                    await GetPostAsync(website, post.URL);

                    //clear, sort, and relist the listbox
                    int index = PostListbox.SelectedIndex;
                    PostListbox.ItemsSource = null;

                    if (postings.Count > 1)
                    {
                        postings.Sort((s1, s2) => s1.StartTime.CompareTo(s2.StartTime));
                    }

                    PostListbox.ItemsSource = postings;
                    PostListbox.SelectedIndex = index; //when you null a list box, you move the index. This returns it to the same place
                    completedPostings++;
                    PostLabel.Content = String.Format("{0} posts of {1} total", completedPostings, numberOfPostings);
                    NumberOfPostsLabel.Content = "Posts: " + postings.Count;

                    UpdateMap();
                }
            }
        }

        //uses task/await to grab webpage's html file
        private async Task<string> GetWebpageAsync(string url)
        {
            //prepare to grab html file
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.Proxy = null;

            try
            {
                //download the html file and save it to content
                using (WebResponse response = await webReq.GetResponseAsync())
                {

                    using (Stream responseStream = response.GetResponseStream())
                    {
                        var webpage = new MemoryStream();
                        await responseStream.CopyToAsync(webpage);

                        webpage.Position = 0;
                        var reader = new StreamReader(webpage);
                        var text = reader.ReadToEnd();

                        return text;

                    }
                }
            }
            catch(Exception ex)
            {
                return null;
            }

        }

        //goes to the post's webpage and grabs info
        private async Task GetPostAsync(Website website, string URL)
        {
            string webpage = await GetWebpageAsync(website.WebDomain + URL);

            if (webpage != null)
            {
                Posting post = new Posting();

                if (website.AdId != null)
                {
                    post.AdID = PostMatch(website.AdId, webpage);
                }

                if (website.DateListed != null)
                {
                    post.DateListed = PostMatch(website.DateListed, webpage);
                }

                if (website.EventDate != null)
                {
                    post.EventDate = PostMatch(website.EventDate, webpage);
                }

                if (website.EventTime != null)
                {
                    post.EventTime = PostMatch(website.EventTime, webpage);

                    //converts start time from a string to a DateTime Object from the first split
                    try
                    {
                        post.StartTime = DateTime.Parse(post.EventTime.Split(' ')[0]);
                    }
                    catch (Exception ex)
                    {

                    }
                }

                if (website.Address != null)
                {
                    post.Address = PostMatch(website.Address, webpage);
                }

                if (website.Description != null)
                {
                    post.Description = PostMatch(website.Description, webpage);
                }

                if (website.URL != null)
                {
                    post.URL = website.WebDomain + URL;
                }

                if (website.Title != null)
                {
                    post.Title = PostMatch(website.Title, webpage);
                }

                post.Disabled = false;

                if ((post.EventDate == null || CheckDate(post.EventDate)) && IsPostingDateWithinRange(post.DateListed, website.PostingDayWindow))
                {
                    if (post.Address != null)
                    {
                        var loc = AddAddressToMap(post.Address);
                        post.Geolocation = false;

                        if (loc.Geolocation)
                        {
                            post.Geolocation = true;
                            post.Latitude = loc.Latitude;
                            post.Longitude = loc.Longitude;
                            post.Address = loc.Address;
                        }
                    }
                    if (!DuplicateAddress(post))
                    {
                        postings.Add(post);
                    }
                }
            }
        }

        //finds each post's details
        private string PostMatch(string pattern, string webpage)
        {
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
            Match match = regex.Match(webpage);

            if (match.Success)
            {
                return match.Groups[1].Value.ToString();
            }

            return null;

        }

        private bool IsPostingDateWithinRange(string postDate, int postingDayWindow)
        {
            if (postingDayWindow == 0)
            {
                return true;
            }

            postDate = postDate + " " + DateTime.Today.Year.ToString();
            postDate = postDate.Replace(".", "");

            bool value = false;
            DateTime post = DateTime.Parse(postDate);
            DateTime date = KijijiCalendar.SelectedDate.Value;
            int numberOfDays = (date - post).Days;

            if (numberOfDays <= postingDayWindow)
            {
                value = true;
            }

            return value;
        }

        private Posting AddAddressToMap(string address)
        {
            var xmlDoc = Geocode(address + ", Prince George, BC");
            Posting post = new Posting();
            post.Geolocation = false;

            if (xmlDoc != null)
            {
                //Create namespace manager
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("rest", "http://schemas.microsoft.com/search/local/ws/rest/v1");

                //Get all geocode locations in the response 
                XmlNodeList locationElements = xmlDoc.SelectNodes("//rest:Location", nsmgr);
                if (locationElements.Count == 0)
                {
                    //MessageBox.Show("The location you entered could not be located.");
                    return post;
                }
                else
                {
                    //Get the geocode location points that are used for display (UsageType=Display)
                    XmlNodeList displayGeocodePoints =
                            locationElements[0].SelectNodes(".//rest:GeocodePoint/rest:UsageType[.='Display']/parent::node()", nsmgr);
                    string latitude = displayGeocodePoints[0].SelectSingleNode(".//rest:Latitude", nsmgr).InnerText;
                    string longitude = displayGeocodePoints[0].SelectSingleNode(".//rest:Longitude", nsmgr).InnerText;
                    var correctedAddress = locationElements[0].SelectSingleNode(".//rest:FormattedAddress", nsmgr).InnerText;
                    double[] value = new double[2] { Convert.ToDouble(latitude), Convert.ToDouble(longitude) };
                    
                    post.Latitude = value[0];
                    post.Longitude = value[1];
                    post.Address = correctedAddress;
                    post.Geolocation = true;

                }
            }
            else
            {
                
            }
            return post;
        }

        // Geocode an address and return a latitude and longitude
        public XmlDocument Geocode(string addressQuery)
        {
            //Create REST Services geocode request using Locations API
            string geocodeRequest = "http://dev.virtualearth.net/REST/v1/Locations/" + addressQuery + "?o=xml&key=" + BingMapsKey;

            //Make the request and get the response
            XmlDocument geocodeResponse = GetXmlResponse(geocodeRequest);

            return (geocodeResponse);
        }

        // Submit a REST Services or Spatial Data Services request and return the response
        private XmlDocument GetXmlResponse(string requestUrl)
        {
            System.Diagnostics.Trace.WriteLine("Request URL (XML): " + requestUrl);
            HttpWebRequest request = WebRequest.Create(requestUrl) as HttpWebRequest;
            try
            {
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new Exception(String.Format("Server error (HTTP {0}: {1}).",
                        response.StatusCode,
                        response.StatusDescription));
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(response.GetResponseStream());
                    return xmlDoc;
                }
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        //Add a pushpin with a label to the map
        private void AddPushpinToMap(double latitude, double longitude, string pinLabel, DateTime time)
        {
            Location location = new Location(latitude, longitude);
            Pushpin pushpin = new Pushpin();
            pushpin.Content = pinLabel;
            pushpin.Location = location;
            pushpin.MouseDown += new MouseButtonEventHandler(PushpinClicked);

            if (time != null)
            {
                var color = PinColor(time);
                pushpin.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color[0], color[1], color[2]));
            }
            else
            {
                pushpin.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0,0,0));
            }
            BingMap.Children.Add(pushpin);
            
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void PostListbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = PostListbox.SelectedIndex;
            if (index > -1)
            {
                Posting post = postings[index];
                saveEdits = false;

                AdIdTextblock.Text = "Ad: " + post.URL;

                if (post.URL != null)
                {
                    AdUrlLink.NavigateUri = new Uri(post.URL);
                }

                PostingDateTextblock.Text = "Posting Date: " + post.DateListed;
                TitleTextbox.Text = post.Title;
                DateTextbox.Text = post.EventDate;
                AddressTextbox.Text = post.Address;
                DetailsTextbox.Text = post.Description;
                StartTimeCombobox.SelectedValue = post.StartTime.ToString("HH:mm");

                MoveToPushPin(post);

                saveEdits = true;
            }
        }

        private bool CheckDate (string date)
        {
            //date will come in as "25-Mar-16 to 26-Mar-16"
            //convert start and end dates to Date objects
            DateTime startDate, endDate;
            string[] splitter = new string[] {" to "};
            var dates = date.Split(splitter, StringSplitOptions.None);

            if (dates != null)
            {
                try
                {
                    startDate = Convert.ToDateTime(dates[0]);
                    if (dates.Count() > 1)
                    {
                        endDate = Convert.ToDateTime(dates[1]);
                    }
                    else
                    {
                        endDate = startDate;
                    }

                    //see if selected date on calendar falls within the time frame
                    //return true if so
                    DateTime dt = KijijiCalendar.SelectedDate.Value;

                    if (dt >= startDate && dt <= endDate)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {

                }
            }

            

            return false;
        }

        private void TitleTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void UpdatePostings()
        {
            //saves the old reference
            int index = PostListbox.SelectedIndex;

            if (index > -1 && saveEdits == true)
            {
                Posting post = postings[index];

                post.Title = TitleTextbox.Text;
                post.EventDate = DateTextbox.Text;
                if (post.Address != AddressTextbox.Text)
                {
                    post.Address = AddressTextbox.Text;

                    //if the address has changed, so should the geolocation
                    //the issue is, can't check on the address for each key press

                }
                
                post.Description = DetailsTextbox.Text;

                if (StartTimeCombobox.SelectedIndex > -1)
                {
                    post.StartTime = DateTime.Parse(StartTimeCombobox.SelectedValue.ToString());
                }

                postings[index] = post;

                PostListbox.ItemsSource = null;
                PostListbox.ItemsSource = postings;

                PostListbox.SelectedIndex = index;
                NumberOfPostsLabel.Content = "Posts: " + postings.Count;

                UpdateMap();

            }
        }

        //adds pins to the map based on the entries in postings
        private void UpdateMap()
        {
            BingMap.Children.Clear();

            int value = 1;
            //add pins with Tapped events
            foreach(Posting post in postings)
            {
                //if the address has a geolocation
                if (post.Geolocation)
                {
                    AddPushpinToMap(post.Latitude, post.Longitude, value.ToString(), post.StartTime);
                    value++;
                }
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

        private void DateTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void TimeTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void AddressTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void DetailsTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemovePosting();
        }

        private void SavePlan_Click(object sender, RoutedEventArgs e)
        {
            SavePlan();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            SavePlan();
        }

        private void AdUrlLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (AdUrlLink.NavigateUri != null)
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                e.Handled = true;
            }
        }

        private void RemovePosting()
        {
            int index = PostListbox.SelectedIndex;

            if (index > -1)
            {
                postings.RemoveAt(index);

                PostListbox.ItemsSource = null;
                PostListbox.ItemsSource = postings;

                //listbox to default selecting item below deleted item
                if (index >= postings.Count)
                {
                    PostListbox.SelectedIndex = postings.Count - 1;
                }
                else
                {
                    PostListbox.SelectedIndex = index;
                }

                NumberOfPostsLabel.Content = "Posts: " + postings.Count;

                UpdateMap();
            }

            if (postings.Count < 1)
            {
                PrintButton.IsEnabled = false;
                PrintMenuItem.IsEnabled = false;
            }
        }

        private void PostListbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                RemovePosting();
            }
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = PostListbox.SelectedIndex;

            if(index > 0)
            {
                postings.Insert(index -1, postings[index]);
                postings.RemoveAt(index + 1);

                PostListbox.ItemsSource = null;
                PostListbox.ItemsSource = postings;

                PostListbox.SelectedIndex = index - 1;
                UpdateMap();
            }

        }

        private void UpButton_Copy_Click(object sender, RoutedEventArgs e)
        {
            int index = PostListbox.SelectedIndex;

            if (index > -1 && index + 1 < postings.Count)
            {
                postings.Insert(index + 2, postings[index]);
                postings.RemoveAt(index);

                PostListbox.ItemsSource = null;
                PostListbox.ItemsSource = postings;

                PostListbox.SelectedIndex = index + 1;

                UpdateMap();
            }
        }

        private void StartTimeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePostings();
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            PostListbox.ItemsSource = null;

            if (postings.Count > 1)
            {
                postings.Sort((s1, s2) => s1.StartTime.CompareTo(s2.StartTime));
            }

            PostListbox.ItemsSource = postings;
            UpdateMap();
        }

        private void GeolocationButton_Click(object sender, RoutedEventArgs e)
        {
            //if there is an address in the field
            string address = AddressTextbox.Text + ", Prince George, BC";
            if (address.Count() > 0)
            {
                //find an address from Bing's REST
                var coordinates = AddAddressToMap(address);

                //if the provided location is good, save it
                if (coordinates.Geolocation)
                {
                    int index = PostListbox.SelectedIndex;
                    Posting post = postings[index];
                    post.Geolocation = true;
                    post.Latitude = coordinates.Latitude;
                    post.Longitude = coordinates.Longitude;
                    post.Address = coordinates.Address;
                    postings[index] = post;
                    UpdateMap();
                    MoveToPushPin(post);
                    AddressTextbox.Text = post.Address;
                }
                else
                {
                    MessageBox.Show("Unable to find location.");
                }
            }
        }

        //used to simplify colors for pins
        private byte[] PinColor(DateTime startTime)
        {
            byte[] color = new byte[3] { 0, 0, 0 };

            if (startTime != DateTime.Today)
            {
                byte red, green;
                byte blue = 0;
                switch(startTime.Hour)
                {
                    case 7:
                        red = 0;
                        green = 255;
                        break;
                    case 8:
                        red = 0;
                        green = 200;
                        break;
                    case 9:
                        red = 220;
                        green = 220;
                        break;
                    case 10:
                        red = 255;
                        green = 160;
                        break;
                    case 11:
                        red = 255;
                        green = 100;
                        break;
                    case 12:
                        red = 255;
                        green = 0;
                        break;

                    default:
                        red = 0;
                        green = 0;
                        break;
                }
                color[0] = red;
                color[1] = green;
                color[2] = blue;
                
            }

            return color;

        }

        private void PushpinClicked(object sender, MouseButtonEventArgs e)
        {
            Pushpin pin = (Pushpin)sender;
            var index = pin.Content;
            int refIndex = Convert.ToInt16(index.ToString());
            PostListbox.SelectedIndex = refIndex - 1;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveXml();
        }

        private void SaveXml()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Xml files (*.Xml)|*.xml";
            sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (sfd.ShowDialog() == true)
            {
                System.Xml.Serialization.XmlSerializer writer =
                    new System.Xml.Serialization.XmlSerializer(typeof(List<Posting>));

                FileStream file = File.Create(sfd.FileName);

                writer.Serialize(file, postings);
                file.Close();
            }
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            LoadXml();
            UpdatePostings();
            UpdateMap();
        }

        private void PlanningButton_Click(object sender, RoutedEventArgs e)
        {
            Planning form = new Planning(postings);
            form.Show();
        }

        //loads a Postings saved XML exported file 
        private void LoadXml()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Xml files (*.Xml)|*.xml";
            ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == true)
            {
                postings.Clear();

                System.Xml.Serialization.XmlSerializer reader =
                    new System.Xml.Serialization.XmlSerializer(typeof(List<Posting>));

                StreamReader file = new StreamReader(ofd.FileName);

                postings = (List<Posting>)reader.Deserialize(file);
                
                file.Close();

                PostListbox.ItemsSource = null;
                PostListbox.ItemsSource = postings;

                PrintButton.IsEnabled = true;
            }
        }

        //searches the postings list and removes duplicates
        //deletes address that have the same geolocation, leaving the first one found alone
        private void RemoveDuplicateAddress()
        {
            List<int> indexes = new List<int>();
            foreach(Posting p in postings)
            {
                indexes.Clear();
                if (p.Geolocation)
                {
                    for(int x = 0; x < postings.Count; x++)
                    {
                        if (postings[x].Geolocation)
                        {
                            if (postings[x].Latitude == p.Latitude && postings[x].Longitude == p.Longitude)
                            {
                                indexes.Add(x);
                            }
                        }
                    }

                    if (indexes.Count > 1)
                    {
                        //should avoid deleting the first copy of the address
                        for (int x = indexes.Count - 1; x > 0; x--)
                        {
                            postings.RemoveAt(indexes[x]);
                        }
                    }
                }

            }

        }

        //searches addresses and sees if the address is a duplicate
        private bool DuplicateAddress(Posting post)
        {
            int index = 0;
            if (post.Geolocation)
            {
                foreach (Posting p in postings)
                {
                    if (p.Geolocation)
                    {
                        if (p.Latitude == post.Latitude && p.Longitude == post.Longitude)
                        {
                            index++;
                        }
                    }
                }
                if (index > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private void SavePlan()
        {
            const string br = "\r\n";
            //create a string and print it as a notepad file
            string text = "Garage Sale Listings" + br + br;

            //getting the date
            DateTime dt = new DateTime();

            if (KijijiCalendar.SelectedDate.HasValue)
            {
                dt = KijijiCalendar.SelectedDate.Value;
            }
            else
            {
                dt = DateTime.Today;
            }

            text += string.Format("Date: {0}/{1}/{2}" + br + br,
                dt.Month,
                dt.Day,
                dt.Year);

            //posts each posting
            string post = "";
            int index = 1;

            foreach (Posting p in postings)
            {
                post = String.Format("[ ] ({0}) ({1}h) {2}\r\n{3}\r\n\r\n",
                    index,
                    p.StartTime.ToString("HH:mm"),
                    p.Address,
                    p.Description);

                text += post;
                index++;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Text files (*.txt)|*.txt";
            sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, text);
            }
        }

        //create a file to load to google maps
        private void ExportPlan_Click(object sender, RoutedEventArgs e)
        {
            ExportKML();
        }

        //export plan as KML to be used as a Google Map
        private void ExportKML()
        {
            string text = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><kml xmlns=\"http://www.opengis.net/kml/2.2\">\r\n";
            text += "<Document>";
            text += "<name>Garage Sales</name><open> 1 </open><Style id=\'exampleStyleDocument\'>" +
                "<LabelStyle><color>ff0000cc</color></LabelStyle></Style>";
            text += "<Style id=\'earlyTime\'><LabelStyle><color>50fa7800</color></LabelStyle></Style>"; //blue
            text += "<Style id=\"0800\"><IconStyle><color>ff00ff00</color></IconStyle></Style>"; //green
            //<Style id=\"0830\"><LabelStyle><color>ff0000ff</color></LabelStyle></Style>" //dark green
            //<Style id=\"0900\"><LabelStyle><color>ff0000ff</color></LabelStyle></Style>"
            //<Style id=\"0930\"><LabelStyle><color>ff0000ff</color></LabelStyle></Style>"
            //<Style id=\"1000\"><LabelStyle><color>ff0000ff</color></LabelStyle></Style>"
            //<Style id=\"lateTime\"><LabelStyle><color>ff0000ff</color></LabelStyle></Style>"

            foreach (Posting p in postings)
            {
                string link = String.Format("<a href=\"{0}\">{1}</a><br>", p.URL, p.Address);
                text += "\t<Placemark>\r\n";
                text += String.Format("\t\t<name>{0}</name>\r\n", p.StartTime.ToString("HH:mm"));
                text += String.Format("\t\t<description><![CDATA[{0}{1}]]></description>\r\n",link, p.Description);
                text += String.Format("\t\t<Point>\r\n<coordinates>{0},{1},0</coordinates>\r\n\t\t</Point>\r\n</Placemark>\r\n",
                    p.Longitude,
                    p.Latitude);
            }
            text += "</Document></kml>";

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "KML files (*.kml)|*.kml";
            sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, text);
            }
        }

    }
}
