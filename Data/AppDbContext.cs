using Microsoft.EntityFrameworkCore;
using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<TrackedFile> TrackedFiles { get; set; }
    }
}