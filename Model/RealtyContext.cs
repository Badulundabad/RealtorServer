using System.Data.Entity;
using RealtyModel.Model;
using RealtyModel.Model.Derived;

namespace RealtorServer.Model
{
    public class RealtyContext : DbContext
    {
        public RealtyContext() : base("RealtyDBConnection")
        {
            Flats.Load();
            //Houses.Load();
            Customers.Load();
        }
        public DbSet<DBFlat> Flats { get; set; }
        public DbSet<House> Houses { get; set; }
        public DbSet<Customer> Customers { get; set; }
    }
}
