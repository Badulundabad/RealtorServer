using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtorServer.Model.DataBase;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;
using RealtyModel.Model.RealtyObjects;
using RealtyModel.Model.Operations;
using RealtyModel.Service;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Action = RealtyModel.Model.Operations.Action;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {

        public RealtyServer(Dispatcher dispatcher) : base(dispatcher) {
            Dispatcher = dispatcher;
        }
        private void AddFlat(Operation operation) {
            try {
                Flat newFlat = BinarySerializer.Deserialize<Flat>(operation.Data);
                operation.Data = new byte[0];
                if (!FindDuplicate(Target.Flat, newFlat.Location)) {
                    if (AddPhotos(newFlat.Album, Target.Flat)) {
                        using (RealtyContext realtyDB = new RealtyContext()) {
                            if (newFlat.CustomerId > 0)
                                newFlat.Customer = realtyDB.Customers.Find(newFlat.CustomerId) ?? newFlat.Customer;
                            if (newFlat.Location.CityId > 0)
                                newFlat.Location.City = realtyDB.Cities.Find(newFlat.Location.CityId) ?? newFlat.Location.City;
                            if (newFlat.Location.DistrictId > 0)
                                newFlat.Location.District = realtyDB.Districts.Find(newFlat.Location.DistrictId) ?? newFlat.Location.District;
                            if (newFlat.Location.StreetId > 0)
                                newFlat.Location.Street = realtyDB.Streets.Find(newFlat.Location.StreetId) ?? newFlat.Location.Street;

                            newFlat.RegistrationDate = DateTime.Now;
                            newFlat.LastUpdateTime = DateTime.Now;

                            realtyDB.Flats.Local.Add(newFlat);
                            realtyDB.SaveChanges();
                        }

                        LogInfo($"Has registered a flat {newFlat.Location.City.Name} {newFlat.Location.District.Name} {newFlat.Location.Street.Name} {newFlat.Location.HouseNumber} кв{newFlat.Location.FlatNumber}");
                        operation.IsSuccessfully = true;
                    } else operation.IsSuccessfully = false;
                } else {
                    LogInfo($" there is a flat with such address");
                    operation.IsSuccessfully = false;
                }
            } catch (Exception ex) {
                LogError($"(AddFlat) {ex.Message}\n\n");
                operation.IsSuccessfully = false;
            }
        }
        private Boolean AddPhotos(Album album, Target objectType) {
            try {
                List<Photo> photos = new List<Photo>();
                foreach (Byte[] array in album.PhotoCollection)
                    photos.Add(new Photo() {
                        Data = array,
                        Location = album.Location,
                        ObjectType = objectType
                    });
                using (RealtyContext realtyDB = new RealtyContext()) {
                    realtyDB.Photos.AddRange(photos);
                    realtyDB.SaveChanges();
                }

                foreach (Photo photo in photos)
                    album.PhotoKeys += $"photo.Id.ToString();";

                return true;
            } catch (Exception ex) {
                LogError($"(AddPhoto) {ex.Message}\n\n");
                return false;
            }
        }
        //private void ChangeFlat(Operation operation) {
        //    LogInfo($"{operation.Number} change flat");
        //    try {
        //        Flat updFlat = BinarySerializer.Deserialize<Flat>(operation.Data);
        //        using (RealtyContext realtyDB = new RealtyContext()) {
        //            Flat dbFlat = realtyDB.Flats.Find(updFlat.Id) ?? throw new Exception($"There is no such flat {updFlat.Location.City.Name} {updFlat.Location.District.Name} {updFlat.Location.Street.Name} д{updFlat.Location.HouseNumber} кв{updFlat.Location.FlatNumber}");
        //            operation.Data = new byte[0];
        //            if (operation.Name == dbFlat.Agent && ChangeProperties(updFlat, dbFlat, operation)) {
        //                dbFlat.LastUpdateTime = DateTime.Now;
        //                realtyDB.SaveChanges();
        //                LogInfo($" has changed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} д{dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");
        //                operation.IsSuccessfully = true;
        //            } else operation.IsSuccessfully = false;
        //        }
        //    } catch (Exception ex) {
        //        LogError($"(ChangeFlat) {ex.Message}");
        //        operation.IsSuccessfully = false;
        //    }
        //}

        private Boolean FindDuplicate(Target target, Location location = null, Customer customer = null) {
            using (RealtyContext realtyDB = new RealtyContext()) {
                if (target == Target.Flat && realtyDB.Flats.Count() > 0)
                    return realtyDB.Flats.Local.Any(flat =>
                                                         flat.Location.City.Name == location.City.Name &&
                                                         flat.Location.Street.Name == location.Street.Name &&
                                                         flat.Location.District.Name == location.District.Name &&
                                                         flat.Location.HouseNumber == location.HouseNumber &&
                                                         flat.Location.FlatNumber == location.FlatNumber);
                else if (target == Target.House && realtyDB.Houses.Local.Count > 0)
                    return realtyDB.Houses.Local.Any(house =>
                                                          house.Location.City == location.City &&
                                                          house.Location.Street == location.Street &&
                                                          house.Location.HouseNumber == location.HouseNumber &&
                                                          house.Location.FlatNumber == location.FlatNumber);
                //else if (target == Target.Customer && realtyDB.Customers.Local.Count > 0)
                //    return realtyDB.Customers.Local.Any(cus =>
                //                                             cus.Name == customer.Name &&
                //                                             cus.PhoneNumbers == customer.PhoneNumbers);
                else return false;
            }
        }
        private void ClearDB() {
            using (RealtyContext realtyDB = new RealtyContext()) {
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Customers'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Albums'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Flats'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Houses'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Locations'");
                realtyDB.Albums.Local.Clear();
                realtyDB.Customers.Local.Clear();
                realtyDB.Flats.Local.Clear();
                realtyDB.Locations.Local.Clear();
                realtyDB.SaveChanges();
            }
        }
    }
}
