using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using RealtyModel;
using RealtyModel.RealtorObject;
using RealtyModel.RealtorObject.DerivedClasses;
using RealtorServer.Model;
using System.Text.Json;

namespace RealtorServer.ViewModel
{
    class RealtorServerViewModel
    {
        public ICommand RunCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public Server CurrentServer { get; private set; }
        public RealtyContext Realty { get; private set; }
        public IdentityServer IdentityServer { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }

        public RealtorServerViewModel()
        {
            Log = new ObservableCollection<LogMessage>();
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher);
            CurrentServer = new Server(Dispatcher.CurrentDispatcher);
            Realty = new RealtyContext();
            //Test();

            IdentityServer.Log.CollectionChanged += (sender, e) => UpdateLog(e.NewItems);
            IdentityServer.IdentityResults.CollectionChanged += (sender, e) => HandleIdentityResult(e.NewItems);
            CurrentServer.Log.CollectionChanged += (sender, e) => UpdateLog(e.NewItems);
            CurrentServer.IncomingOperations.CollectionChanged += (sender, e) => HandleIncomingOperations(e.NewItems);

            RunCommand = new CustomCommand((obj) =>
            {
                Dispatcher.CurrentDispatcher.InvokeAsync(IdentityServer.RunAsync);
                Dispatcher.CurrentDispatcher.InvokeAsync(CurrentServer.RunAsync);
            });
            StopCommand = new CustomCommand((obj) =>
            {
                Dispatcher.CurrentDispatcher.Invoke(IdentityServer.Stop);
                Dispatcher.CurrentDispatcher.Invoke(CurrentServer.Stop);
            });
        }

        public void Test()
        {
            Flat flat = new Flat()
            {
                Info = new FlatInfo()
                {
                    Balcony = "asd",
                    Bath = "asd",
                    Bathroom = "asd",
                    Ceiling = 2,
                    Condition = "asd",
                    Convenience = "asd",
                    Description = "qwe",
                    Floor = "asd",
                    Fund = "asd",
                    General = 2,
                    HasChute = false,
                    HasElevator = false,
                    HasGarage = false,
                    HasImprovedLayout = false,
                    HasRenovation = false,
                    Heating = "qwe",
                    IsCorner = false,
                    IsPrivatised = false,
                    Kitchen = 2,
                    Kvl = 2,
                    Living = 2.1,
                    Loggia = "asd",
                    Material = "asd",
                    RoomCount = 2,
                    Rooms = "asd",
                    Type = "asd",
                    TypeOfRooms = "asd",
                    Water = "asd",
                    Windows = "asd",
                    Year = 1990
                },
                Worker = new Worker()
                {
                    Agency = "asd",
                    RegDate = DateTime.Now,
                    Registrant = "asd",
                    RespDate = DateTime.Now,
                    Responsible = "asd"
                },
                Location=new Location()
                {
                    Banner=false,
                    City="asd",
                    District="asd",
                    Exchange=false,
                    FlatNumber=2,
                    HouseNumber=2,
                    Street="asd"
                },
                Cost=new Cost()
                {
                    Area=2,
                    VAT=false,
                    Mortgage=false,
                    Multiplier=2,
                    PseudoPrice=2,
                    RealPrice=2
                }
            };
            Realty.Flats.Local.Add(flat);
            Realty.SaveChanges();
        }


        private void HandleIdentityResult(IList results)
        {
            foreach (Operation result in results)
            {
                CurrentServer.OperationResults.Add(result);
            }
        }
        private void HandleIncomingOperations(IList operations)
        {
            try
            {
                foreach (Operation operation in operations)
                {
                    try
                    {
                        switch (operation.OperationType)
                        {
                            case OperationType.Register:
                                {
                                    IdentityServer.IdentityQueue.Enqueue(operation);
                                    break;
                                }
                            case OperationType.Login:
                                {
                                    IdentityServer.IdentityQueue.Enqueue(operation);
                                    break;
                                }
                            case OperationType.Add:
                                {
                                    AddObject(operation);
                                    break;
                                }
                            case OperationType.Change:
                                {
                                    UpdateObject(operation);
                                    break;
                                }
                            case OperationType.Remove:
                                {
                                    RemoveObject(operation);
                                    break;
                                }
                            case OperationType.Update:
                                {
                                    Get(operation);
                                    break;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateLog("(HandleOperations)threw an exception: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateLog("(HandleOperations)threw an exception: " + ex.Message);
            }
        }
        private void AddObject(Operation operation)
        {
            switch (operation.ObjectType)
            {
                case ObjectType.Flat:
                    {
                        Flat newFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                        if (FindDuplicate(ObjectType.Flat, newFlat.Location))
                        {
                            operation.IsSuccessfully = false;
                            CurrentServer.OperationResults.Add(operation);
                        }
                        else
                        {
                            //добавить в бд
                            Realty.Flats.Local.Add(newFlat);
                            //отправить подтверждение
                            operation.IsSuccessfully = true;
                            CurrentServer.OperationResults.Add(operation);
                            //отправить всем клиентам обновление
                            Operation op = new Operation()
                            {
                                IpAddress="broadcast",
                                OperationType=OperationType.Update,
                                ObjectType=operation.ObjectType,
                                Data=real
                            }
                        }
                        break;
                    }
                case ObjectType.House:
                    {
                        break;
                    }
            }
        }

        private Boolean FindDuplicate(ObjectType objectType, Location location)
        {
            Boolean result = true;
            if (objectType == ObjectType.Flat)
            {
                if (Realty.Flats.Local.Where<Flat>(flat =>
                    flat.Location.City == location.City &&
                    flat.Location.Street == location.Street &&
                    flat.Location.HouseNumber == location.HouseNumber &&
                    flat.Location.FlatNumber == location.FlatNumber
                   ).Count() == 0)
                    result = false;
            }
            else if (objectType == ObjectType.House)
            {
                if (Realty.Houses.Local.Where<House>(house =>
                     house.Location.City == location.City &&
                     house.Location.Street == location.Street &&
                     house.Location.HouseNumber == location.HouseNumber &&
                     house.Location.FlatNumber == location.FlatNumber
                   ).Count() == 0)
                    result = false;
            }
            return result;
        }
        private Int32 GetId(ObjectType objectType, Location location)
        {
            Int32 id = -1;
            if (objectType == ObjectType.Flat)
            {
                ObservableCollection<Flat> flats = (ObservableCollection<Flat>)Realty.Flats.Local.Where<Flat>(flat =>
                    flat.Location.City == location.City &&
                    flat.Location.Street == location.Street &&
                    flat.Location.HouseNumber == location.HouseNumber &&
                    flat.Location.FlatNumber == location.FlatNumber);
                if (flats != null && flats.Count != 0)
                {
                    id = flats[0].Id;
                }

            }
            else if (objectType == ObjectType.House)
            {
                ObservableCollection<House> houses = (ObservableCollection<House>)Realty.Houses.Local.Where<House>(house =>
                     house.Location.City == location.City &&
                     house.Location.Street == location.Street &&
                     house.Location.HouseNumber == location.HouseNumber &&
                     house.Location.FlatNumber == location.FlatNumber);
                if(houses!=null && houses.Count != 0)
                {
                    id = houses[0].Id;
                }
            }
            return id;
        }
        private void UpdateObject(Operation operation)
        {
            throw new NotImplementedException();
        }
        private void RemoveObject(Operation operation)
        {
            throw new NotImplementedException();
        }
        private void Get(Operation operation)
        {
            throw new NotImplementedException();
        }
        private void UpdateLog(IList messages)
        {
            List<String> newMessages = new List<String>();
            foreach (LogMessage message in messages)
            {
                newMessages.Add(message.DateTime + " " + message.Text);
            }
            File.AppendAllLines("log.txt", newMessages);

            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                foreach (LogMessage message in messages)
                {
                    Log.Add(message);
                }
            }));
        }
        private void UpdateLog(String message)
        {
            String mes = " server " + message;
            File.AppendAllLines("log.txt", new List<String>() { DateTime.Now.ToString("dd:MM:yy hh:mm") + mes });
            LogMessage logMessage = new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), mes);
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                Log.Add(logMessage);
            }));
        }
    }
}
