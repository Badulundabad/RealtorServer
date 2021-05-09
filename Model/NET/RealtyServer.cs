using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtorServer.Model.DataBase;
using System.Threading.Tasks;
using System.Globalization;
using System.Diagnostics;
using NLog;
using static RealtorServer.Model.Event.EventHandlers;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;
using RealtyModel.Model.RealtyObjects;
using System.Data.Entity;
using RealtyModel.Service;
using System.Text;
using RealtyModel.Model.Operations;
using RealtorServer.Model.Event;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        private RealtyContext realtyDB = new RealtyContext();
        public event OperationHandledEventHandler OperationHandled;

        public RealtyServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Dispatcher = dispatcher;
            OutcomingOperations = new OperationQueue();
            IncomingOperations = new OperationQueue();
            IncomingOperations.Enqueued += (s, e) => Handle();
        }

        private void Test()
        {
            List<Photo> list = new List<Photo>();
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "Файлы изображений (*.BMP; *.JPG; *.JPEG; *.PNG) | *.BMP; *.JPG; *.JPEG; *.PNG",
                Multiselect = true,
                Title = "Выбрать фотографии"
            };
            if (openFileDialog.ShowDialog() == true)
                foreach (string fileName in openFileDialog.FileNames)
                    list.Add(new Photo() { Data = File.ReadAllBytes(fileName) });
            realtyDB.Photos.AddRange(list);
            realtyDB.SaveChanges();

            Album album = new Album();
        }
        private void ClearDB()
        {
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Customers'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Albums'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Cities'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Districts'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Streets'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Flats'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Houses'");
            realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Locations'");
            realtyDB.Albums.Local.Clear();
            realtyDB.Cities.Local.Clear();
            realtyDB.Customers.Local.Clear();
            realtyDB.Districts.Local.Clear();
            realtyDB.Flats.Local.Clear();
            realtyDB.Locations.Local.Clear();
            realtyDB.Streets.Local.Clear();
            realtyDB.SaveChanges();
        }

        private void Handle()
        {
            lock (handleLocker)
            {
                try
                {
                    while (IncomingOperations.Count > 0)
                    {
                        Operation operation = IncomingOperations.Dequeue();
                        Act action = operation.Parameters.Action;
                        LogInfo($"has started to handle {operation.Number}");
                        if (action == Act.Add)
                            AddObject(operation);
                        else if (action == Act.Change)
                            ChangeObject(operation);
                        else if (action == Act.Delete)
                            RemoveObject(operation);
                        //else if (action == Act.Request)
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(Handle) {ex.Message}");
                }
            }
        }
        private void AddObject(Operation operation)
        {
            LogInfo($"{operation.Number} add");
            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                AddFlat(operation);
            else if (target == Target.House)
                AddHouse(operation);
            else if (target == Target.Photo)
                AddPhoto(operation);
        }
        private void ChangeObject(Operation operation)
        {
            LogInfo($"{operation.Number} change");

            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                ChangeFlat(operation);
            else if (target == Target.House)
                ChangeHouse(operation);
        }
        private void RemoveObject(Operation operation)
        {
            LogInfo($"{operation.Number} remove");

            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                RemoveFlat(operation);
            else if (target == Target.House)
                RemoveHouse(operation);
        }

        private void AddFlat(Operation operation)
        {
            LogInfo($"{operation.Number} add flat");
            try
            {
                Flat newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                if (!FindDuplicate(Target.Flat, newFlat.Location))
                {
                    if (newFlat.AlbumId > 0)
                        newFlat.Album = realtyDB.Albums.Find(newFlat.AlbumId);
                    if (newFlat.CustomerId > 0)
                        newFlat.Customer = realtyDB.Customers.Find(newFlat.CustomerId);
                    if (newFlat.Location.CityId > 0)
                        newFlat.Location.City = realtyDB.Cities.Find(newFlat.Location.CityId);
                    if (newFlat.Location.DistrictId > 0)
                        newFlat.Location.District = realtyDB.Districts.Find(newFlat.Location.DistrictId);
                    if (newFlat.Location.StreetId > 0)
                        newFlat.Location.Street = realtyDB.Streets.Find(newFlat.Location.StreetId);

                    newFlat.RegistrationDate = DateTime.Now;
                    newFlat.LastUpdateTime = DateTime.Now;

                    realtyDB.Flats.Local.Add(newFlat);
                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has registered a flat {newFlat.Location.City.Name} {newFlat.Location.District.Name} {newFlat.Location.Street.Name} {newFlat.Location.HouseNumber} кв{newFlat.Location.FlatNumber}");

                    operation.Data = JsonSerializer.Serialize(newFlat);
                    operation.IsBroadcast = true;
                    operation.IsSuccessfully = true;
                }
                else
                {
                    LogInfo($"{operation.Number} there is a flat with such address");
                    operation.Data = null;
                    operation.IsSuccessfully = false;
                }
            }
            catch (Exception ex)
            {
                LogError($"(AddFlat) {ex.Message}\n\n");
                LogError($"(AddFlat) {operation.Data}\n\n");
                operation.Data = null;
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
            }
        }
        private void AddPhoto(Operation operation)
        {
            try
            {
                String[] data = operation.Data.Split(new String[] { "<GUID>" }, StringSplitOptions.None);
                Photo photo = JsonSerializer.Deserialize<Photo>(data[1]);
                realtyDB.Photos.Local.Add(photo);
                realtyDB.SaveChanges();
                operation.Data = data[0];
                operation.IsSuccessfully = true;
                LogInfo($"SAVED A PHOTO {operation.Number}");
            }
            catch (Exception ex)
            {
                LogError($"(AddPhoto) {ex.Message}\n\n");
                operation.Data = null;
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
            }
        }
        private void ChangeFlat(Operation operation)
        {
            LogInfo($"{operation.Number} change flat");
            try
            {
                Flat updFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                Flat dbFlat = realtyDB.Flats.Find(updFlat.Id);
                if (dbFlat != null && updFlat != null && operation.Name == dbFlat.Agent)
                {
                    ChangeProperties(updFlat, dbFlat, operation);
                    dbFlat.LastUpdateTime = DateTime.Now;
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has changed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                    operation.Data = JsonSerializer.Serialize(updFlat);
                    operation.IsBroadcast = true;
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(ChangeFlat) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void RemoveFlat(Operation operation)
        {
            LogInfo($"{operation.Number} remove flat");
            try
            {
                Flat dbFlat = realtyDB.Flats.Find(operation.Data);
                if (dbFlat != null && operation.Name == dbFlat.Agent)
                {
                    realtyDB.Flats.Remove(dbFlat);
                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has removed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                    operation.Parameters.Action = Act.Delete;
                    operation.IsBroadcast = true;
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(RemoveFlat) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }

        private void ChangeProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, Operation operation)
        {
            if (operation.Parameters.HasBaseChanges)
                ChangeBaseProperties(fromObject, toObject, operation.Parameters.Target);
            if (operation.Parameters.HasAlbumChanges)
                ChangeAlbum(fromObject, toObject);
            if (operation.Parameters.HasCustomerChanges)
                ChangeCustomer(fromObject, toObject);
            if (operation.Parameters.HasLocationChanges)
                ChangeLocation(fromObject, toObject, operation.Parameters.Target);
        }
        private void ChangeBaseProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, Target target)
        {
            if (target == Target.Flat)
                ((Flat)toObject).Info = ((Flat)fromObject).Info;
            else if (target == Target.House)
                ((House)toObject).Info = ((House)fromObject).Info;
            toObject.Cost = fromObject.Cost;
            toObject.HasExclusive = fromObject.HasExclusive;
            toObject.IsSold = fromObject.IsSold;
        }
        private void ChangeAlbum(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {
            //toObject.Album.Preview = fromObject.Album.Preview;
            //toObject.Album.PhotoList = fromObject.Album.PhotoList;
        }
        private void ChangeCustomer(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {
            toObject.Customer.Name = fromObject.Customer.Name;
            toObject.Customer.PhoneNumbers = fromObject.Customer.PhoneNumbers;
        }
        private void ChangeLocation(BaseRealtorObject fromObject, BaseRealtorObject toObject, Target type)
        {
            if (fromObject.Location.City.Id == 0)
                toObject.Location.City = fromObject.Location.City;
            else
            {
                toObject.Location.CityId = fromObject.Location.CityId;
                toObject.Location.City.Id = fromObject.Location.City.Id;
                toObject.Location.City.Name = fromObject.Location.City.Name;
            }
            if (fromObject.Location.District.Id == 0)
                toObject.Location.District = fromObject.Location.District;
            else
            {
                toObject.Location.DistrictId = fromObject.Location.DistrictId;
                toObject.Location.District.Id = fromObject.Location.District.Id;
                toObject.Location.District.Name = fromObject.Location.District.Name;
            }
            if (fromObject.Location.Street.Id == 0)
                toObject.Location.Street = fromObject.Location.Street;
            else
            {
                toObject.Location.StreetId = fromObject.Location.StreetId;
                toObject.Location.Street.Id = fromObject.Location.Street.Id;
                toObject.Location.Street.Name = fromObject.Location.Street.Name;
            }
            if (type == Target.Flat)
                toObject.Location.FlatNumber = fromObject.Location.FlatNumber;
            toObject.Location.HouseNumber = fromObject.Location.HouseNumber;
            toObject.Location.HasBanner = fromObject.Location.HasBanner;
            toObject.Location.HasExchange = fromObject.Location.HasExchange;
        }
        private void AddHouse(Operation operation)
        {
            LogInfo($"{operation.Number} add house");

            try
            {
                House newHouse = JsonSerializer.Deserialize<House>(operation.Data);
                if (!FindDuplicate(Target.House, newHouse.Location))
                {
                    //добавить в бд
                    realtyDB.Houses.Local.Add(newHouse);
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has registered a house {newHouse.Location.City.Name} {newHouse.Location.District.Name} {newHouse.Location.Street.Name} {newHouse.Location.HouseNumber}");

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize(newHouse);
                    operation.IsBroadcast = true;
                    operation.IsSuccessfully = true;
                }
                else
                    operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(AddHouse) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void ChangeHouse(Operation operation)
        {
            LogInfo($"{operation.Number} change house");
            try
            {
                House updHouse = JsonSerializer.Deserialize<House>(operation.Data);
                House dbHouse = realtyDB.Houses.Find(updHouse.Id);
                if (dbHouse != null && updHouse != null && operation.Name == dbHouse.Agent)
                {
                    ChangeProperties(updHouse, dbHouse, operation);
                    dbHouse.LastUpdateTime = DateTime.Now;
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has changed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");

                    //operation.Data = JsonSerializer.SerializeToUtf8Bytes(updHouse);
                    operation.IsBroadcast = true;
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(ChangeHouse) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void RemoveHouse(Operation operation)
        {
            LogInfo($"{operation.Number} remove house");
            try
            {
                House dbHouse = realtyDB.Houses.Find(operation.Data);
                if (dbHouse != null && operation.Name == dbHouse.Agent)
                {
                    realtyDB.Houses.Remove(dbHouse);
                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has removed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");

                    operation.IsSuccessfully = true;
                    operation.Parameters.Action = Act.Delete;
                    operation.IsBroadcast = true;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(RemoveHouse) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }

        private Boolean FindDuplicate(Target target, Location location = null, Customer customer = null)
        {
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
}
