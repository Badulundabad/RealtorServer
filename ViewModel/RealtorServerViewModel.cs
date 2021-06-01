using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using RealtyModel.Model.RealtyObjects;
using RealtyModel.Service;
using RealtorServer.Model.DataBase;
using RealtorServer.Model.NET;
using NLog;
using RealtyModel.Exceptions;

namespace RealtorServer.ViewModel
{
    public class RealtorServerViewModel : INotifyPropertyChanged
    {
        #region Fields and Properties
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
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.CheckQueueAsync()));
                if (Debugger.IsAttached)
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunUDPMarkerAsync()));

            });
            StopCommand = new CustomCommand((obj) =>
            {
                IsRunning = false;
                Server.Stop();
                RealtyServer.Stop();
                IdentityServer.Stop();
            });

            RunCommand.Execute(new object());
            CheckQueueAsync();
        }

        private void InitializeMembers()
        {
            Server = new LocalServer(Dispatcher.CurrentDispatcher);
            Server.Clients.CollectionChanged += (s, e) =>
            {
                foreach (LocalClient client in e.NewItems)
                    client.Disconnected += (sender, evArgs) => IdentityServer.Logout(client.IpAddress.ToString());
            };
            RealtyServer = new RealtyServer(Dispatcher.CurrentDispatcher, Server.OutcomingOperations);
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher, Server.OutcomingOperations);
        }
        private async void CheckQueueAsync()
        {
            await Task.Run(() =>
            {
                while (IsRunning)
                {
                    if (Server.IncomingOperations.Count > 0)
                        Handle();
                    Task.Delay(100).Wait();
                }
            });
        }

        private void Handle()
        {
            Operation operation = null;
            try
            {
                operation = Server.IncomingOperations.Dequeue();
                if (operation.Parameters.Direction == Direction.Identity)
                    IdentityServer.HandleAsync(operation);
                else if (operation.Parameters.Direction == Direction.Realty)
                {
                    if (operation.Parameters.Target == Target.Lists)
                        RealtyServer.HandleAsync(operation);
                    else if (IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                        RealtyServer.HandleAsync(operation);
                    else throw new InformationalException("security check was failed");
                }
                else throw new InformationalException("operation direction was wrong");
            }
            catch (InformationalException ex)
            {
                LogInfo($"{operation.Number} {ex.Message}");
                operation.IsSuccessfully = false;
                Server.OutcomingOperations.Enqueue(operation);
            }
            catch (Exception ex)
            {
                LogError($"(Handle) {ex.Message}");
                operation.IsSuccessfully = false;
                Server.OutcomingOperations.Enqueue(operation);
            }
        }

        private void OnPropertyChanged([CallerMemberName] String prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        private void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} {this.GetType().Name}     {text}");
            logger.Info($"{this.GetType().Name} {text}");
        }
        private void LogError(String text)
        {
            Debug.WriteLine($"\n{DateTime.Now} ERROR {this.GetType().Name}     {text}\n");
            logger.Error($"{this.GetType().Name} {text}");
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
                    PhotoCollection = new byte[200000]
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
