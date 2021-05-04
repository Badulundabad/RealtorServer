using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;
using NLog;
using System.Diagnostics;
using System.Linq;

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private Socket listeningSocket;
        private ObservableCollection<LocalClient> clients = new ObservableCollection<LocalClient>();

        public ObservableCollection<LocalClient> Clients
        {
            get => clients;
            set => clients = value;
        }

        public LocalServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Dispatcher = dispatcher;
            IncomingOperations = new OperationQueue();
            OutcomingOperations = new OperationQueue();
            OutcomingOperations.Enqueued += (s, e) => HandleAsync();
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
                        LogInfo("HAS RAN");
                        while (IsRunning)
                            if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                                ConnectClientAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(RunAsync) {ex.Message}");
                }
                finally
                {
                    DisconnectAllClients();
                    LogInfo("HAS STOPPED");
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

                        LogInfo("UDP marker has ran");

                        while (IsRunning)
                        {
                            if (socket.Poll(1000000, SelectMode.SelectRead))
                            {
                                Int32 byteCount = socket.ReceiveFrom(buffer, ref endPoint);
                                if (byteCount == 1 && buffer[0] == 0x10)
                                    socket.SendTo(new byte[] { 0x20 }, endPoint);
                            }
                        }
                        socket.Shutdown(SocketShutdown.Both);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(RunUDPMarkerAsync) {ex.Message}");
                }
                finally
                {
                    LogInfo("UDP marker has stopped");
                }
            });
        }

        private async void ConnectClientAsync()
        {
            await Task.Run(() =>
            {
                LocalClient client = null;
                try
                {
                    Socket clientSocket = listeningSocket.Accept();
                    client = new LocalClient(clientSocket);
                    LogInfo($"HAS CONNECTED {client.IpAddress}");

                    client.CheckConnectionAsync();
                    client.ReceiveAsync();

                    AddClientAsync(client);
                    client.OperationReceived += (s, e) => IncomingOperations.Enqueue(e.Operation);
                    client.Disconnected += (s, e) => RemoveClientAsync((LocalClient)s);
                }
                catch (Exception ex)
                {
                    LogError($"HASN'T CONNECTED {client?.IpAddress} {ex.Message}");
                }
            });
        }
        private async void AddClientAsync(LocalClient client)
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    clients.Add(client);
                });
                LogInfo($"has added {client.IpAddress}. Clients count = {Clients.Count}");
            });
        }
        private async void RemoveClientAsync(LocalClient client)
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    clients.Remove(client);
                });
                LogInfo($"has removed {client.IpAddress}. Clients count = {Clients.Count}");
            });
        }
        private async void HandleAsync()
        {
            await Task.Run(() =>
            {
                lock (handleLocker)
                {
                    if (OutcomingOperations != null && OutcomingOperations.Count > 0)
                    {
                        try
                        {
                            Operation operation = null;
                            if (OutcomingOperations != null && OutcomingOperations.Count > 0)
                                operation = OutcomingOperations.Dequeue();

                            if (clients != null && clients.Count > 0 && operation != null)
                            {
                                if (operation.IsBroadcast)
                                {
                                    LogInfo($"has redirected {operation.OperationNumber} to broadcast");
                                    foreach (LocalClient client in clients)
                                        client.OutcomingOperations.Enqueue(operation);
                                }
                                else
                                    foreach (LocalClient client in clients)
                                        if (operation.IpAddress == client.IpAddress.ToString())
                                        {
                                            LogInfo($"has redirected {operation.OperationNumber} to {operation.IpAddress}");
                                            client.OutcomingOperations.Enqueue(operation);
                                        }
                            }
                            else LogInfo($"hasn't find destination for {operation.OperationNumber}");
                        }
                        catch (Exception ex)
                        {
                            LogError($"(Handle) {ex.Message}");
                        }
                    }
                }
            });
        }
        public void DisconnectAllClients()
        {
            if (clients != null && clients.Count > 0)
                foreach (LocalClient client in clients.ToArray())
                    client.DisconnectAsync();
        }
    }
}
