using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using NLog;
using System.Diagnostics;

namespace RealtorServer.Model.NET
{
    class OperationHandling
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        protected Operation operation;
        protected bool IsDuplicate(Flat flat) {
            using (RealtyContext context = new RealtyContext()) {
                Location location = flat.Location;
                return context.Flats.Local.Any(bro => bro.Location.City == location.City
                    && bro.Location.District == location.District
                    && bro.Location.Street == location.Street
                    && bro.Location.HouseNumber.Equals(location.HouseNumber, StringComparison.OrdinalIgnoreCase)
                    && bro.Location.FlatNumber == location.FlatNumber);
            }
        }
        protected bool IsDuplicate(House house) {
            using (RealtyContext context = new RealtyContext()) {
                Location location = house.Location;
                return context.Houses.Local.Any(bro => bro.Location.City == location.City
                    && bro.Location.District == location.District
                    && bro.Location.Street == location.Street
                    && bro.Location.HouseNumber.Equals(location.HouseNumber, StringComparison.OrdinalIgnoreCase));
            }
        }
        protected void AddStreetIfNotExist(Flat flat, RealtyContext context) {
            bool hasDuplicateStreet = context.Streets.Local.Any(s => s.Name == flat.Location.Street);
            if (!hasDuplicateStreet) {
                context.Streets.Add(new Street() { Name = flat.Location.Street });
            }
        }
        protected void AddStreetIfNotExist(House house, RealtyContext context) {
            bool hasDuplicateStreet = context.Streets.Local.Any(s => s.Name == house.Location.Street);
            if (!hasDuplicateStreet) {
                context.Streets.Add(new Street() { Name = house.Location.Street });
            }
        }
        protected static void SetDates(Flat flat) {
            flat.RegistrationDate = DateTime.Now;
            flat.LastUpdateTime = flat.RegistrationDate;
        }
        protected static void SetDates(House house) {
            house.RegistrationDate = DateTime.Now;
            house.LastUpdateTime = house.RegistrationDate;
        }
        protected static int AddOrUpdateAlbum(Album album, RealtyContext context) {
            Album duplicateAlbum = context.Albums.Find(album.Id);
            Debug.WriteLine($"Пришел с id {album.Id}");
            if (duplicateAlbum != null) {
                context.Entry(duplicateAlbum).CurrentValues.SetValues(album);
                return album.Id;
            } else {
                context.Albums.Add(album);
                context.SaveChanges();
                return album.Id;
            }
        }
        public virtual Response Handle() {
            throw new NotImplementedException();
        }

        protected void LogInfo(String text) {
            Debug.WriteLine($"{DateTime.Now} INFO    {text}");
            Console.WriteLine($"{DateTime.Now} INFO    {text}");
            logger.Info($"    {text}");
        }
        protected void LogWarn(String text) {
            Debug.WriteLine($"{DateTime.Now} WARN    {text}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{DateTime.Now} WARN    {text}");
            Console.ForegroundColor = ConsoleColor.White;
            logger.Warn($"    {text}");
        }
        protected void LogError(String text) {
            Debug.WriteLine($"\n{DateTime.Now} ERROR    {text}\n");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now} ERROR    {text}");
            Console.ForegroundColor = ConsoleColor.White;
            logger.Error($"    {text}");
        }
    }
}
