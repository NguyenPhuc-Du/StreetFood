using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class RestaurantAudio
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public string LanguageCode { get; set; }
        public string AudioUrl { get; set; }

        public POI Poi { get; set; }
    }
}
