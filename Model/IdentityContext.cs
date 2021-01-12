using System.Data.Entity;
using RealtyModel;

namespace RealtorServer.Model
{
    public class IdentityContext : DbContext
    {
        public IdentityContext() : base("UserDBConnection")
        {
            Users.Load();
            Users.Local.CollectionChanged += (sender, e) => { this.SaveChanges(); };
        }
        public DbSet<User> Users { get; set; }
    }
}
