using System;

namespace StreetFood.Domain.Entities
{
    public class UserAccount
    {
        public int id { get; set; } 
        public string username { get; set; } 
        public string password { get; set; } 
        public string role { get; set; } 

        
        public DateTime createdat { get; set; }

        public string? restaurant_owners { get; set; }
        public string? script_change_requests { get; set; }
    }
}