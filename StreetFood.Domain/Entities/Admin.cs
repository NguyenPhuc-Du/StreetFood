using System;
using System.Collections.Generic;
using System.Text;

namespace StreetFood.Domain.Entities
{
    public class Admin
    {
        public int Id { get; set; }

        public string Username { get; set; }
        public string PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
