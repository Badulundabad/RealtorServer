using System.Data.Entity;
using RealtyModel;

namespace RealtorServer.Model
{
    public class PhotoContext : DbContext
    {
        public PhotoContext() : base("PhotoDBConnection")
        {
            Photos.Load();
        }
        public DbSet<Photo> Photos { get; set; }
    }
}
