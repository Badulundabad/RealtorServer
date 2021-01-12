using System;
using System.Data.Entity;
using RealtyModel.RealtorObject.DerivedClasses;

namespace RealtorServer.Model
{
    public class RealtyContext : DbContext
    {
        public RealtyContext() : base("RealtyDBConnection")
        {
            Flats.Load();
            Houses.Load();
        }
        public DbSet<Flat> Flats { get; set; }
        public DbSet<House> Houses { get; set; }
    }
}
