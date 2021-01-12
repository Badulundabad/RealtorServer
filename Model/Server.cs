using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel;

namespace RealtorServer.Model
{
    public class Server : INotifyPropertyChanged
    {
        Boolean isRunning;
        Socket listeningSocket;
        Dispatcher uiDispatcher;
        #region Properties
        public Boolean IsRunning
        {
            get => isRunning;
            private set
            {
                isRunning = value;
                OnProperyChanged();
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public List<Operation> OperationResults { get; set; }
        public ObservableCollection<Operation> IncomingOperations { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }
        public ObservableCollection<ClientHandler> OnlineClients { get; private set; }
        #endregion

        public Server(Dispatcher dispatcher)
        {
            uiDispatcher = dispatcher;
            OperationResults = new List<Operation>();
            Log = new ObservableCollection<LogMessage>();
            OnlineClients = new ObservableCollection<ClientHandler>();
            IncomingOperations = new ObservableCollection<Operation>();
        }

        public async void RunAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    IsRunning = true;
                    IPEndPoint serverAddress = new IPEndPoint(IPAddress.Any, 8005);
                    using (listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        listeningSocket.Bind(serverAddress);
                        listeningSocket.Listen(10);
                        UpdateLog("Server", "is running");

                        Socket clientSocket;
                        do
                        {
                            if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                            {
                                clientSocket = listeningSocket.Accept();
                                ClientHandler client = new ClientHandler(clientSocket, this);
                                client.PropertyChanged += (sender, e) =>
                                {
                                    if (e.PropertyName == "IsConnected")
                                    {
                                        if (!((ClientHandler)sender).IsConnected)
                                            uiDispatcher.BeginInvoke(new Action(() =>
                                            {
                                                OnlineClients.Remove((ClientHandler)sender);
                                            }));
                                    }
                                };
                                await uiDispatcher.BeginInvoke(new Action(() => OnlineClients.Add(client)));
                                await Task.Run(client.Connect);
                            }
                        }
                        while (IsRunning);
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog("Server", "(RunAsync)threw an exception: " + ex.Message);
                    Stop();
                }
                finally
                {
                    UpdateLog("Server", "was stopped");
                }
            });
        }
        public void Stop()
        {
            try
            {
                IsRunning = false;
                if (OnlineClients.Count > 0)
                    foreach (ClientHandler client in OnlineClients)
                    {
                        client.Disconnect();
                    }
            }
            catch (Exception ex)
            {
                UpdateLog("Server", "(Stop)threw an exception: " + ex.Message);
            }
        }
        internal void UpdateLog(String ipAddress, String message)
        {
            try
            {
                uiDispatcher.BeginInvoke(new Action(() =>
                {
                    Log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), ipAddress + " " + message));
                }));
            }
            catch (Exception ex)
            {
                UpdateLog(ipAddress, "(UpdateLog)threw an exception: " + ex.Message);
            }
        }
        internal void UpdateOperationQueue(String ipAddress, String operationJson)
        {
            try
            {
                Operation operation = JsonSerializer.Deserialize<Operation>(operationJson);
                operation.IpAddress = ipAddress;
                uiDispatcher.BeginInvoke(new Action(() =>
                {
                    IncomingOperations.Add(operation);
                }));
            }
            catch (Exception ex)
            {
                UpdateLog(ipAddress, "(Stop)threw an exception: " + ex.Message);
            }
        }
        private void OnProperyChanged([CallerMemberName] String prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }
}
