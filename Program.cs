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
    }
}