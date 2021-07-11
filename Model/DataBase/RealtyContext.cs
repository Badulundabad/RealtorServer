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
            //Houses.Load();
            Streets.Load();
            Albums.Load();
        }
        
        public DbSet<Flat> Flats { get; set; }
        public DbSet<House> Houses { get; set; }
        public DbSet<Street> Streets {get;set;}
        public DbSet<Album> Albums {get;set;}

    }
}
