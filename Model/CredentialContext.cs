using System.Data.Entity;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class CredentialContext : DbContext
    {
        public CredentialContext() : base("UserDBConnection")
        {
            Credentials.Load();
            Credentials.Local.CollectionChanged += (sender, e) => { this.SaveChanges(); };
        }
        public DbSet<Credential> Credentials { get; set; }
    }
}
