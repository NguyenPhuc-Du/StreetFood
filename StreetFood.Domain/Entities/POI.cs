using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class POI
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Radius { get; set; }
        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<RestaurantDetail> RestaurantDetails { get; set; }
        public ICollection<RestaurantAudio> RestaurantAudios { get; set; }
        public ICollection<Food> Foods { get; set; }
        public ICollection<OwnerRequest> OwnerRequests { get; set; }
        public ICollection<DeviceVisit> DeviceVisits { get; set; }
    }
}
