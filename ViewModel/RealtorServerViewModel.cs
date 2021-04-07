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
        private Boolean isRunning = false;
        private Queue<Operation> output = new Queue<Operation>();
        private DispatcherTimer filterTask = new DispatcherTimer();
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
                filterTask.Start();
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => IdentityServer.Run()));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => RealtyServer.Run()));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunAsync()));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunUDPMarkerAsync()));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.PollClientsAsync()));
            });
            StopCommand = new CustomCommand((obj) =>
            {
                IsRunning = false;
                filterTask.Stop();
                Server.Stop();
                IdentityServer.Stop();
                RealtyServer.Stop();
            });
        }

        private void InitializeMembers()
        {
            Log = new ObservableCollection<LogMessage>();
            Server = new LocalServer(Dispatcher.CurrentDispatcher, Log, output);
            RealtyServer = new RealtyServer(Dispatcher.CurrentDispatcher, Log, output);
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher, Log, output);
            filterTask.Interval = TimeSpan.FromMilliseconds(100);
            filterTask.Tick += (o, e) => CheckQueue();
        }
        private void CheckQueue()
        {
            while (Server.IncomingQueue.Count > 0)
            {
                Operation operation = Server.IncomingQueue.Dequeue();
                if (operation != null)
                    Handle(operation);
            }
            void Handle(Operation operation)
            {
                try
                {
                    if (operation.OperationParameters.Type == OperationType.Update)
                    {
                        IdentityServer.IncomingQueue.Enqueue(operation);
                        RealtyServer.IncomingQueue.Enqueue(operation);
                    }

                    else if (operation.OperationParameters.Direction == OperationDirection.Identity)
                        IdentityServer.IncomingQueue.Enqueue(operation);

                    else if (operation.OperationParameters.Direction == OperationDirection.Realty && IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                        RealtyServer.IncomingQueue.Enqueue(operation);

                    else
                    {
                        operation.IsSuccessfully = false;
                        output.Enqueue(operation);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"ViewModel(CheckQueue) {ex.Message}");
                    UpdateLog($"(CheckQueue) {ex.Message}");
                    operation.IsSuccessfully = false;
                    output.Enqueue(operation);
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
