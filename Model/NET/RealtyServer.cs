using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using NLog;
using RealtyModel.Model.Base;

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

            DebugMethClear();
            realtyContext.SaveChanges();
        }
        public override void Stop()
        {
            queueChecker.Stop();
            logger.Info("Realty server has stopped");
            UpdateLog("has stopped");
        }
        private void DebugMethClear()
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
                            operation = SendFullUpdate(operation);
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
                    operation.IsSuccessfully = false;
                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Realty server(AddFlat) {ex.Message}");
                UpdateLog($"(AddFlat) {ex.Message}");
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
        
        private Operation SendFullUpdate(Operation operation)
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
            return null;
        }
        private Boolean FindDuplicate(TargetType target, Location location = null, Customer customer = null, Album album = null)
        {
            Boolean result = true;
            if (target == TargetType.Flat)
                if (realtyContext.Flats.Local.FirstOrDefault<Flat>(flat =>
                    flat.Location.City == location.City
                    && flat.Location.Street == location.Street
                    && flat.Location.District == location.District
                    && flat.Location.HouseNumber == location.HouseNumber
                    && flat.Location.FlatNumber == location.FlatNumber) == null)
                    result = false;
            if (target == TargetType.House)
                if (realtyContext.Houses.Local.First<House>(house =>
                    house.Location.City == location.City &&
                    house.Location.Street == location.Street &&
                    house.Location.HouseNumber == location.HouseNumber &&
                    house.Location.FlatNumber == location.FlatNumber) == null)
                    result = false;
            if (target == TargetType.Customer)
                if (realtyContext.Customers.Local.First<Customer>(cus =>
                    cus.Name == customer.Name &&
                    cus.PhoneNumbers == customer.PhoneNumbers) == null)
                    result = false;

            return result;
        }
    }
}
