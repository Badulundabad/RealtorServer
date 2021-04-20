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

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private Socket listeningSocket = null;
        private List<LocalClient> clients = new List<LocalClient>();
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
                        using (Timer queueChecker = new Timer((o) => CheckOutQueue(), new object(), 0, 500))
                        {
                            logger.Info("Local server has ran");
                            UpdateLog("has ran");
                            while (IsRunning)
                            {
                                if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                                    ConnectClient();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Local server(RunAsync) {ex.Message}");
                    UpdateLog($"(RunAsync) {ex.Message}");
                }
                finally
                {
                    DisconnectAllClients();
                    logger.Info("Local server has stopped");
                    UpdateLog("has stopped");
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
                    logger.Error($"Local server (RunUDPMarkerAsync) {ex.Message}");
                    UpdateLog($"(RunUDPMarkerAsync) {ex.Message}");
                }
                finally
                {
                    socket.Dispose();
                    socket.Close();
                    logger.Info("UDP marker has stopped");
                }
            });
        }

        private void ConnectClient()
        {
            Socket clientSocket = listeningSocket.Accept();
            var client = new LocalClient(dispatcher, clientSocket, log, IncomingQueue);
            client.ConnectAsync();
            clients.Add(client);
            logger.Info($"{client.IpAddress} has connected");
            UpdateLog($"{client.IpAddress} has connected");
        }
        private void DisconnectAllClients()
        {
            foreach (LocalClient client in clients)
                client.Disconnect();
            LocalClient[] clientArray = clients.ToArray();
            foreach (LocalClient client in clientArray)
                clients.Remove(client);
        }
        private void CheckOutQueue()
        {
            try
            {
                while (IsRunning && outcomingQueue.Count > 0)
                {
                    Operation operation = outcomingQueue.Dequeue();
                    if (operation != null)
                    {
                        foreach (LocalClient client in clients)
                        {
                            if (operation.IpAddress == "broadcast")
                                client.SendQueue.Enqueue(operation);
                            else if (operation.IpAddress == client.IpAddress)
                                client.SendQueue.Enqueue(operation);
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
