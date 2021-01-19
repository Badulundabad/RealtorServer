using System.Data.Entity;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class AlbumContext : DbContext
    {
        public AlbumContext() : base("PhotoDBConnection")
        {
            Albums.Load();
        }
        public DbSet<Album> Albums { get; set; }
    }
}
