using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class DeviceVisit
    {
        public int Id { get; set; }

        public string DeviceId { get; set; }
        public int PoiId { get; set; }

        public DateTime EnterTime { get; set; }
        public DateTime ExitTime { get; set; }
        public int Duration { get; set; }

        public POI Poi { get; set; }
    }
}
