using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class LocationLog
    {
        public int Id { get; set; }

        public string DeviceId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
