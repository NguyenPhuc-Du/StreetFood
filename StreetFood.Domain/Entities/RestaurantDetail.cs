using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class RestaurantDetail
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public string OpeningHours { get; set; }
        public string Phone { get; set; }

        public POI Poi { get; set; }
    }
}
