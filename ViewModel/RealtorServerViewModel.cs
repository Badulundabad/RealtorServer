using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using RealtyModel.Model;
using RealtyModel.Service;
using RealtorServer.Model.NET;
using System.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RealtorServer.ViewModel
{
    class RealtorServerViewModel : INotifyPropertyChanged
    {
        private Boolean isRunning = false;
        private Queue<Operation> outcomingOperations = new Queue<Operation>();
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private Task run;


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

        public RealtorServerViewModel()
        {
            Log = new ObservableCollection<LogMessage>();
            Server = new LocalServer(Dispatcher.CurrentDispatcher, Log, outcomingOperations);
            RealtyServer = new RealtyServer(Dispatcher.CurrentDispatcher, Log, outcomingOperations);
            IdentityServer = new IdentityServer(Dispatcher.CurrentDispatcher, Log, outcomingOperations);

            RunCommand = new CustomCommand((obj) =>
            {
                IsRunning = true;
                cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = cancellationTokenSource.Token;
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => RunAsync(Dispatcher.CurrentDispatcher)));
                //Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () => await IdentityServer.RunAsync(cancellationToken)));
                //Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () => await RealtyServer.RunAsync(cancellationToken)));
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => Server.RunAsync(cancellationToken)));
            });
            StopCommand = new CustomCommand((obj) =>
            {
                IsRunning = false;
                cancellationTokenSource.Cancel();
            });
        }

        private async void RunAsync(Dispatcher dispatcher)
        {
            await Task.Run(() =>
            {
                try
                {
                    UpdateLog(dispatcher, " operation ran");

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        while (Server.IncomingQueue.Count > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Handle(Server.IncomingQueue.Dequeue());
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    UpdateLog(dispatcher, " operation canceled");
                }
                finally
                {
                    UpdateLog(dispatcher, " end");
                }
            });
        }
        private void Handle(Operation operation)
        {
            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (operation.OperationParameters.Direction == OperationDirection.Identity)
                    {
                        IdentityServer.IncomingQueue.Enqueue(operation);
                    }
                    else if (operation.OperationParameters.Direction == OperationDirection.Realty && IdentityServer.CheckAccess(operation.IpAddress, operation.Token))
                    {
                        RealtyServer.IncomingQueue.Enqueue(operation);
                    }
                    else
                    {
                        operation.IsSuccessfully = false;
                        outcomingOperations.Enqueue(operation);
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    UpdateLog(Dispatcher.CurrentDispatcher, $"(Handle) {ex.Message}");
                    operation.IsSuccessfully = false;
                    outcomingOperations.Enqueue(operation);
                }
            }
        }
        private void UpdateLog(Dispatcher dispatcher, String text)
        {
            //File.AppendAllLines("log.txt", new List<String>() { DateTime.Now.ToString("dd:MM:yy hh:mm") + "Server" + text });
            LogMessage logMessage = new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), "Server" + text);
            dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Add(logMessage);
            }));
        }
        private void UpdateLog(String text)
        {
            //File.AppendAllLines("log.txt", new List<String>() { DateTime.Now.ToString("dd:MM:yy hh:mm") + "Server" + text });
            LogMessage logMessage = new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), "Server" + text);
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
             {
                 Log.Add(logMessage);
             }));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] String prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
