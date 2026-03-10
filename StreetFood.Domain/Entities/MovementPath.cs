using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class MovementPath
    {
        public int Id { get; set; }

        public string DeviceId { get; set; }

        public int FromPoiId { get; set; }
        public int ToPoiId { get; set; }

        public DateTime CreatedAt { get; set; }

        public POI FromPoi { get; set; }
        public POI ToPoi { get; set; }
    }
}
