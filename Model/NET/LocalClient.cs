using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RealtyModel.Model;
using RealtyModel.Model.Operations;
using RealtorServer.Model.Event;
using NLog;

namespace RealtorServer.Model.NET
{
    public class LocalClient : INotifyPropertyChanged
    {
        #region Fields
        private object sendLocker = new object();
        private String name;
        private Boolean isConnected;
        private IPAddress ipAddress;
        private NetworkStream stream;
        private TcpClient tcpClient;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public event DisconnectedEventHandler Disconnected;
        public event PropertyChangedEventHandler PropertyChanged;
        private LocalServer server;
        #endregion
        #region Properties
        public String Name
        {
            get => name;
            private set
            {
                name = value;
                OnPropertyChanged();
            }
        }
        public Boolean IsConnected
        {
            get => isConnected;
            private set
            {
                isConnected = value;
            }
        }
        public IPAddress IpAddress
        {
            get => ipAddress;
            private set
            {
                ipAddress = value;
                OnPropertyChanged();
            }
        }
        public Queue<Operation> OutcomingOperations { get; private set; }
        #endregion

        public LocalClient(TcpClient client, LocalServer server)
        {
            this.server = server;
            tcpClient = client;
            stream = tcpClient.GetStream();
            IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            OutcomingOperations = new Queue<Operation>();
        }

        public void Run()
        {
            IsConnected = true;
            CheckConnectionAsync();
            CheckQueueAsync();
            ReceiveAsync();
        }
        private async void ReceiveAsync()
        {
            await Task.Run((Action)(() =>
            {
                try
                {
                    while (IsConnected)
                    {
                        if (stream.DataAvailable)
                        {
                            List<byte> byteList = new List<byte>();
                            if (ReceiveData(byteList))
                                HandleResponse(byteList.ToArray());
                        }
                    }
                }
                finally
                {
                    LogInfo("STOPPED");
                }
            }));
        }
        private bool ReceiveData(List<byte> byteList)
        {
            try
            {
                LogInfo($"started to receive");
                Int32 size = GetSize();
                while (byteList.Count < size)
                {
                    if (stream.DataAvailable)
                    {
                        byte[] buffer = new byte[8192];
                        int bytes = stream.Read(buffer, 0, buffer.Length);
                        LogInfo($"Receive {bytes} bytes");
                        byte[] receivedData = new byte[bytes];
                        Array.Copy(buffer, receivedData, bytes);
                        byteList.AddRange(receivedData);
                    }
                    else throw new Exception("available data was 0 before a size has reached");
                }
                LogInfo($"finished to receive");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"(ReceiveData) {ex.Message}");
                return false;
            }
        }
        private int GetSize()
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, buffer.Length);
            return BitConverter.ToInt32(buffer, 0);
        }
        private void HandleResponse(Byte[] data)
        {
            try
            {
                Operation operation = BinarySerializer.Deserialize<Operation>(data);
                operation.IpAddress = IpAddress.ToString();
                server.IncomingOperations.Enqueue(operation);
                LogInfo($"RECEIVED {data.Length} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Action} {operation.Parameters.Target}");
            }
            catch (Exception ex)
            {
                LogError($"(HandleResponseAsync) {ex.Message}");
            }
        }

        public async void CheckQueueAsync()
        {
            await Task.Run(() =>
            {
                while (IsConnected)
                {
                    if (OutcomingOperations.Count > 0)
                        SendAsync();
                    Task.Delay(100).Wait();
                }
            });
        }
        private void SendAsync()
        {
            try
            {
                Operation operation = OutcomingOperations.Dequeue();
                if (operation.Parameters.Action == Act.Login && operation.IsSuccessfully)
                    Name = operation.Name;
                else if (operation.Parameters.Action == Act.Logout && operation.IsSuccessfully)
                    Name = "";
                operation.Number = (Guid.NewGuid()).ToString();
                Byte[] data = BinarySerializer.Serialize(operation);
                Byte[] dataSize = BitConverter.GetBytes(data.Length);

                stream.Write(dataSize, 0, 4);
                stream.Write(data, 0, data.Length);
                LogInfo($"SENT {data.Length} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Action} {operation.Parameters.Target}");
            }
            catch (SocketException sockEx)
            {
                LogError($"(SendAsync) {sockEx.ErrorCode} {sockEx.Message}");
            }
            catch (Exception ex)
            {
                LogError($"(SendAsync) {ex.Message}");
            }
        }

        public async void CheckConnectionAsync()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        if (GetSocketStatus())
                            Task.Delay(1000).Wait();
                        else
                        {
                            LogInfo("SOCKET WAS UNAVAILABLE");
                            DisconnectAsync();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"(ConnectAsync) { ex.Message}");
                        //DisconnectAsync();
                    }
                }
            });
        }
        private bool GetSocketStatus()
        {
            bool part1 = tcpClient.Client.Poll(1000, SelectMode.SelectRead);
            bool part2 = (tcpClient.Client.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }
        public async void DisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    IsConnected = false;
                    OutcomingOperations = new OperationQueue();
                    Task.Delay(1000).Wait();
                    tcpClient.Close();
                    stream.Close();
                    stream.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"(DisconnectAsync) {ex.Message}");
                }
                finally
                {
                    LogInfo("\nHAS DISCONNECTED\n");
                    Disconnected?.Invoke(this, new DisconnectedEventArgs());
                }
            });
        }

        private void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} {IpAddress}     {text}");
            logger.Info(text);
        }
        private void LogError(String text)
        {
            Debug.WriteLine($"\n{DateTime.Now} ERROR {IpAddress}     {text}\n");
            logger.Error(text);
        }
        private void OnPropertyChanged([CallerMemberName] String prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }
}
