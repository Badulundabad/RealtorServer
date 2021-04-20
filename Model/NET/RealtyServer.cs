﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using NLog;
using RandomFlatGenerator;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtorServer.Model.DataBase;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        private System.Timers.Timer queueChecker = null;
        private RealtyContext realtyContext = new RealtyContext();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public RealtyServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.dispatcher = dispatcher;
            this.log = log;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
        }

        public void Run()
        {
            queueChecker = new System.Timers.Timer();
            queueChecker.Interval = 100;
            queueChecker.AutoReset = true;
            queueChecker.Elapsed += (o, e) => Handle();
            queueChecker.Start();
            logger.Info("Realty server has ran");
            UpdateLog("has ran");

            //DebugMethClear();
            //realtyContext.SaveChanges();
        }
        public override void Stop()
        {
            queueChecker.Stop();
            logger.Info("Realty server has stopped");
            UpdateLog("has stopped");
        }
        private void ClearDB()
        {
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Customers'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Albums'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Cities'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Districts'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Streets'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Flats'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Houses'");
            realtyContext.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Locations'");
            realtyContext.Albums.Local.Clear();
            realtyContext.Cities.Local.Clear();
            realtyContext.Customers.Local.Clear();
            realtyContext.Districts.Local.Clear();
            realtyContext.Flats.Local.Clear();
            realtyContext.Locations.Local.Clear();
            realtyContext.Streets.Local.Clear();
            realtyContext.SaveChanges();
        }
        private void Handle()
        {
            Operation operation = null;
            try
            {
                while (incomingQueue.Count > 0)
                {
                    operation = incomingQueue.Dequeue();
                    if (operation != null)
                    {
                        OperationType type = operation.OperationParameters.Type;
                        if (type == OperationType.Add)
                            operation = AddObject(operation);
                        else if (type == OperationType.Change)
                            operation = ChangeObject(operation);
                        else if (type == OperationType.Remove)
                            operation = RemoveObject(operation);
                        else if (type == OperationType.Update)
                            SendUpdate(operation);
                        else operation.IsSuccessfully = false;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(Handle) {ex.Message}");
                UpdateLog($"(Handle) {ex.Message}");
                if (operation != null)
                    operation.IsSuccessfully = false;
            }
            finally
            {
                if (operation != null)
                    outcomingQueue.Enqueue(operation);
            }
        }

        private Operation AddObject(Operation operation)
        {
            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                operation = AddFlat(operation);
            else if (target == TargetType.House)
                operation = AddHouse(operation);
            return operation;
        }
        private Operation ChangeObject(Operation operation)
        {
            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                operation = ChangeFlat(operation);
            else if (target == TargetType.House)
                operation = ChangeHouse(operation);
            return operation;
        }
        private Operation RemoveObject(Operation operation)
        {
            TargetType target = operation.OperationParameters.Target;
            if (target == TargetType.Flat)
                operation = RemoveFlat(operation);
            else if (target == TargetType.House)
                operation = RemoveHouse(operation);
            return operation;
        }

        private Operation AddFlat(Operation operation)
        {
            try
            {
                Flat newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                if (!FindDuplicate(TargetType.Flat, newFlat.Location))
                {
                    //добавить в бд
                    realtyContext.Flats.Local.Add(newFlat);
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has registered a flat {newFlat.Location.City} {newFlat.Location.District} {newFlat.Location.Street} {newFlat.Location.HouseNumber} кв{newFlat.Location.FlatNumber}");

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize<Flat>(newFlat);
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else
                {
                    operation.Data = "";
                    operation.IsSuccessfully = false;
                }
                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(AddFlat) {ex.Message}");
                UpdateLog($"(AddFlat) {ex.Message}");
                operation.Data = "";
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation AddHouse(Operation operation)
        {
            try
            {
                House newHouse = JsonSerializer.Deserialize<House>(operation.Data);
                if (!FindDuplicate(TargetType.House, newHouse.Location))
                {
                    //добавить в бд
                    realtyContext.Houses.Local.Add(newHouse);
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has registered a house {newHouse.Location.City} {newHouse.Location.District} {newHouse.Location.Street} {newHouse.Location.HouseNumber}");

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize<House>(newHouse);
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else
                    operation.IsSuccessfully = false;
                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(AddHouse) {ex.Message}");
                UpdateLog($"(AddHouse) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation ChangeFlat(Operation operation)
        {
            try
            {
                Flat updFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                Flat dbFlat = realtyContext.Flats.Find(updFlat.Id);
                if (dbFlat != null && updFlat != null && operation.Name == dbFlat.Agent)
                {
                    UpdateProperties(updFlat, dbFlat, operation);
                    dbFlat.LastUpdateTime = DateTime.Now;
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has changed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                    operation.Data = JsonSerializer.Serialize<Flat>(updFlat);
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(ChangeFlat) {ex.Message}");
                UpdateLog("(ChangeFlat) " + ex.Message);
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation ChangeHouse(Operation operation)
        {
            try
            {
                House updHouse = JsonSerializer.Deserialize<House>(operation.Data);
                House dbHouse = realtyContext.Houses.Find(updHouse.Id);
                if (dbHouse != null && updHouse != null && operation.Name == dbHouse.Agent)
                {
                    UpdateProperties(updHouse, dbHouse, operation);
                    dbHouse.LastUpdateTime = DateTime.Now;
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has changed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");

                    operation.Data = JsonSerializer.Serialize<House>(updHouse);
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(ChangeHouse) {ex.Message}");
                UpdateLog("(AddObject) " + ex.Message);
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation RemoveFlat(Operation operation)
        {
            try
            {
                Flat dbFlat = realtyContext.Flats.Find(operation.Data);
                if (dbFlat != null && operation.Name == dbFlat.Agent)
                {
                    realtyContext.Flats.Remove(dbFlat);
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has removed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");
                    operation.OperationParameters.Type = OperationType.Remove;
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(RemoveFlat) {ex.Message}");
                UpdateLog($"(RemoveFlat) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation RemoveHouse(Operation operation)
        {
            try
            {
                House dbHouse = realtyContext.Houses.Find(operation.Data);
                if (dbHouse != null && operation.Name == dbHouse.Agent)
                {
                    realtyContext.Houses.Remove(dbHouse);
                    realtyContext.SaveChanges();
                    logger.Info($"{operation.IpAddress} has removed a house {dbHouse.Location.City} {dbHouse.Location.District} {dbHouse.Location.Street} {dbHouse.Location.HouseNumber}");
                    operation.IsSuccessfully = true;
                    operation.OperationParameters.Type = OperationType.Remove;
                    operation.IpAddress = "broadcast";
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(RemoveHouse) {ex.Message}");
                UpdateLog($"(RemoveHouse) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Boolean FindDuplicate(TargetType target, Location location = null, Customer customer = null)
        {
            if (target == TargetType.Flat)
                return realtyContext.Flats.Local.Any(flat =>
                                                     flat.Location.City == location.City &&
                                                     flat.Location.Street == location.Street &&
                                                     flat.Location.District == location.District &&
                                                     flat.Location.HouseNumber == location.HouseNumber &&
                                                     flat.Location.FlatNumber == location.FlatNumber);
            else if (target == TargetType.House)
                return realtyContext.Houses.Local.Any(house =>
                                                      house.Location.City == location.City &&
                                                      house.Location.Street == location.Street &&
                                                      house.Location.HouseNumber == location.HouseNumber &&
                                                      house.Location.FlatNumber == location.FlatNumber);
            else if (target == TargetType.Customer)
                return realtyContext.Customers.Local.Any(cus =>
                                                         cus.Name == customer.Name &&
                                                         cus.PhoneNumbers == customer.PhoneNumbers);
            else throw new Exception("The target hasn't a right value");
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
            toObject.Album.PhotoList = fromObject.Album.PhotoList;
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
            //SendLists(operation);
            Boolean hasFlats = realtyContext.Flats.Local.Count > 0;
            //Boolean hasHouses = realtyContext.Houses.Local.Count > 0;
            Boolean hasHouses = false;

            if (operation.Data == "never")
            {
                SendAllObjects(operation);
                //SendAllPhotosAsync(operation, hasFlats, hasHouses);
            }
            else
            {
                SendMissingObjects(operation);
                SendMissingAlbumsAsync(operation, hasFlats, hasHouses);
            }
        }

        private void SendLists(Operation operation)
        {
            if (realtyContext.Cities.Local.Count > 0)
                SendCities(operation);
            if (realtyContext.Districts.Local.Count > 0)
                SendDistricts(operation);
            if (realtyContext.Streets.Local.Count > 0)
                SendStreets(operation);
            if (realtyContext.Customers.Local.Count > 0)
                SendCutomers(operation);
        }
        private void SendCities(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.City;
                operation.Data = JsonSerializer.Serialize(realtyContext.Cities.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
            }
        }
        private void SendDistricts(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.District;
                operation.Data = JsonSerializer.Serialize(realtyContext.Districts.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
            }
        }
        private void SendStreets(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.Street;
                operation.Data = JsonSerializer.Serialize(realtyContext.Streets.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
            }
        }
        private void SendCutomers(Operation operation)
        {
            try
            {
                operation.OperationParameters.Target = TargetType.Customer;
                operation.Data = JsonSerializer.Serialize(realtyContext.Customers.Local.ToArray());
                operation.IsSuccessfully = true;
            }
            catch
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
            }
        }

        private void SendAllObjects(Operation operation)
        {
            Boolean hasFlats = realtyContext.Flats.Local.Count > 0;
            Boolean hasHouses = realtyContext.Houses.Local.Count > 0;
            object[][] dbObjects = new object[2][];
            if (hasFlats)
            {
                dbObjects[0] = realtyContext.Flats.AsNoTracking().ToArray();
                foreach (Flat flat in dbObjects[0])
                    flat.Album.PhotoList = null;
            }
            if (hasHouses)
            {
                dbObjects[1] = realtyContext.Houses.AsNoTracking().ToArray();
                foreach (House house in dbObjects[1])
                    house.Album.PhotoList = null;
            }
            if (hasFlats || hasHouses)
            {
                operation.Data = JsonSerializer.Serialize(dbObjects);
                operation.IsSuccessfully = true;
            }
            else
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            operation.OperationParameters.Target = TargetType.All;
            outcomingQueue.Enqueue(operation);
        }
        private void SendAllFlats(Operation operation)
        {
            Flat[] flats = realtyContext.Flats.AsNoTracking().ToArray();
            foreach (Flat flat in flats)
                flat.Album.PhotoList = null;
            String dataJson = JsonSerializer.Serialize(flats);
            operation.Data = dataJson;
            operation.OperationParameters.Target = TargetType.Flat;
            outcomingQueue.Enqueue(operation);
        }
        private void SendAllHouses(Operation operation)
        {
            House[] houses = realtyContext.Houses.AsNoTracking().ToArray();
            foreach (House house in houses)
                house.Album.PhotoList = null;
            String dataJson = JsonSerializer.Serialize(houses);
            operation.Data = dataJson;
            operation.OperationParameters.Target = TargetType.House;
            outcomingQueue.Enqueue(operation);
        }
        private async void SendAllPhotosAsync(Operation operation, Boolean hasFlats, Boolean hasHouses)
        {
            await Task.Run(() =>
            {
                if (hasFlats)
                    foreach (Flat flat in realtyContext.Flats)
                    {
                        operation.Data = JsonSerializer.Serialize(flat.Album);
                        outcomingQueue.Enqueue(operation);
                    }
                if (hasHouses)
                    foreach (House house in realtyContext.Houses)
                    {
                        operation.Data = JsonSerializer.Serialize(house.Album);
                        outcomingQueue.Enqueue(operation);
                    }
                SendUpdateCompleteMessage(operation);
            });
        }

        private void SendMissingObjects(Operation operation)
        {
            DateTime lastUpdateTime = DateTime.Parse(operation.Data);
            Boolean hasFlats = realtyContext.Flats.Local.Count > 0;
            object[][] dbObjects = new object[2][];
            if (hasFlats)
            {
                dbObjects[0] = realtyContext.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= lastUpdateTime).ToArray();
                foreach (Flat flat in dbObjects[0])
                    flat.Album.PhotoList = null;
            }
            Boolean hasHouses = realtyContext.Houses.Local.Count > 0;
            if (hasHouses)
            {
                dbObjects[1] = realtyContext.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= lastUpdateTime).ToArray();
                foreach (House house in dbObjects[1])
                    house.Album.PhotoList = null;
            }
            if (hasFlats || hasHouses)
            {
                operation.Data = JsonSerializer.Serialize(dbObjects);
                operation.IsSuccessfully = true;
            }
            else
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
            }
            operation.OperationParameters.Target = TargetType.All;
            outcomingQueue.Enqueue(operation);
        }
        private void SendMissingFlats(Operation operation)
        {
            DateTime lastUpdateTime = DateTime.Parse(operation.Data);

            Flat[] missingFlats = realtyContext.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= lastUpdateTime).ToArray();
            foreach (Flat flat in missingFlats)
                flat.Album.PhotoList = null;

            operation.Data = JsonSerializer.Serialize(missingFlats);
            operation.OperationParameters.Target = TargetType.Flat;
            outcomingQueue.Enqueue(operation);
        }
        private void SendMissingHouses(Operation operation)
        {
            DateTime lastUpdateTime = DateTime.Parse(operation.Data);

            House[] missingHouses = realtyContext.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= lastUpdateTime).ToArray();
            foreach (House house in missingHouses)
                house.Album.PhotoList = null;

            operation.Data = JsonSerializer.Serialize(missingHouses);
            operation.OperationParameters.Target = TargetType.House;
            outcomingQueue.Enqueue(operation);
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
                        Flat[] missingFlats = realtyContext.Flats.AsNoTracking().Where(flat => flat.LastUpdateTime >= lastUpdateTime).ToArray();
                        foreach (Flat flat in missingFlats)
                        {
                            operation.Data = JsonSerializer.Serialize(flat.Album);
                            outcomingQueue.Enqueue(operation);
                        }
                    }
                    if (hasHouses)
                    {
                        House[] missingHouses = realtyContext.Houses.AsNoTracking().Where(house => house.LastUpdateTime >= lastUpdateTime).ToArray();
                        foreach (House house in missingHouses)
                        {
                            operation.Data = JsonSerializer.Serialize(house.Album);
                            outcomingQueue.Enqueue(operation);
                        }
                    }

                    SendUpdateCompleteMessage(operation);
                }
                catch (Exception ex)
                {
                    logger.Error($"Realty server(SendMissingAlbumsAsync) {ex.Message}");
                }
            });
        }

        private void SendUpdateCompleteMessage(Operation operation)
        {
            operation.Data = "Completed";
            outcomingQueue.Enqueue(operation);
        }


        private Operation SendFullUpdateTEST(Operation operation)
        {
            FlatGenerator generator = new FlatGenerator();
            Flat[] flats = generator.CreateFlatList(0, 3000).ToArray();
            String json = JsonSerializer.Serialize<Flat[]>(flats);
            operation.Data = json;
            outcomingQueue.Enqueue(operation);
            //Send a list of houses
            return null;
        }
    }
}
