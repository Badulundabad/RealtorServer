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
            //ClearDB();
            //Test();
            //Album album = new Album()
            //{
            //    PhotoList = new System.Collections.ObjectModel.ObservableCollection<byte[]>()
            //};
            //JsonSerializer.Serialize(album);
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
                        if (operation != null)
                        {
                            LogInfo($"has started to handle {operation.OperationNumber}");

                            OperationType type = operation.OperationParameters.Type;
                            if (type == OperationType.Add)
                                AddObject(operation);
                            else if (type == OperationType.Change)
                                ChangeObject(operation);
                            else if (type == OperationType.Remove)
                                RemoveObject(operation);
                            else if (type == OperationType.Update)
                                SendUpdate(operation);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(Handle) {ex.Message}");
                }
            }
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
            album.Serialize(list);
            album.Deserialize();
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

        private void AddObject(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} add");

            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                AddFlat(operation);
            else if (target == TargetType.House)
                AddHouse(operation);
        }
        private void ChangeObject(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} change");

            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                ChangeFlat(operation);
            else if (target == TargetType.House)
                ChangeHouse(operation);
        }
        private void RemoveObject(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} remove");

            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                RemoveFlat(operation);
            else if (target == TargetType.House)
                RemoveHouse(operation);
        }

        private void AddFlat(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} add flat");
            try
            {
                Flat newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                if (!FindDuplicate(TargetType.Flat, newFlat.Location))
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

                    List<Photo> photos = new List<Photo>();
                    newFlat.Album.Deserialize();

                    foreach (Byte[] data in newFlat.Album.PhotoCollection)
                    {
                        Photo photo = new Photo() { AlbumId = newFlat.Album.Id, Data = data };
                        photos.Add(photo);
                        realtyDB.Photos.Local.Add(photo);
                    }
                    newFlat.Album.UpdateKeys(photos);

                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has registered a flat {newFlat.Location.City.Name} {newFlat.Location.District.Name} {newFlat.Location.Street.Name} {newFlat.Location.HouseNumber} кв{newFlat.Location.FlatNumber}");

                    operation.Data = JsonSerializer.Serialize<Flat>(newFlat);
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else
                    operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                LogError($"(AddFlat) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void AddHouse(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} add house");

            try
            {
                House newHouse = JsonSerializer.Deserialize<House>(operation.Data);
                if (!FindDuplicate(TargetType.House, newHouse.Location))
                {
                    //добавить в бд
                    realtyDB.Houses.Local.Add(newHouse);
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has registered a house {newHouse.Location.City.Name} {newHouse.Location.District.Name} {newHouse.Location.Street.Name} {newHouse.Location.HouseNumber}");

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize<House>(newHouse);
                    operation.IpAddress = "broadcast";
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
        private void ChangeFlat(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} change flat");
            try
            {
                Flat updFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                Flat dbFlat = realtyDB.Flats.Find(updFlat.Id);
                if (dbFlat != null && updFlat != null && operation.Name == dbFlat.Agent)
                {
                    UpdateProperties(updFlat, dbFlat, operation);
                    dbFlat.LastUpdateTime = DateTime.Now;
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has changed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                    operation.Data = JsonSerializer.Serialize<Flat>(updFlat);
                    operation.IpAddress = "broadcast";
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
        private void ChangeHouse(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} change house");
            try
            {
                House updHouse = JsonSerializer.Deserialize<House>(operation.Data);
                House dbHouse = realtyDB.Houses.Find(updHouse.Id);
                if (dbHouse != null && updHouse != null && operation.Name == dbHouse.Agent)
                {
                    UpdateProperties(updHouse, dbHouse, operation);
                    dbHouse.LastUpdateTime = DateTime.Now;
                    realtyDB.SaveChanges();
                    LogInfo($"{operation.IpAddress} has changed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");

                    operation.Data = JsonSerializer.Serialize<House>(updHouse);
                    operation.IpAddress = "broadcast";
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
        private void RemoveFlat(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} remove flat");
            try
            {
                Flat dbFlat = realtyDB.Flats.Find(operation.Data);
                if (dbFlat != null && operation.Name == dbFlat.Agent)
                {
                    realtyDB.Flats.Remove(dbFlat);
                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has removed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                    operation.OperationParameters.Type = OperationType.Remove;
                    operation.IpAddress = "broadcast";
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
        private void RemoveHouse(Operation operation)
        {
            LogInfo($"{operation.OperationNumber} remove house");
            try
            {
                House dbHouse = realtyDB.Houses.Find(operation.Data);
                if (dbHouse != null && operation.Name == dbHouse.Agent)
                {
                    realtyDB.Houses.Remove(dbHouse);
                    realtyDB.SaveChanges();

                    LogInfo($"{operation.IpAddress} has removed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");

                    operation.IsSuccessfully = true;
                    operation.OperationParameters.Type = OperationType.Remove;
                    operation.IpAddress = "broadcast";
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
        private Boolean FindDuplicate(TargetType target, Location location = null, Customer customer = null)
        {
            if (target == TargetType.Flat && realtyDB.Flats.Count() > 0)
                return realtyDB.Flats.Local.Any(flat =>
                                                     flat.Location.City.Name == location.City.Name &&
                                                     flat.Location.Street.Name == location.Street.Name &&
                                                     flat.Location.District.Name == location.District.Name &&
                                                     flat.Location.HouseNumber == location.HouseNumber &&
                                                     flat.Location.FlatNumber == location.FlatNumber);
            else if (target == TargetType.House && realtyDB.Houses.Local.Count > 0)
                return realtyDB.Houses.Local.Any(house =>
                                                      house.Location.City == location.City &&
                                                      house.Location.Street == location.Street &&
                                                      house.Location.HouseNumber == location.HouseNumber &&
                                                      house.Location.FlatNumber == location.FlatNumber);
            else if (target == TargetType.Customer && realtyDB.Customers.Local.Count > 0)
                return realtyDB.Customers.Local.Any(cus =>
                                                         cus.Name == customer.Name &&
                                                         cus.PhoneNumbers == customer.PhoneNumbers);
            else return false;
        }

        private void UpdateProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, Operation operation)
        {
            if (operation.OperationParameters.HasBaseChanges)
                UpdateBaseProperties(fromObject, toObject, operation.OperationParameters.Target);
            if (operation.OperationParameters.HasAlbumChanges)
                UpdateAlbum(fromObject, toObject);
            if (operation.OperationParameters.HasCustomerChanges)
                UpdateCustomer(fromObject, toObject);
            if (operation.OperationParameters.HasLocationChanges)
                UpdateLocation(fromObject, toObject, operation.OperationParameters.Target);
        }
        private void UpdateBaseProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, TargetType target)
        {
            if (target == TargetType.Flat)
                ((Flat)toObject).Info = ((Flat)fromObject).Info;
            else if (target == TargetType.House)
                ((House)toObject).Info = ((House)fromObject).Info;
            toObject.Cost = fromObject.Cost;
            toObject.HasExclusive = fromObject.HasExclusive;
            toObject.IsSold = fromObject.IsSold;
        }
        private void UpdateAlbum(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {
            toObject.Album.Preview = fromObject.Album.Preview;
            //toObject.Album.PhotoList = fromObject.Album.PhotoList;
        }
        private void UpdateCustomer(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {
            toObject.Customer.Name = fromObject.Customer.Name;
            toObject.Customer.PhoneNumbers = fromObject.Customer.PhoneNumbers;
        }
        private void UpdateLocation(BaseRealtorObject fromObject, BaseRealtorObject toObject, TargetType type)
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
            if (type == TargetType.Flat)
                toObject.Location.FlatNumber = fromObject.Location.FlatNumber;
            toObject.Location.HouseNumber = fromObject.Location.HouseNumber;
            toObject.Location.HasBanner = fromObject.Location.HasBanner;
            toObject.Location.HasExchange = fromObject.Location.HasExchange;
        }

        private void SendUpdate(Operation operation)
        {
            SendFullUpdate(operation);
            SendAllPhotosAsync(operation);
            SendUpdateCompleteMessage(operation);
            //if (operation.Data == "never")
            //{
            //    LogInfo($"{operation.OperationNumber} has requested a full update");
            //    SendFullUpdate(operation);
            //    //SendAllPhotosAsync(operation, hasFlats, hasHouses);
            //}
            //else
            //{
            //    LogInfo($"{operation.OperationNumber} has requested a partial full update");
            //    SendPartialUpdate(operation);
            //    //SendMissingAlbumsAsync(operation);
            //}
        }

        private void SendLists(Operation operation)
        {
            if (realtyDB.Cities.Local.Count > 0)
                SendCities(operation);
            if (realtyDB.Districts.Local.Count > 0)
                SendDistricts(operation);
            if (realtyDB.Streets.Local.Count > 0)
                SendStreets(operation);
            if (realtyDB.Customers.Local.Count > 0)
                SendCutomers(operation);
        }
        private void SendCities(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.City;
                operation.Data = JsonSerializer.Serialize(realtyDB.Cities.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                base.OutcomingOperations.Enqueue(operation);
            }
        }
        private void SendDistricts(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.District;
                operation.Data = JsonSerializer.Serialize(realtyDB.Districts.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                base.OutcomingOperations.Enqueue(operation);
            }
        }
        private void SendStreets(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.Street;
                operation.Data = JsonSerializer.Serialize(realtyDB.Streets.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                OutcomingOperations.Enqueue(operation);
            }
        }
        private void SendCutomers(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.Customer;
                operation.Data = JsonSerializer.Serialize(realtyDB.Customers.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                OutcomingOperations.Enqueue(operation);
            }
        }

        private void SendFullUpdate(Operation operation)
        {
            try
            {
                String[] dbObjects = new String[2];
                if (realtyDB.Flats.Local.Count > 0)
                {
                    Flat[] flats = realtyDB.Flats.ToArray();
                    dbObjects[0] = JsonSerializer.Serialize(flats);
                }
                if (realtyDB.Houses.Local.Count > 0)
                {
                    House[] houses = realtyDB.Houses.AsNoTracking().ToArray();
                    dbObjects[1] = JsonSerializer.Serialize(houses);
                }

                operation.Data = JsonSerializer.Serialize(dbObjects);
                operation.IsSuccessfully = true;
                operation.OperationParameters.Target = TargetType.All;

                LogInfo($"{operation.OperationNumber} the full update has prepared");
            }
            catch (Exception ex)
            {
                LogError($"(SendFullUpdate) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void SendAllFlats(Operation operation)
        {
            Flat[] flats = realtyDB.Flats.AsNoTracking().ToArray();
            String dataJson = JsonSerializer.Serialize(flats);
            operation.Data = dataJson;
            operation.OperationParameters.Target = TargetType.Flat;
            OutcomingOperations.Enqueue(operation);
        }
        private void SendAllHouses(Operation operation)
        {
            House[] houses = realtyDB.Houses.AsNoTracking().ToArray();
            String dataJson = JsonSerializer.Serialize(houses);
            operation.Data = dataJson;
            operation.OperationParameters.Target = TargetType.House;
            OutcomingOperations.Enqueue(operation);
        }
        private void SendAllPhotosAsync(Operation operation)
        {
            if (realtyDB.Photos.Local.Count > 0)
            {
                operation.Data = JsonSerializer.Serialize(realtyDB.Photos.ToArray());
                operation.OperationParameters.Target = TargetType.Album;
                operation.IsSuccessfully = true;
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }

        private void SendPartialUpdate(Operation operation)
        {
            try
            {
                DateTime dt = Convert.ToDateTime(operation.Data, CultureInfo.InvariantCulture);
                String[] dbObjects = new String[2];
                if (realtyDB.Flats.Local.Count > 0)
                {
                    Flat[] flats = realtyDB.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= dt).ToArray();
                    dbObjects[0] = JsonSerializer.Serialize(flats);
                }
                if (realtyDB.Houses.Local.Count > 0)
                {
                    House[] houses = realtyDB.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= dt).ToArray();
                    dbObjects[1] = JsonSerializer.Serialize(houses);
                }

                operation.Data = JsonSerializer.Serialize(dbObjects);
                operation.IsSuccessfully = true;
                operation.OperationParameters.Target = TargetType.All;

                LogInfo($"{operation.OperationNumber} the partial update has prepared");
            }
            catch (Exception ex)
            {
                LogError($"(SendPartialUpdate) {ex.Message}");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void SendMissingFlats(Operation operation)
        {
            DateTime lastUpdateTime = DateTime.Parse(operation.Data);

            Flat[] missingFlats = realtyDB.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= lastUpdateTime).ToArray();
            operation.Data = JsonSerializer.Serialize(missingFlats);
            operation.OperationParameters.Target = TargetType.Flat;
            OutcomingOperations.Enqueue(operation);
        }
        private void SendMissingHouses(Operation operation)
        {
            DateTime lastUpdateTime = DateTime.Parse(operation.Data);

            House[] missingHouses = realtyDB.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= lastUpdateTime).ToArray();
            operation.Data = JsonSerializer.Serialize(missingHouses);
            operation.OperationParameters.Target = TargetType.House;
            OutcomingOperations.Enqueue(operation);
        }
        private async void SendMissingAlbumsAsync(Operation operation, Boolean hasFlats, Boolean hasHouses)
        {
            await Task.Run(() =>
            {
                try
                {
                    DateTime lastUpdateTime = DateTime.Parse(operation.Data);
                    operation.OperationParameters.Target = TargetType.Album;

                    if (hasFlats)
                    {
                        Flat[] missingFlats = realtyDB.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= lastUpdateTime).ToArray();
                        foreach (Flat flat in missingFlats)
                        {
                            operation.Data = JsonSerializer.Serialize(flat.Album);
                            OutcomingOperations.Enqueue(operation);
                        }
                    }
                    if (hasHouses)
                    {
                        House[] missingHouses = realtyDB.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= lastUpdateTime).ToArray();
                        foreach (House house in missingHouses)
                        {
                            operation.Data = JsonSerializer.Serialize(house.Album);
                            OutcomingOperations.Enqueue(operation);
                        }
                    }

                    SendUpdateCompleteMessage(operation);
                }
                catch (Exception ex)
                {
                    LogError($"(SendMissingAlbumsAsync) {ex.Message}");
                }
            });
        }

        private void SendUpdateCompleteMessage(Operation operation)
        {
            operation.OperationParameters.Target = TargetType.None;
            operation.Data = "completed";
            OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
        }
    }
}
