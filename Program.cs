using RealtorServer.Model.DataBase;
using RealtorServer.Model.NET;
using System;

namespace RealtorServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "RealtyServer";
            Console.BufferHeight = 1000;
            new RealtyContext();
            new Server().RunAsync();
            String input = "";
            while (input != "stop")
                input = Console.ReadLine();
        }
        private void ClearDB()
        {
            using (RealtyContext realtyDB = new RealtyContext())
            {
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Albums'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Flats'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Houses'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Locations'");
                realtyDB.Albums.Local.Clear();
                realtyDB.Flats.Local.Clear();
                realtyDB.SaveChanges();
            }
        }
    }
}