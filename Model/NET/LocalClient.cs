using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using RealtyModel.Model;
using NLog;
using RealtorServer.Model.Event;
using System.Text.Json;
using RealtyModel.Model.Operations;
using System.Collections.Generic;

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
        public event OperationReceivedEventHandler OperationReceived;
        public event PropertyChangedEventHandler PropertyChanged;
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
        public OperationQueue OutcomingOperations { get; private set; }
        #endregion

        public LocalClient(TcpClient client)
        {
            tcpClient = client;
            stream = tcpClient.GetStream();
            IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            OutcomingOperations = new OperationQueue();
            OutcomingOperations.Enqueued += (s, e) => SendAsync();
        }

        public async void ReceiveAsync()
        {
            await Task.Run((Action)(() =>
            {
                IsConnected = true;
                try
                {
                    while (IsConnected)
                    {
                        if (stream.DataAvailable)
                        {
                            List<byte> byteList = new List<byte>();
                            int size = GetSize();
                            bool isSuccessful = ReceiveData(byteList, size);
                            HandleResponseAsync(byteList.ToArray(), size);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(ReceiveAsync) {ex.Message}");
                }
                finally
                {
                    LogInfo("STOPPED");
                }
            }));
        }
        private bool ReceiveData(List<byte> byteList, int size)
        {
            try
            {
                while (byteList.Count < size)
                {
                    byte[] buffer = new byte[8192];
                    int bytes = stream.Read(buffer, 0, buffer.Length);
                    LogInfo($"Receive {bytes} bytes");
                    byte[] receivedData = new byte[bytes];
                    Array.Copy(buffer, receivedData, bytes);
                    byteList.AddRange(receivedData);
                }
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
        private async void HandleResponseAsync(Byte[] data, int expectedSize)
        {
            await Task.Run(() =>
            {
                try
                {
                    Operation operation = BinarySerializer.Deserialize<Operation>(data);
                    operation.IpAddress = IpAddress.ToString();
                    if (data.Length == expectedSize)
                        LogInfo($"RECEIVED {expectedSize} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Action} {operation.Parameters.Target}");
                    else
                        LogInfo($"RECEIVED WRONG BYTE COUNT: data - {data.Length} OF {expectedSize}");
                    OperationReceived?.Invoke(this, new OperationReceivedEventArgs(operation));
                }
                catch (Exception ex)
                {
                    LogError($"(HandleResponseAsync) {ex.Message}");
                }
            });
        }
        private async void SendAsync()
        {
            await Task.Run(() =>
            {
                lock (sendLocker)
                {
                    if (OutcomingOperations != null && OutcomingOperations.Count > 0)
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
                        Task.Delay(100).Wait();
                    }
                }
            });
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
            bool GetSocketStatus()
            {
                bool part1 = tcpClient.Client.Poll(1000, SelectMode.SelectRead);
                bool part2 = (tcpClient.Client.Available == 0);
                if (part1 && part2)
                    return false;
                else
                    return true;
            }
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
