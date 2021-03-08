using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private Socket listeningSocket = null;
        private List<LocalClient> clients = new List<LocalClient>();
        public ObservableCollection<Task> OnlineClients { get; private set; }

        public LocalServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
            OnlineClients = new ObservableCollection<Task>();
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
                        Socket clientSocket;
                        using (Timer queueChecker = new Timer((o) => CheckOutQueue(), new object(), 0, 500))
                        {
                            UpdateLog("has ran");
                            while (IsRunning)
                            {
                                if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                                {
                                    clientSocket = listeningSocket.Accept();
                                    var client = new LocalClient(dispatcher, clientSocket, log, IncomingQueue);
                                    client.ConnectAsync();
                                    clients.Add(client);
                                    UpdateLog("new client has connected");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog($"(RunAsync) {ex.Message}");
                }
                finally
                {
                    UpdateLog("is trying to stop");
                    foreach (LocalClient client in clients)
                    {
                        client.Disconnect();
                    }
                    LocalClient[] clientArray = clients.ToArray();
                    System.Timers.Timer timer = new System.Timers.Timer();
                    timer.AutoReset = false;
                    if (clients.Count == 0)
                        timer.Interval = 100;
                    else
                        timer.Interval = 150 * clients.Count;
                    timer.Elapsed += (o, e) =>
                    {
                        foreach (LocalClient client in clientArray)
                        {
                            clients.Remove(client);
                        }
                        UpdateLog("has stopped");
                    };
                    timer.Enabled = true;
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
                    UpdateLog($"(RunUDPMarkerAsync) {ex.Message}");
                }
            });
        }

        private void CheckOutQueue()
        {
            try
            {
                //UpdateLog("is checking a queue");
                while (IsRunning && outcomingQueue.Count > 0)
                {
                    Operation operation = outcomingQueue.Dequeue();
                    if (operation != null)
                    {
                        foreach (LocalClient client in clients)
                        {
                            if (operation.IpAddress == client.IpAddress)
                            {
                                client.SendQueue.Enqueue(operation);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateLog($"(CheckOutQueue) {ex.Message}");
            }
        }
    }
}
