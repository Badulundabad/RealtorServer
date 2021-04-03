using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using System.Data.Entity;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using System.Threading;
using RealtyModel.Model.Base;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        private System.Timers.Timer queueChecker = null;
        private RealtyContext realtyContext = new RealtyContext();

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
            UpdateLog("has ran");

            Clear();
            realtyContext.SaveChanges();
        }
        public override void Stop()
        {
            queueChecker.Stop();
            UpdateLog("has stopped");
        }
        private void Clear()
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
            Operation newOperation = null;
            try
            {
                while (incomingQueue.Count > 0)
                {
                    newOperation = incomingQueue.Dequeue();
                    if (newOperation != null)
                    {
                        switch (newOperation.OperationParameters.Type)
                        {
                            case OperationType.Add:
                                {
                                    if (newOperation.OperationParameters.Target == TargetType.Flat)
                                        newOperation = AddFlat(newOperation);
                                    else if (newOperation.OperationParameters.Target == TargetType.House)
                                        newOperation = AddHouse(newOperation);
                                    else newOperation.IsSuccessfully = false;
                                    break;
                                }
                            case OperationType.Change:
                                {
                                    if (newOperation.OperationParameters.Target == TargetType.Flat)
                                        newOperation = ChangeFlat(newOperation);
                                    else if (newOperation.OperationParameters.Target == TargetType.House)
                                        newOperation = ChangeHouse(newOperation);
                                    else newOperation.IsSuccessfully = false;
                                    break;
                                }
                            case OperationType.Remove:
                                {
                                    if (newOperation.OperationParameters.Target == TargetType.Flat)
                                        newOperation = RemoveFlat(newOperation);
                                    else if (newOperation.OperationParameters.Target == TargetType.House)
                                        newOperation = RemoveHouse(newOperation);
                                    else newOperation.IsSuccessfully = false;
                                    break;
                                }
                            case OperationType.Update:
                                {
                                    if (newOperation.OperationParameters.Target == TargetType.All)
                                        SendFullUpdate(newOperation);
                                    break;
                                }
                            default:
                                {
                                    newOperation.IsSuccessfully = false;
                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateLog($"(Handle) {ex.Message}");
                if (newOperation != null) newOperation.IsSuccessfully = false;
            }
            finally
            {
                if (newOperation != null)
                {
                    outcomingQueue.Enqueue(newOperation);
                }
            }
        }

        //There need to build some methods
        //Add an object
        //Change an object
        //Delete an object
        //MAYBE Get an object
        //Update on all clients
        private Operation AddFlat(Operation operation)
        {
            Flat newFlat = null;
            try
            {
                newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                if (!FindDuplicate(TargetType.Flat, newFlat.Location))
                {
                    //добавить в бд
                    realtyContext.Flats.Local.Add(newFlat);
                    realtyContext.SaveChanges();

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize<Flat>(newFlat);
                    operation.OperationParameters.Type = OperationType.Update;
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation AddHouse(Operation operation)
        {
            House newHouse = null;
            try
            {
                newHouse = JsonSerializer.Deserialize<House>(operation.Data);
                if (!FindDuplicate(TargetType.House, newHouse.Location))
                {
                    //добавить в бд
                    realtyContext.Houses.Local.Add(newHouse);
                    realtyContext.SaveChanges();

                    //отправить всем клиентам обновление
                    operation.Data = JsonSerializer.Serialize<House>(newHouse);
                    operation.OperationParameters.Type = OperationType.Update;
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog($"(AddObject) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation ChangeFlat(Operation operation)
        {
            Flat updFlat = null;
            Flat dbFlat = null;
            try
            {
                updFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                dbFlat = realtyContext.Flats.Find(updFlat.Id);

                if (dbFlat != null && updFlat != null && operation.Name == dbFlat.Agent && UpdateProperties())
                {
                    realtyContext.SaveChanges();

                    operation.Data = JsonSerializer.Serialize<Flat>(updFlat);
                    operation.OperationParameters.Type = OperationType.Update;
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
                operation.IsSuccessfully = false;
                return operation;
            }
            Boolean UpdateProperties()
            {
                try
                {
                    if (operation.OperationParameters.HasBaseChanges)
                    {
                        dbFlat.Info = updFlat.Info;
                        dbFlat.Cost = updFlat.Cost;
                        dbFlat.HasExclusive = updFlat.HasExclusive;
                        dbFlat.IsSold = updFlat.IsSold;

                        //TEMPORARY!!!!
                        dbFlat.Agent = updFlat.Agent;
                    }
                    if (operation.OperationParameters.HasAlbumChanges)
                    {
                        dbFlat.Album.Preview = updFlat.Album.Preview;
                        dbFlat.Album.PhotoList = updFlat.Album.PhotoList;
                    }
                    if (operation.OperationParameters.HasCustomerChanges)
                    {
                        dbFlat.Customer.Name = updFlat.Customer.Name;
                        dbFlat.Customer.PhoneNumbers = updFlat.Customer.PhoneNumbers;
                    }
                    if (operation.OperationParameters.HasLocationChanges)
                    {
                        if (updFlat.Location.City.Id == 0)
                            dbFlat.Location.City = updFlat.Location.City;
                        else
                        {
                            dbFlat.Location.CityId = updFlat.Location.CityId;
                            dbFlat.Location.City.Id = updFlat.Location.City.Id;
                            dbFlat.Location.City.Name = updFlat.Location.City.Name;
                        }
                        if (updFlat.Location.District.Id == 0)
                            dbFlat.Location.District = updFlat.Location.District;
                        else
                        {
                            dbFlat.Location.DistrictId = updFlat.Location.DistrictId;
                            dbFlat.Location.District.Id = updFlat.Location.District.Id;
                            dbFlat.Location.District.Name = updFlat.Location.District.Name;
                        }
                        if (updFlat.Location.Street.Id == 0)
                            dbFlat.Location.Street = updFlat.Location.Street;
                        else
                        {
                            dbFlat.Location.StreetId = updFlat.Location.StreetId;
                            dbFlat.Location.Street.Id = updFlat.Location.Street.Id;
                            dbFlat.Location.Street.Name = updFlat.Location.Street.Name;
                        }
                        dbFlat.Location.HouseNumber = updFlat.Location.HouseNumber;
                        dbFlat.Location.FlatNumber = updFlat.Location.FlatNumber;
                        dbFlat.Location.HasBanner = updFlat.Location.HasBanner;
                        dbFlat.Location.HasExchange = updFlat.Location.HasExchange;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    UpdateLog($"(UpdateProperties) {ex.Message}");
                    return false;
                }
            }
        }
        private Operation ChangeHouse(Operation operation)
        {
            House updHouse = null;
            House dbHouse = null;
            try
            {
                updHouse = JsonSerializer.Deserialize<House>(operation.Data);
                dbHouse = realtyContext.Houses.Find(updHouse.Id);
                if (dbHouse != null && updHouse != null && operation.Name == dbHouse.Agent && UpdateProperties())
                {
                    realtyContext.SaveChanges();

                    operation.Data = JsonSerializer.Serialize<House>(updHouse);
                    operation.OperationParameters.Type = OperationType.Update;
                    operation.IpAddress = "broadcast";
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;

                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
                operation.IsSuccessfully = false;
                return operation;
            }
            Boolean UpdateProperties()
            {
                try
                {
                    if (operation.OperationParameters.HasBaseChanges)
                    {
                        dbHouse.Info = updHouse.Info;
                        dbHouse.Cost = updHouse.Cost;
                        dbHouse.HasExclusive = updHouse.HasExclusive;
                        dbHouse.IsSold = updHouse.IsSold;

                        //TEMPORARY!!!!
                        dbHouse.Agent = updHouse.Agent;
                    }
                    if (operation.OperationParameters.HasAlbumChanges)
                    {
                        dbHouse.Album.Preview = updHouse.Album.Preview;
                        dbHouse.Album.PhotoList = updHouse.Album.PhotoList;
                    }
                    if (operation.OperationParameters.HasCustomerChanges)
                    {
                        dbHouse.Customer.Name = updHouse.Customer.Name;
                        dbHouse.Customer.PhoneNumbers = updHouse.Customer.PhoneNumbers;
                    }
                    if (operation.OperationParameters.HasLocationChanges)
                    {
                        if (updHouse.Location.City.Id == 0)
                            dbHouse.Location.City = updHouse.Location.City;
                        else
                        {
                            dbHouse.Location.CityId = updHouse.Location.CityId;
                            dbHouse.Location.City.Id = updHouse.Location.City.Id;
                            dbHouse.Location.City.Name = updHouse.Location.City.Name;
                        }
                        if (updHouse.Location.District.Id == 0)
                            dbHouse.Location.District = updHouse.Location.District;
                        else
                        {
                            dbHouse.Location.DistrictId = updHouse.Location.DistrictId;
                            dbHouse.Location.District.Id = updHouse.Location.District.Id;
                            dbHouse.Location.District.Name = updHouse.Location.District.Name;
                        }
                        if (updHouse.Location.Street.Id == 0)
                            dbHouse.Location.Street = updHouse.Location.Street;
                        else
                        {
                            dbHouse.Location.StreetId = updHouse.Location.StreetId;
                            dbHouse.Location.Street.Id = updHouse.Location.Street.Id;
                            dbHouse.Location.Street.Name = updHouse.Location.Street.Name;
                        }
                        dbHouse.Location.HouseNumber = updHouse.Location.HouseNumber;
                        dbHouse.Location.FlatNumber = updHouse.Location.FlatNumber;
                        dbHouse.Location.HasBanner = updHouse.Location.HasBanner;
                        dbHouse.Location.HasExchange = updHouse.Location.HasExchange;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    UpdateLog($"(UpdateProperties) {ex.Message}");
                    return false;
                }
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
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog($"(RemoveHouse) {ex.Message}");
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
                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
                return operation;
            }
            catch (Exception ex)
            {
                UpdateLog($"(RemoveHouse) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private void SendFullUpdate(Operation operation)
        {
            //Send a list of customers
            Customer[] customers = realtyContext.Customers.Local.ToArray();
            operation.Data = JsonSerializer.Serialize(customers);
            operation.OperationParameters.Target = TargetType.Customer;
            outcomingQueue.Enqueue(operation);
            //Send a list of flats
            Flat[] flats = realtyContext.Flats.Local.ToArray();
            operation.OperationParameters.Target = TargetType.Flat;
            foreach (Flat flat in flats)
            {
                operation.Data = JsonSerializer.Serialize(flat);
                outcomingQueue.Enqueue(operation);
            }
            //Send a list of houses
        }
        private Boolean FindDuplicate(TargetType targetType, Location location = null, Customer customer = null, Album album = null)
        {
            Boolean result = true;
            switch (targetType)
            {
                case TargetType.Flat:
                    {
                        if (realtyContext.Flats.Local.FirstOrDefault<Flat>(flat =>
                            flat.Location.City == location.City
                            && flat.Location.Street == location.Street
                            && flat.Location.District == location.District
                            && flat.Location.HouseNumber == location.HouseNumber
                            && flat.Location.FlatNumber == location.FlatNumber) == null)
                            result = false;
                        break;
                    }
                case TargetType.House:
                    {
                        if (realtyContext.Houses.Local.First<House>(house =>
                            house.Location.City == location.City &&
                            house.Location.Street == location.Street &&
                            house.Location.HouseNumber == location.HouseNumber &&
                            house.Location.FlatNumber == location.FlatNumber) == null)
                            result = false;
                        break;
                    }
                case TargetType.Customer:
                    {
                        if (realtyContext.Customers.Local.First<Customer>(cus =>
                            cus.Name == customer.Name &&
                            cus.PhoneNumbers == customer.PhoneNumbers) == null)
                            result = false;
                        break;
                    }
            }
            return result;
        }
    }
}
