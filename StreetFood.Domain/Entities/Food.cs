using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class Food
    {
        public int Id { get; set; }
        public int PoiId { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }

        public POI Poi { get; set; }
    }
}
