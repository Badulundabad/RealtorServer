using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Service;
using RealtorServer.Model.NET;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NLog;
using System.Diagnostics;
using System.Net;
using RealtyModel.Model.Operations;
using RealtorServer.Model.DataBase;
using RealtyModel.Model.Derived;
using System.Text.Json;
using RealtyModel.Model.Base;
using RealtyModel.Model.RealtyObjects;
using System.Data.Entity;
using System.Windows;

namespace RealtorServer.ViewModel
{
    class RealtorServerViewModel : INotifyPropertyChanged
    {
        #region Fields and Properties
        private object handleLocker = new object();
        private Boolean isRunning = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public event PropertyChangedEventHandler PropertyChanged;

        public Boolean IsRunning
        {
            get => isRunning;
            set
            {
                isRunning = value;
                OnPropertyChanged();
            }
        }
        public ICommand RunCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public LocalServer Server { get; private set; }
        public RealtyServer RealtyServer { get; private set; }
        public IdentityServer IdentityServer { get; private set; }
        #endregion

        public RealtorServerViewModel()
        {
            InitializeMembers();
            //TestFillDB();

            RunCommand = new CustomCommand((obj) =>
            {
                IsRunning = true;
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunAsync()));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunUDPMarkerAsync()));
            });
            RunCommand.Execute(new object());
            StopCommand = new CustomCommand((obj) =>
            {
                IsRunning = false;
                Server.Stop();
            });
        }

        private void InitializeMembers()
        {
            Server = new LocalServer(Dispatcher.CurrentDispatcher);
            Server.IncomingOperations.Enqueued += (s, e) => Handle();
            RealtyServer = new RealtyServer(Dispatcher.CurrentDispatcher);
            RealtyServer.OperationHandled += (s, e) => Server.OutcomingOperations.Enqueue(e.Operation);
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher);
            IdentityServer.OperationHandled += (s, e) => Server.OutcomingOperations.Enqueue(e.Operation);
        }
        private void Handle()
        {
            lock (handleLocker)
            {
                while (Server.IncomingOperations.Count > 0)
                {
                    Operation operation = null;
                    try
                    {
                        operation = Server.IncomingOperations.Dequeue();
                        if (operation.Parameters.Action == Act.Change)
                            RealtyServer.IncomingOperations.Enqueue(operation);
                        else if (operation.Parameters.Direction == Direction.Identity)
                            IdentityServer.IncomingOperations.Enqueue(operation);
                        else if (operation.Parameters.Direction == Direction.Realty)
                        {
                            if (operation.Parameters.Target == Target.Lists)
                                RealtyServer.IncomingOperations.Enqueue(operation);
                            else if (IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                                RealtyServer.IncomingOperations.Enqueue(operation);
                            else
                            {
                                logger.Info($"ViewModel {operation.Number} security check failed");
                                Debug.WriteLine($"{DateTime.Now} ViewModel {operation.Number} security check failed");
                            }
                        }
                        else
                        {
                            operation.IsSuccessfully = false;
                            Server.OutcomingOperations.Enqueue(operation);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"ViewModel(Handle) {ex.Message}");
                        Debug.WriteLine($"\n{DateTime.Now} ERROR ViewModel(Handle) {ex.Message}\n");
                        operation.IsSuccessfully = false;
                        Server.OutcomingOperations.Enqueue(operation);
                    }
                }
            }
        }
        private void OnPropertyChanged([CallerMemberName] String prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }



        private void TestFillDB()
        {
            RealtyContext context = new RealtyContext();
            Flat flat = new Flat()
            {
                Agent = "asd",
                Album = new Album()
                {
                    Location = "asdas",
                    PhotoKeys = "asd",
                    Preview = new byte[200000]
                },
                HasAlbumChanges = false,
                Cost = new Cost()
                {
                    Area = 100,
                    HasMortgage = false,
                    HasPercents = false,
                    HasVAT = false,
                    Price = 10000000
                },
                Customer = new Customer()
                {
                    Name = "asdsa",
                    PhoneNumbers = "123241324"
                },
                GeneralInfo = new BaseInfo()
                {
                    Ceiling = 100,
                    Condition = "asdas",
                    Convenience = "asdsa",
                    Description = "asdasd",
                    General = 1231,
                    Heating = "asdad",
                    Kitchen = 123421,
                    Living = 13241,
                    RoomCount = 123,
                    Water = "asdasd",
                    Year = 123
                },
                HasBaseChanges = false,
                HasCustomerChanges = false,
                HasExclusive = false,
                HasLocationChanges = false,
                Info = new FlatInfo()
                {
                    Balcony = "12312",
                    Bath = "sadad",
                    Bathroom = "asdasd",
                    Floor = "asdsad",
                    Fund = "asdasd",
                    HasChute = false,
                    HasElevator = false,
                    HasGarage = false,
                    HasImprovedLayout = false,
                    HasRenovation = false,
                    IsCorner = false,
                    IsPrivatised = false,
                    IsSeparated = false,
                    Kvl = 12312,
                    Loggia = "asdasd",
                    Material = "asdasd",
                    Rooms = "asdasads",
                    Type = "asdasd",
                    TypeOfRooms = "asdasd",
                    Windows = "asdasd"
                },
                IsSold = false,
                LastUpdateTime = DateTime.Now,
                Location = new Location()
                {
                    City = new City()
                    {
                        Name = "asdasd"
                    },
                    District = new District()
                    {
                        Name = "adssa"
                    },
                    Street = new Street()
                    {
                        Name = "asdasd",
                    },
                    FlatNumber = 1,
                    HouseNumber = 123,
                    HasBanner = false,
                    HasExchange = false
                },
                RegistrationDate = DateTime.Now,
                Status = Status.Active,
                Type = Target.Flat
            };
            Flat[] flats = new Flat[3000];
            for (Int32 i = 0; i < 3000; i++)
            {
                context.Flats.Add(flat);
                context.SaveChanges();
            }
            context.Flats.Load();
            ObservableCollection<Flat> flats1 = context.Flats.Local;
        }
    }
}
