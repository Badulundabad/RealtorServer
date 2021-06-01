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
using RealtyModel.Model.Operations;
using System.Collections.Generic;

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private TcpListener tcpListener;
        public ObservableCollection<LocalClient> Clients { get; set; }
        public LocalServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Clients = new ObservableCollection<LocalClient>();
            Dispatcher = dispatcher;
            IncomingOperations = new Queue<Operation>();
            OutcomingOperations = new Queue<Operation>();
        }

        public override async void RunAsync()
        {
            IsRunning = true;
            await Task.Run(() =>
            {
                try
                {
                    RunListener();
                    while (IsRunning)
                    {
                        try
                        {
                            if (tcpListener.Pending())
                                ConnectClientAsync();
                            Task.Delay(100).Wait();
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
                finally
                {
                    DisconnectAllClients();
                    LogInfo("HAS STOPPED");
                }
            });
        }

        private void RunListener()
        {
            IPAddress ip = null;
            if (Debugger.IsAttached)
                ip = IPAddress.Loopback;
            else
                ip = IPAddress.Parse("192.168.1.250");
            tcpListener = new TcpListener(ip, 15000);
            tcpListener.Start();
            LogInfo($"HAS RAN on {ip}");
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
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    client = new LocalClient(tcpClient, this);
                    LogInfo($"HAS CONNECTED {client.IpAddress}");

                    AddClientAsync(client);
                    client.Disconnected += (s, e) => OnClientDisconnectedAsync((LocalClient)s);
                    client.Run();
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
                    Clients.Add(client);
                });
                LogInfo($"has added {client.IpAddress}. Clients count = {Clients.Count}");
            });
        }
        private async void OnClientDisconnectedAsync(LocalClient client)
        {
            await Task.Run(() =>
            {
                Boolean isRemoved = false;
                Boolean isLoggedOut = false;
                while (!isRemoved && !isLoggedOut)
                {
                    if (Clients != null && Clients.Count > 0)
                        Dispatcher.Invoke(() =>
                        {
                            isRemoved = Clients.Remove(client);
                        });
                    else isRemoved = true;
                    
                }
                LogInfo($"has removed {client.IpAddress}. Clients count = {Clients.Count}");
            });
        }

        public async void CheckQueueAsync()
        {
            await Task.Run(() =>
            {
                while (IsRunning)
                {
                    if (OutcomingOperations.Count > 0)
                        Handle();
                    Task.Delay(100).Wait();
                }
            });
        }
        private void Handle()
        {
            try
            {
                Operation operation = OutcomingOperations.Dequeue();
                LogInfo($"started to handle {operation.Parameters.Action} {operation.Parameters.Target}");

                if (Clients.Count > 0)
                {
                    if (operation.IsBroadcast)
                    {
                        LogInfo($"has redirected {operation.Number} to broadcast");
                        foreach (LocalClient client in Clients)
                            client.OutcomingOperations.Enqueue(operation);
                    }
                    else
                        foreach (LocalClient client in Clients)
                            if (operation.IpAddress == client.IpAddress.ToString())
                            {
                                client.OutcomingOperations.Enqueue(operation);
                                LogInfo($"has redirected {operation.Number} to {operation.IpAddress}");
                            }
                }
                else LogInfo($"hasn't find destination for {operation.Number}");
            }
            catch (Exception ex)
            {
                LogError($"(Handle) {ex.Message}");
            }
        }

        public void DisconnectAllClients()
        {
            if (Clients != null && Clients.Count > 0)
                foreach (LocalClient client in Clients.ToArray())
                    client.DisconnectAsync();
        }
    }
}
