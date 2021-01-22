using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Service;
using RealtorServer.Model;
using ImageTranscoding;
using System.Drawing.Imaging;

namespace RealtorServer.ViewModel
{
    class RealtorServerViewModel
    {
        private AlbumContext albumContext = new AlbumContext();
        private RealtyContext realtyContext = new RealtyContext();
        private Queue<Operation> identityQueue = new Queue<Operation>();
        private Queue<Operation> outcomingOperations = new Queue<Operation>();

        public ICommand RunCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public LocalServer CurrentServer { get; private set; }
        public CredentialServer CredentialServer { get; private set; }
        public NewIdentityServer IdentityServer { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }
        
        public RealtorServerViewModel()
        {
            Log = new ObservableCollection<LogMessage>();
            
            CredentialServer = new CredentialServer(Dispatcher.CurrentDispatcher);
            CurrentServer = new LocalServer(Dispatcher.CurrentDispatcher);

            IdentityServer = new NewIdentityServer(Dispatcher.CurrentDispatcher, Log, identityQueue, outcomingOperations);

            RunCommand = new CustomCommand((obj) =>
            {
                Dispatcher.CurrentDispatcher.InvokeAsync(CredentialServer.RunAsync);
                Dispatcher.CurrentDispatcher.InvokeAsync(CurrentServer.RunAsync);
            });
            StopCommand = new CustomCommand((obj) =>
            {
                Dispatcher.CurrentDispatcher.Invoke(CredentialServer.Stop);
                Dispatcher.CurrentDispatcher.Invoke(CurrentServer.Stop);
            });


            //Test();
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
                                    CredentialServer.IdentityQueue.Enqueue(operation);
                                    break;
                                }
                            case OperationType.Login:
                                {
                                    CredentialServer.IdentityQueue.Enqueue(operation);
                                    break;
                                }
                            case OperationType.Add:
                                {
                                    //AddObject(operation);
                                    break;
                                }
                            case OperationType.Change:
                                {
                                    ChangeObject(operation);
                                    break;
                                }
                            case OperationType.Remove:
                                {
                                    RemoveObject(operation);
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
            try
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
                                DBFlat newDBFlat = DBFlat.GetDBFlat(newFlat);
                                newDBFlat.CredentialId = newFlat.Credential.Id;
                                if (newFlat.Customer.Id == -1)
                                {
                                    newFlat.Customer.Id = AddCustomer(newFlat.Customer);
                                }
                                newFlat.Album.Id = AddAlbum(newFlat.Album);

                                newDBFlat.CustomerId = newFlat.Customer.Id;
                                newDBFlat.AlbumId = newFlat.Album.Id;

                                if (newDBFlat.CustomerId != -1 && newDBFlat.AlbumId != -1)
                                {
                                    //добавить в бд
                                    realtyContext.Flats.Local.Add(newDBFlat);
                                    //отправить подтверждение
                                    operation.IsSuccessfully = true;
                                    operation.Data = "";
                                    CurrentServer.OperationResults.Add(operation);
                                    //отправить всем клиентам обновление
                                    DBFlat dbFlat = realtyContext.Flats.Local.First<DBFlat>(fl => fl.Location == newDBFlat.Location);
                                    newFlat.Id = dbFlat.Id;
                                    Operation updateOperation = new Operation()
                                    {
                                        IpAddress = "broadcast",
                                        OperationType = OperationType.Update,
                                        ObjectType = operation.ObjectType,
                                        Data = JsonSerializer.Serialize<Flat>(newFlat)
                                    };
                                    CurrentServer.OperationResults.Add(updateOperation);
                                }
                                else
                                {
                                    //отправить отказ
                                    operation.IsSuccessfully = false;
                                    operation.Data = "";
                                    CurrentServer.OperationResults.Add(operation);
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
            catch(Exception ex)
            {
                UpdateLog("(AddObject) " + ex.Message);
            }
        }
        private Int32 AddAlbum(Album album)
        {
            albumContext.Albums.Local.Add(album);
            Album newAlbum = albumContext.Albums.Local.First(alb =>
            alb.Location == album.Location &&
            alb.Preview == album.Preview &&
            alb.PhotoList == album.PhotoList);
            return newAlbum.Id;
        }
        private Int32 AddCustomer(Customer customer)
        {
            realtyContext.Customers.Local.Add(customer);
            Customer newCust = realtyContext.Customers.Local.First<Customer>(cus =>
            cus.Name == customer.Name &&
            cus.PhoneNumbers == customer.PhoneNumbers);
            return newCust.Id;
        }
        
        private Boolean FindDuplicate(ObjectType objectType, Location location = null, Customer customer = null, Album album = null)
        {
            Boolean result = true;
            if (objectType == ObjectType.Flat)
            {
                if (realtyContext.Flats.Local.Where<DBFlat>(flat =>
                    flat.Location.City == location.City &&
                    flat.Location.Street == location.Street &&
                    flat.Location.HouseNumber == location.HouseNumber &&
                    flat.Location.FlatNumber == location.FlatNumber
                   ).Count() == 0)
                    result = false;
            }
            else if (objectType == ObjectType.House)
            {
                if (realtyContext.Houses.Local.Where<House>(house =>
                     house.Location.City == location.City &&
                     house.Location.Street == location.Street &&
                     house.Location.HouseNumber == location.HouseNumber &&
                     house.Location.FlatNumber == location.FlatNumber
                   ).Count() == 0)
                    result = false;
            }
            else if (objectType == ObjectType.Customer)
            {
                if (realtyContext.Customers.Local.Where<Customer>(cus =>
                     cus.Name == customer.Name &&
                     cus.PhoneNumbers == customer.PhoneNumbers
                   ).Count() == 0)
                    result = false;
            }
            else if (objectType == ObjectType.Album)
            {
                if (albumContext.Albums.Local.Where<Album>(alb =>
                    alb.Location == album.Location &&
                    alb.Preview == album.Preview &&
                    alb.PhotoList == album.PhotoList
                   ).Count() == 0)
                    result = false;
            }
            return result;
        }
        private Int32 GetId(ObjectType objectType, Location location = null, Customer customer = null, Album album = null)
        {
            Int32 id = -1;
            if (objectType == ObjectType.Flat)
            {
                ObservableCollection<DBFlat> flats = (ObservableCollection<DBFlat>)realtyContext.Flats.Local.Where<DBFlat>(flat =>
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
                ObservableCollection<House> houses = (ObservableCollection<House>)realtyContext.Houses.Local.Where<House>(house =>
                     house.Location.City == location.City &&
                     house.Location.Street == location.Street &&
                     house.Location.HouseNumber == location.HouseNumber &&
                     house.Location.FlatNumber == location.FlatNumber);
                if (houses != null && houses.Count != 0)
                {
                    id = houses[0].Id;
                }
            }
            else if (objectType == ObjectType.Customer)
            {
                ObservableCollection<Customer> customers = (ObservableCollection<Customer>)realtyContext.Customers.Local.Where<Customer>(cus =>
                    cus.Name == customer.Name &&
                    cus.PhoneNumbers == customer.PhoneNumbers);
                if (customers != null && customers.Count != 0)
                {
                    id = customers[0].Id;
                }
            }
            else if (objectType == ObjectType.Album)
            {
                ObservableCollection<Album> albums = (ObservableCollection<Album>)albumContext.Albums.Local.Where<Album>(alb =>
                alb.Location == album.Location &&
                alb.Preview == album.Preview &&
                alb.PhotoList == album.PhotoList);
                if (albums != null && albums.Count != 0)
                {
                    id = albums[0].Id;
                }
            }
            return id;
        }
        
        private void ChangeObject(Operation operation)
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
        /*
        private void Test()
        {
            Flat flat;
            for(Int32 i = 0; i <10; i++)
            {
                flat = new Flat()
                {
                    Cost = new Cost()
                    {

                    }
                    Album = new Album()
                    {
                        
                    }
                }
                Operation operation = new Operation()
                {
                    Data =
                }
            }
        }*/
    }
}
