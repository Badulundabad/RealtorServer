﻿using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtorServer.Model.DataBase;
using System.Collections.Generic;
using Microsoft.Win32;
using System.IO;
using RealtyModel.Model.RealtyObjects;
using RealtyModel.Model.Operations;
using RealtorServer.Model.Event;
using RealtyModel.Service;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace RealtorServer.Model.NET
{
    public class RealtyServer : Server
    {
        public event OperationHandledEventHandler OperationHandled;

        public RealtyServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Dispatcher = dispatcher;
            OutcomingOperations = new OperationQueue();
            IncomingOperations = new OperationQueue();
            IncomingOperations.Enqueued += (s, e) => Handle();
        }

        private void ClearDB()
        {
            using (RealtyContext realtyDB = new RealtyContext())
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
                        Act action = operation.Parameters.Action;
                        LogInfo($"has started to handle {operation.Number}");
                        if (action == Act.Add)
                            AddObject(operation);
                        else if (action == Act.Change)
                            ChangeObject(operation);
                        else if (action == Act.Delete)
                            DeleteObject(operation);
                        else if (action == Act.Request)
                        {
                            if (operation.Parameters.Target == Target.Query)
                                SendResponse(operation);
                            else if (operation.Parameters.Target == Target.Lists)
                                SendLocationOptions(operation);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(Handle) {ex.Message}");
                }
            }
        }

        private async void SendResponse(Operation operation)
        {
            await Task.Run(() =>
            {
                try
                {
                    RealtyContext context = new RealtyContext();
                    Filter filter = BinarySerializer.Deserialize<Filter>(operation.Data);
                    operation.Data = new byte[0];
                    List<Flat> flats;
                    if (filter.IsFlat)
                    {
                        using (RealtyContext realty = new RealtyContext())
                            flats = realty.Flats.Local.ToList<Flat>();
                        Flat[][] filteredArrays = filter.CreateFilteredList(flats);
                        operation.Parameters.PartCount = filteredArrays.Length;
                        operation.IsSuccessfully = true;
                        Int32 number = 0;
                        foreach (Flat[] array in filteredArrays)
                        {
                            Operation part = operation.GetCopy();
                            part.Parameters.Part = number++;
                            part.Data = BinarySerializer.Serialize(array);
                            OperationHandled?.Invoke(this, new OperationHandledEventArgs(part));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(SendResponse) {ex.Message}\n\n");
                    operation.IsSuccessfully = false;
                    OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
                }
            });
        }
        private async void SendLocationOptions(Operation operation)
        {
            await Task.Run(() =>
            {
                try
                {
                    LocationOptions lists = new LocationOptions();
                    using (RealtyContext context = new RealtyContext())
                    {
                        if (context.Cities.Local.Count > 0)
                            lists.Cities = context.Cities.Local;
                        else lists.Cities = new ObservableCollection<City>();
                        if (context.Districts.Local.Count > 0)
                            lists.Districts = context.Districts.Local;
                        else lists.Districts = new ObservableCollection<District>();
                        if (context.Streets.Local.Count > 0)
                            lists.Streets = context.Streets.Local;
                        else lists.Streets = new ObservableCollection<Street>();

                        operation.Data = BinarySerializer.Serialize<LocationOptions>(lists);
                        operation.IsSuccessfully = true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(AddFlat) {ex.Message}");
                    operation.IsSuccessfully = false;
                }
                finally
                {
                    OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
                }
            });
        }
        private void AddObject(Operation operation)
        {
            LogInfo($"{operation.Number} add");
            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                AddFlat(operation);
            else if (target == Target.Photo)
                AddPhoto(operation);
        }
        private void ChangeObject(Operation operation)
        {
            LogInfo($"{operation.Number} change");

            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                ChangeFlat(operation);
        }
        private void DeleteObject(Operation operation)
        {
            LogInfo($"{operation.Number} remove");

            Target target = operation.Parameters.Target;
            if (target == Target.Flat)
                DeleteFlat(operation);
        }

        private void AddFlat(Operation operation)
        {
            LogInfo($"{operation.Number} add flat");
            try
            {
                Flat newFlat = BinarySerializer.Deserialize<Flat>(operation.Data);
                operation.Data = new byte[0];
                if (!FindDuplicate(Target.Flat, newFlat.Location))
                {
                    using (RealtyContext realtyDB = new RealtyContext())
                    {
                        if (newFlat.CustomerId > 0)
                            newFlat.Customer = realtyDB.Customers.Find(newFlat.CustomerId) ?? newFlat.Customer;
                        if (newFlat.Location.CityId > 0)
                            newFlat.Location.City = realtyDB.Cities.Find(newFlat.Location.CityId) ?? newFlat.Location.City;
                        if (newFlat.Location.DistrictId > 0)
                            newFlat.Location.District = realtyDB.Districts.Find(newFlat.Location.DistrictId) ?? newFlat.Location.District;
                        if (newFlat.Location.StreetId > 0)
                            newFlat.Location.Street = realtyDB.Streets.Find(newFlat.Location.StreetId) ?? newFlat.Location.Street;

                        newFlat.RegistrationDate = DateTime.Now;
                        newFlat.LastUpdateTime = DateTime.Now;

                        realtyDB.Flats.Local.Add(newFlat);
                        realtyDB.SaveChanges();
                    }

                    LogInfo($"{operation.Name} has registered a flat {newFlat.Location.City.Name} {newFlat.Location.District.Name} {newFlat.Location.Street.Name} {newFlat.Location.HouseNumber} кв{newFlat.Location.FlatNumber}");
                    operation.IsSuccessfully = true;
                }
                else
                {
                    LogInfo($"{operation.Number} there is a flat with such address");
                    operation.IsSuccessfully = false;
                }
            }
            catch (Exception ex)
            {
                LogError($"(AddFlat) {ex.Message}\n\n");
                LogError($"(AddFlat) {operation.Data}\n\n");
                operation.IsSuccessfully = false;
            }
            finally
            {
                OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
            }
        }
        private void AddPhoto(Operation operation)
        {
            //try
            //{
            //    String[] data = operation.Data.Split(new String[] { "<GUID>" }, StringSplitOptions.None);
            //    Photo photo = JsonSerializer.Deserialize<Photo>(data[1]);
            //    using (RealtyContext realtyDB = new RealtyContext())
            //    {
            //        realtyDB.Photos.Local.Add(photo);
            //        realtyDB.SaveChanges();
            //    }
            //    operation.Data = data[0];
            //    operation.IsSuccessfully = true;
            //    LogInfo($"SAVED A PHOTO {operation.Number}");
            //}
            //catch (Exception ex)
            //{
            //    LogError($"(AddPhoto) {ex.Message}\n\n");
            //    operation.Data = null;
            //    operation.IsSuccessfully = false;
            //}
            //finally
            //{
            //    OperationHandled?.Invoke(this, new OperationHandledEventArgs(operation));
            //}
        }
        private void ChangeFlat(Operation operation)
        {
            LogInfo($"{operation.Number} change flat");
            try
            {
                Flat updFlat = JsonSerializer.Deserialize<Flat>(operation.Data);
                using (RealtyContext realtyDB = new RealtyContext())
                {
                    Flat dbFlat = realtyDB.Flats.Find(updFlat.Id) ?? throw new Exception($"There is no such flat {updFlat.Location.City.Name} {updFlat.Location.District.Name} {updFlat.Location.Street.Name} д{updFlat.Location.HouseNumber} кв{updFlat.Location.FlatNumber}");
                    operation.Data = new byte[0];
                    if (operation.Name == dbFlat.Agent && ChangeProperties(updFlat, dbFlat, operation))
                    {
                        dbFlat.LastUpdateTime = DateTime.Now;
                        realtyDB.SaveChanges();
                        LogInfo($"{operation.Name} has changed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} д{dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");
                        operation.IsSuccessfully = true;
                    }
                    else operation.IsSuccessfully = false;
                }
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

        private Boolean ChangeProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, Operation operation)
        {
            try
            {
                if (fromObject.HasBaseChanges)
                    ChangeBaseProperties(fromObject, toObject, operation.Parameters.Target);
                if (fromObject.HasAlbumChanges)
                    ChangeAlbum(fromObject, toObject);
                if (fromObject.HasCustomerChanges)
                    ChangeCustomer(fromObject, toObject);
                if (fromObject.HasLocationChanges)
                    ChangeLocation(fromObject, toObject, operation.Parameters.Target);
                return true;
            }
            catch (Exception ex)
            {
                LogError($"(ChangeProperties) {ex.Message}");
                return false;
            }
        }
        private void ChangeBaseProperties(BaseRealtorObject fromObject, BaseRealtorObject toObject, Target target)
        {
            if (target == Target.Flat)
                ((Flat)toObject).Info = ((Flat)fromObject).Info;
            else if (target == Target.House)
                ((House)toObject).Info = ((House)fromObject).Info;
            toObject.Cost = fromObject.Cost;
            toObject.HasExclusive = fromObject.HasExclusive;
            toObject.IsSold = fromObject.IsSold;
        }
        private void ChangeAlbum(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {

        }
        private void ChangeCustomer(BaseRealtorObject fromObject, BaseRealtorObject toObject)
        {
            toObject.Customer.Name = fromObject.Customer.Name;
            toObject.Customer.PhoneNumbers = fromObject.Customer.PhoneNumbers;
        }
        private void ChangeLocation(BaseRealtorObject fromObject, BaseRealtorObject toObject, Target type)
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
            if (type == Target.Flat)
                toObject.Location.FlatNumber = fromObject.Location.FlatNumber;
            toObject.Location.HouseNumber = fromObject.Location.HouseNumber;
            toObject.Location.HasBanner = fromObject.Location.HasBanner;
            toObject.Location.HasExchange = fromObject.Location.HasExchange;
        }

        private Boolean FindDuplicate(Target target, Location location = null, Customer customer = null)
        {
            using (RealtyContext realtyDB = new RealtyContext())
            {
                if (target == Target.Flat && realtyDB.Flats.Count() > 0)
                    return realtyDB.Flats.Local.Any(flat =>
                                                         flat.Location.City.Name == location.City.Name &&
                                                         flat.Location.Street.Name == location.Street.Name &&
                                                         flat.Location.District.Name == location.District.Name &&
                                                         flat.Location.HouseNumber == location.HouseNumber &&
                                                         flat.Location.FlatNumber == location.FlatNumber);
                else if (target == Target.House && realtyDB.Houses.Local.Count > 0)
                    return realtyDB.Houses.Local.Any(house =>
                                                          house.Location.City == location.City &&
                                                          house.Location.Street == location.Street &&
                                                          house.Location.HouseNumber == location.HouseNumber &&
                                                          house.Location.FlatNumber == location.FlatNumber);
                //else if (target == Target.Customer && realtyDB.Customers.Local.Count > 0)
                //    return realtyDB.Customers.Local.Any(cus =>
                //                                             cus.Name == customer.Name &&
                //                                             cus.PhoneNumbers == customer.PhoneNumbers);
                else return false;
            }
        }





        private void DeleteFlat(Operation operation)
        {
            LogInfo($"{operation.Number} remove flat");
            try
            {
                using (RealtyContext realtyDB = new RealtyContext())
                {
                    Flat dbFlat = realtyDB.Flats.Find(operation.Data);
                    if (dbFlat != null && operation.Name == dbFlat.Agent)
                    {
                        realtyDB.Flats.Remove(dbFlat);
                        realtyDB.SaveChanges();

                        LogInfo($"{operation.IpAddress} has removed a flat {dbFlat.Location.City} {dbFlat.Location.District} {dbFlat.Location.Street} {dbFlat.Location.HouseNumber} кв{dbFlat.Location.FlatNumber}");

                        operation.Parameters.Action = Act.Delete;
                        operation.IsBroadcast = true;
                        operation.IsSuccessfully = true;
                    }
                    else operation.IsSuccessfully = false;
                }
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
    }
}
