using RealtorServer.Model.DataBase;
using RealtorServer.Model.NET;
using System;

namespace RealtorServer
{
    class Program
    {
        private static Server server = new Server();

        static void Main(string[] args)
        {
            server.RunAsync();
            Console.Title = "RealtyServer";
            Console.BufferHeight = 1000;
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
                realtyDB.Locations.Local.Clear();
                realtyDB.SaveChanges();
            }
        }
    }
}