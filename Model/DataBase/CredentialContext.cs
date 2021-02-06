using System.Data.Entity;
using RealtyModel.Model;

namespace RealtorServer.Model.DataBase
{
    public class CredentialContext : DbContext
    {
        public CredentialContext() : base("UserDBConnection")
        {
            Credentials.Load();
        }
        public DbSet<Credential> Credentials { get; set; }
    }
}
