using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;
using NLog;
using System.Globalization;
using System.Diagnostics;
using System.Linq;

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private Socket listeningSocket;
        private ObservableCollection<LocalClient> clients = new ObservableCollection<LocalClient>();
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public ObservableCollection<LocalClient> Clients
        {
            get => clients;
            set => clients = value;
        }
        public ObservableCollection<Task> OnlineClients { get; private set; }

        public LocalServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log) : base(dispatcher, log)
        {
            Log = log;
            Dispatcher = dispatcher;
            OutcomingOperations = new OperationQueue();
            IncomingOperations = new OperationQueue();
            OnlineClients = new ObservableCollection<Task>();
            OutcomingOperations.Enqueued += (s, e) => Handle();
        }

        public override async void RunAsync()
        {
            IsRunning = true;
            await Task.Run(() =>
            {
                try
                {
                    IPEndPoint serverAddress = new IPEndPoint(IPAddress.Any, 8005);
                    using (listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        listeningSocket.Bind(serverAddress);
                        listeningSocket.Listen(10);
                        logger.Info("LocalServer has ran");
                        Debug.WriteLine("LocalServer has ran");
                        //UpdateLog("has ran");
                        while (IsRunning)
                            if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                                ConnectClient();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LocalServer(RunAsync) {ex.Message}");
                    logger.Error($"LocalServer(RunAsync) {ex.Message}");
                    //UpdateLog($"(RunAsync) {ex.Message}");
                }
                finally
                {
                    DisconnectAllClients();
                    Debug.WriteLine("LocalServer has stopped");
                    logger.Info("LocalServer has stopped");
                    //UpdateLog("has stopped");
                }
            });
        }
        public async void RunUDPMarkerAsync()
        {
            await Task.Run(() =>
            {
                Socket socket = null;
                try
                {
                    using (socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, true);
                        socket.EnableBroadcast = true;
                        socket.Bind(new IPEndPoint(IPAddress.Any, 8080));

                        byte[] buffer = new byte[socket.ReceiveBufferSize];
                        EndPoint endPoint = new IPEndPoint(IPAddress.None, 0);

                        Debug.WriteLine("UDP marker has ran");
                        logger.Info("UDP marker has ran");
                        while (IsRunning)
                        {
                            if (socket.Poll(1000000, SelectMode.SelectRead))
                            {
                                Int32 byteCount = socket.ReceiveFrom(buffer, ref endPoint);
                                if (byteCount == 1 && buffer[0] == 0x10)
                                    socket.SendTo(new byte[] { 0x20 }, endPoint);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LocalServer (RunUDPMarkerAsync) {ex.Message}");
                    logger.Error($"LocalServer (RunUDPMarkerAsync) {ex.Message}");
                    //UpdateLog($"(RunUDPMarkerAsync) {ex.Message}");
                }
                finally
                {
                    socket.Dispose();
                    socket.Close();
                    Debug.WriteLine("UDP marker has stopped");
                    logger.Info("UDP marker has stopped");
                }
            });
        }

        private void ConnectClient()
        {
            Socket clientSocket = listeningSocket.Accept();
            var client = new LocalClient(Dispatcher, clientSocket, Log);
            //client.ReceiveOverSocketAsync();
            client.ReceiveOverStreamAsync();
            AddClient(client);
            client.OperationReceived += (s, e) => IncomingOperations.Enqueue(e.Operation);
            client.Disconnected += (s, e) => RemoveClient((LocalClient)s);
            Debug.WriteLine($"{client.IpAddress} has connected");
            logger.Info($"{client.IpAddress} has connected");
            //UpdateLog($"{client.IpAddress} has connected");
        }
        public void DisconnectAllClients()
        {
            if (clients != null && clients.Count > 0)
                foreach (LocalClient client in clients.ToArray())
                    client.Disconnect();
        }
        private void AddClient(LocalClient client)
        {
            Dispatcher.Invoke(() =>
            {
                clients.Add(client);
            });
        }
        private void RemoveClient(LocalClient client)
        {
            Dispatcher.Invoke(() =>
            {
                clients.Remove(client);
            });
        }
        private void Handle()
        {
            lock (handleLocker)
            {
                while (true)
                {
                    try
                    {
                        if (clients != null && clients.Count > 0 && OutcomingOperations.Count > 0)
                        {
                            Operation operation = OutcomingOperations.Dequeue();
                            if (operation != null)
                            {
                                if (operation.IpAddress == "broadcast")
                                    foreach (LocalClient client in clients)
                                    {
                                        Debug.WriteLine($"LocalServer has handled {operation.OperationNumber}");
                                        logger.Info($"LocalServer has handled {operation.OperationNumber}");
                                        client.OutcomingOperations.Enqueue(operation);
                                    }
                                else
                                    foreach (LocalClient client in clients)
                                        if (operation.IpAddress == client.IpAddress)
                                        {
                                            Debug.WriteLine($"LocalServer has handled {operation.OperationNumber}");
                                            logger.Info($"LocalServer has handled {operation.OperationNumber}");
                                            client.OutcomingOperations.Enqueue(operation);
                                        }
                            }
                        }
                        else
                            break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LocalServer(Handle) {ex.Message}");
                        Debug.WriteLine($"LocalServer(Handle) {ex.InnerException}");
                        logger.Info($"LocalServer(Handle) {ex.Message}");
                        //UpdateLog($"(Handle) {ex.Message}");
                    }
                }
            }
        }
    }
}
