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
        private Server server;
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        public RealtorServerViewModel() {
            server = new Server();
            ClearDB();
            Dispatcher.CurrentDispatcher.BeginInvoke(new System.Action(() => server.RunAsync()));
        }
        private void ClearDB() {
            using (RealtyContext realtyDB = new RealtyContext()) {
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Customers'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Albums'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Flats'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Houses'");
                realtyDB.Database.ExecuteSqlCommand("update sqlite_sequence set seq = 0 where name = 'Locations'");
                realtyDB.Albums.Local.Clear();
                realtyDB.Customers.Local.Clear();
                realtyDB.Flats.Local.Clear();
                realtyDB.Locations.Local.Clear();
                realtyDB.SaveChanges();
            }
        }
        private void OnPropertyChanged([CallerMemberName] String prop = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
