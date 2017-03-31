using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KijijiScannerWPF
{
    public class Posting
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
    }
}
