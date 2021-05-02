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

namespace RealtorServer.ViewModel
{
    class RealtorServerViewModel : INotifyPropertyChanged
    {
        #region Fields and Properties
        private object handleLocker = new object();
        private Boolean isRunning = false;
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
        public IdentityServer IdentityServer { get; private set; }
        public RealtyServer RealtyServer { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        public RealtorServerViewModel()
        {
            InitializeMembers();
            RunCommand = new CustomCommand((obj) =>
            {
                Log.Clear();
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
            Log = new ObservableCollection<LogMessage>();
            Server = new LocalServer(Dispatcher.CurrentDispatcher, Log);
            Server.IncomingOperations.Enqueued += (s, e) => Handle();
            RealtyServer = new RealtyServer(Dispatcher.CurrentDispatcher, Log);
            RealtyServer.OperationHandled += (s, e) => Server.OutcomingOperations.Enqueue(e.Operation);
            //RealtyServer.OutcomingOperations.Enqueued += (s, e) => Server.OutcomingOperations.Enqueue(RealtyServer.OutcomingOperations.Dequeue());//Переделать убрав Outcoming на событие Handled
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher, Log);
            //IdentityServer.OutcomingOperations.Enqueued += (s, e) => Server.OutcomingOperations.Enqueue(IdentityServer.OutcomingOperations.Dequeue());
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
                        if (operation.OperationParameters.Type == OperationType.Update)
                            RealtyServer.IncomingOperations.Enqueue(operation);
                        else if (operation.OperationParameters.Direction == OperationDirection.Identity)
                            IdentityServer.IncomingOperations.Enqueue(operation);
                        else if (operation.OperationParameters.Direction == OperationDirection.Realty && IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                            RealtyServer.IncomingOperations.Enqueue(operation);
                        else
                        {
                            operation.IsSuccessfully = false;
                            Server.OutcomingOperations.Enqueue(operation);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"ViewModel(CheckQueue) {ex.Message}");
                        UpdateLog($"(Handle) {ex.Message}");
                        operation.IsSuccessfully = false;
                        Server.OutcomingOperations.Enqueue(operation);
                    }
                }
            }
        }
        private void UpdateLog(String text)
        {
            //После тестов удалить
            LogMessage logMessage = new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), $"Server {text}");
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                Log.Add(logMessage);
            }));
        }
        private void OnPropertyChanged([CallerMemberName] String prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
