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

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        private RealtyContext realtyContext = new RealtyContext();
        private AlbumContext albumContext = new AlbumContext();

        public RealtyServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.dispatcher = dispatcher;
            this.log = log;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
        }

        public override async void RunAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        while (incomingQueue.Count > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            Operation newOperation = incomingQueue.Dequeue();
                            try
                            {
                                switch (newOperation.OperationParameters.Type)
                                {
                                    case OperationType.Add:
                                        {
                                            if (newOperation.OperationParameters.Target == TargetType.Flat)
                                                AddFlat(newOperation);
                                            else if (newOperation.OperationParameters.Target == TargetType.House)
                                                AddHouse(newOperation);
                                            else newOperation.IsSuccessfully = false;
                                            break;
                                        }
                                    case OperationType.Change:
                                        {
                                            break;
                                        }
                                    case OperationType.Remove:
                                        {
                                            break;
                                        }
                                    case OperationType.Update:
                                        {
                                            if (newOperation.OperationParameters.Target == TargetType.All)
                                            {
                                                SendFullUpdate(newOperation);
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            break;
                                        }
                                }
                            }
                            catch (Exception ex)
                            {
                                UpdateLog("(RunAsync) " + ex.Message);
                            }
                            finally
                            {
                                //outcomingQueue.Enqueue(newOperation);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {

                    }
                    finally
                    {
                        UpdateLog(" has stopped");
                    }
                }
            });
        }

        //There need to build some methods
        //Add an object
        //Change an object
        //Delete an object
        //MAYBE Get an object
        //Update on all clients

        private void AddFlat(Operation operation)
        {
            try
            {
                Flat newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                if (!FindDuplicate(TargetType.Flat, newFlat.Location))
                {
                    //добавить в бд
                    realtyContext.Flats.Local.Add(newFlat);
                    //отправить клиенту подтверждение операции
                    operation.IsSuccessfully = true;
                    operation.Data = "";
                    outcomingQueue.Enqueue(operation);
                    //отправить всем клиентам обновление
                    //newFlat.Customer.Id = newFlat.CustomerId;
                    //newFlat.Album.Id = newFlat.AlbumId;
                    Operation updateOperation = new Operation()
                    {
                        IpAddress = "broadcast",
                        OperationParameters = new OperationParameters() { Type = OperationType.Update, Target = TargetType.Flat },
                        Data = JsonSerializer.Serialize<Flat>(newFlat)
                    };
                    outcomingQueue.Enqueue(updateOperation);
                }
                else
                {
                    operation.IsSuccessfully = false;
                    outcomingQueue.Enqueue(operation);
                }
            }
            catch (Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
            }
        }
        private void AddHouse(Operation operation)
        {
            try
            {
                House newHouse = JsonSerializer.Deserialize<House>(operation.Data);
                if (!FindDuplicate(TargetType.House, newHouse.Location))
                {
                    //добавить в бд
                    realtyContext.Houses.Local.Add(newHouse);
                    //отправить клиенту подтверждение операции
                    operation.IsSuccessfully = true;
                    operation.Data = "";
                    outcomingQueue.Enqueue(operation);
                    //отправить всем клиентам обновление
                    Operation updateOperation = new Operation()
                    {
                        IpAddress = "broadcast",
                        OperationParameters = new OperationParameters() { Type = OperationType.Update, Target = TargetType.House },
                        Data = JsonSerializer.Serialize<House>(newHouse)
                    };
                    outcomingQueue.Enqueue(updateOperation);
                }
                else
                {
                    operation.IsSuccessfully = false;
                    outcomingQueue.Enqueue(operation);
                }
            }
            catch (Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
                operation.IsSuccessfully = false;
                outcomingQueue.Enqueue(operation);
            }
        }
        private Int32 AddAlbum(Album album)
        {
            albumContext.Albums.Local.Add(album);
            return album.Id;
        }
        private Int32 AddCustomer(Customer customer)
        {
            realtyContext.Customers.Local.Add(customer);
            return customer.Id;
        }
        private void SendFullUpdate(Operation operation)
        {
            //Send a list of agents
            Agent[] agents = realtyContext.Agents.Local.ToArray();
            operation.Data = JsonSerializer.Serialize(agents);
            operation.OperationParameters.Target = TargetType.Agent;
            operation.IsSuccessfully = true;
            outcomingQueue.Enqueue(operation);
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
