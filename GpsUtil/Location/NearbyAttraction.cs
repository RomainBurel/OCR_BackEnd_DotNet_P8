namespace GpsUtil.Location
{
    public class NearbyAttraction
    {
        public string AttractionName { get; set; }
        public double AttractionLatitude { get; set; }
        public double AttractionLongitude { get; set; }
        public double UserLocationLatitude { get; set; }
        public double UserLocationLongitude { get; set; }
        public double Distance { get; set; }
        public int Rewards { get; set; }
    }
}
