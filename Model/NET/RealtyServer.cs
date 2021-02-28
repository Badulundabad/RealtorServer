using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Data.Entity;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using RandomFlatGenerator;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        private System.Timers.Timer queueChecker = null;
        private RealtyContext realtyContext = new RealtyContext();
        private AlbumContext albumContext = new AlbumContext();

        public RealtyServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output, CancellationToken cancellationToken) : base(dispatcher, log, output, cancellationToken)
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
        }
        public override void Stop()
        {
            queueChecker.Stop();
            UpdateLog("has stopped");
        }
        private void Test()
        {
            Clear();
            realtyContext.SaveChanges();

            Flat flat = new Flat()
            {
                Agent = "first attempt",
                Id = 1,
                AlbumId = 1,
                CustomerId = 1,
                LocationId = 1,
                Album = new Album()
                {
                    Id = 1,
                    Location = "asd",
                    PhotoList = new byte[256],
                    Preview = new byte[256]
                },
                Customer = new Customer()
                {
                    Id = 1,
                    Name = "asd",
                    PhoneNumbers = "asd"
                },
                Location = new Location()
                {
                    Id = 0,
                    CityId = 1,
                    DistrictId = 1,
                    StreetId = 1,
                    City = new City()
                    {
                        Id = 1,
                        Name = "asd"
                    },
                    District = new District()
                    {
                        Id = 1,
                        Name = "asd"
                    },
                    Street = new Street()
                    {
                        Id = 1,
                        Name = "asd"
                    },
                    FlatNumber = 12,
                    HasBanner = false,
                    HasExchange = false,
                    HouseNumber = 12
                },
                Info = new FlatInfo()
                {
                    Balcony = "asd",
                    Bath = "asd",
                    Bathroom = "asd",
                    Ceiling = 10,
                    Condition = "asd",
                    Convenience = "asd",
                    Description = "asd",
                    Floor = "asd",
                    Fund = "asd",
                    General = 10,
                    HasChute = false,
                    HasElevator = false,
                    HasGarage = false,
                    HasImprovedLayout = false,
                    HasRenovation = false,
                    Heating = "asd",
                    IsCorner = false,
                    IsPrivatised = false,
                    IsSeparated = false,
                    Kitchen = 12,
                    Kvl = 12,
                    Living = 12,
                    Loggia = "asd",
                    Material = "asd",
                    RoomCount = 1,
                    Rooms = "asd",
                    Type = "asd",
                    TypeOfRooms = "asd",
                    Water = "asd",
                    Windows = "asd",
                    Year = 12
                },
                Cost = new Cost()
                {
                    Area = 10,
                    HasMortgage = true,
                    HasPercents = true,
                    HasVAT = true,
                    Price = 10
                },
                HasExclusive = true,
                IsSold = false,
            };
            realtyContext.Flats.Local.Add(flat);
            realtyContext.SaveChanges();

            Album album = flat.Album;
            Cost cost = flat.Cost;
            Customer customer = flat.Customer;
            FlatInfo info = flat.Info;
            Location location = flat.Location;

            Flat flat2 = realtyContext.Flats.Find(1);
            flat2.Agent = "flat2";
            flat2.Album = album;
            flat2.Cost = cost;
            flat2.Customer = customer;
            flat2.Info = info;
            flat2.Location = location;
            realtyContext.Flats.Attach(flat2);
            realtyContext.Entry(flat2).State = EntityState.Modified;
            realtyContext.SaveChanges();

            Flat flat3 = new Flat()
            {
                Id = 1,
                AlbumId = 1,
                CustomerId = 1,
                LocationId = 1,
                HasExclusive = false,
                IsSold = false,
                Agent = "flat3",
                Album = album,
                Cost = cost,
                Customer = customer,
                Info = info,
                Location = location,
            };
            realtyContext.Flats.Attach(flat3);
            realtyContext.Entry(flat3).State = EntityState.Modified;
            realtyContext.SaveChanges();
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
                                    //else if (newOperation.OperationParameters.Target == TargetType.House)
                                    //    newOperation = ChangeHouse(newOperation);
                                    else newOperation.IsSuccessfully = false;
                                    break;
                                }
                            case OperationType.Remove:
                                {
                                    if (newOperation.OperationParameters.Target == TargetType.Flat)
                                    {
                                        realtyContext.Flats.Remove(JsonSerializer.Deserialize<Flat>(newOperation.Data));

                                    }
                                    else if (newOperation.OperationParameters.Target == TargetType.House)
                                        AddHouse(newOperation);
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
                if (newOperation != null) outcomingQueue.Enqueue(newOperation);
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
                if (UpdateProperties())
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
                        if(updFlat.Location.City.Id == 0)
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
        private void ChangeHouse(Operation newOperation)
        {

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
