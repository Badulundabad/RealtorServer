using System.Data.Entity;
using RealtyModel.Model;
using RealtyModel.Model.Derived;

namespace RealtorServer.Model.DataBase
{
    public class RealtyContext : DbContext
    {
        public RealtyContext() : base("RealtyDBConnection")
        {
            Flats.Load();
            Cities.Load();
            Districts.Load();
            Streets.Load();
            Locations.Load();
            Agents.Load();
            Albums.Load();
            Customers.Load();
        }
        public DbSet<Flat> Flats { get; set; }
        public DbSet<House> Houses { get; set; }

        public DbSet<Location> Locations {get;set;}
        public DbSet<City> Cities {get;set;}
        public DbSet<District> Districts {get;set;}
        public DbSet<Street> Streets {get;set;}

        public DbSet<Agent> Agents {get;set;}
        public DbSet<Customer> Customers { get; set; }

        public DbSet<Album> Albums {get;set;}
    }
}
