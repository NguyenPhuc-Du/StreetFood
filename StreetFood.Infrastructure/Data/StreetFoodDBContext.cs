using Microsoft.EntityFrameworkCore;
using StreetFood.Domain.Entities;

namespace StreetFood.Infrastructure.Data
{
    public class StreetFoodDBContext : DbContext
    {
        public StreetFoodDBContext(DbContextOptions<StreetFoodDBContext> options)
            : base(options)
        {
        }

        public DbSet<POI> POIs { get; set; }
        public DbSet<RestaurantDetail> RestaurantDetails { get; set; }
        public DbSet<RestaurantAudio> RestaurantAudios { get; set; }
        public DbSet<Food> Foods { get; set; }
        public DbSet<OwnerRequest> OwnerRequests { get; set; }
        public DbSet<DeviceVisit> DeviceVisits { get; set; }
        public DbSet<LocationLog> LocationLogs { get; set; }
        public DbSet<MovementPath> MovementPaths { get; set; }
        public DbSet<Admin> Admins { get; set; }

        public DbSet<UserAccount> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            
            modelBuilder.Entity<UserAccount>().ToTable("users");

            
        }
    }
}