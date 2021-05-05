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
                        if (operation.Parameters.Type == OperationType.Update)
                            RealtyServer.IncomingOperations.Enqueue(operation);
                        else if (operation.Parameters.Direction == OperationDirection.Identity)
                            IdentityServer.IncomingOperations.Enqueue(operation);
                        else if (operation.Parameters.Direction == OperationDirection.Realty)
                        {
                            if (IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                                RealtyServer.IncomingOperations.Enqueue(operation);
                            else
                            {
                                logger.Info($"ViewModel {operation.OperationNumber} security check failed");
                                Debug.WriteLine($"{DateTime.Now} ViewModel {operation.OperationNumber} security check failed");
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
    }
}
