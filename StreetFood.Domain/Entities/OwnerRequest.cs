using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class OwnerRequest
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public string RequestType { get; set; }
        public string Content { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public POI Poi { get; set; }
    }
}
