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
using static RealtorServer.Model.Event.EventHandlers;
using System.Text.Json;

namespace RealtorServer.Model.NET
{
    public class LocalClient : INotifyPropertyChanged
    {
        #region Fields
        private object sendLocker = new object();
        private String name = "";
        private IPAddress ipAddress = null;
        private Boolean isConnected = false;
        private Socket socket;
        private NetworkStream stream;
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
        public IPAddress IpAddress
        {
            get => ipAddress;
            private set
            {
                ipAddress = value;
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
        public OperationQueue OutcomingOperations { get; private set; }
        #endregion

        public LocalClient(Socket socket)
        {
            this.socket = socket;
            stream = new NetworkStream(socket, true);
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address;
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
                            Byte[] buffer = new Byte[32];
                            stream.Read(buffer, 0, 4);
                            Int32 expectedSize = BitConverter.ToInt32(buffer, 0);
                            Int32 bytesReceived = 0;
                            StringBuilder response = new StringBuilder();

                            do
                            {
                                bytesReceived += stream.Read(buffer, 0, buffer.Length);
                                response.Append(Encoding.UTF8.GetString(buffer));
                            }
                            while (bytesReceived < expectedSize);

                            HandleResponseAsync(response.ToString(), expectedSize);
                            response.Length = 0;
                            response.Capacity = 0;
                            GC.Collect();
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
        private async void HandleResponseAsync(String data, int expectedSize)
        {
            await Task.Run(() =>
            {
                try
                {
                    data = data.Split('#')[1];
                    if (data.Length + 2 == expectedSize)
                    {
                        Operation operation = JsonSerializer.Deserialize<Operation>(data);
                        LogInfo($"RECEIVED {expectedSize} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Type} {operation.Parameters.Target}");
                        operation.IpAddress = IpAddress.ToString();
                        OperationReceived?.Invoke(this, new OperationReceivedEventArgs(operation));
                    }
                    else LogInfo($"RECEIVED WRONG BYTE COUNT: {data.Length} OF {expectedSize}\n{data}");
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
                            if (operation.Parameters.Type == OperationType.Login && operation.IsSuccessfully)
                                Name = operation.Name;
                            else if (operation.Parameters.Type == OperationType.Logout && operation.IsSuccessfully)
                                Name = "";
                            operation.Number = Guid.NewGuid();
                            String json = "#" + JsonSerializer.Serialize(operation) + "#";
                            Byte[] data = Encoding.UTF8.GetBytes(json);
                            Byte[] dataSize = BitConverter.GetBytes(data.Length);

                            stream.Write(dataSize, 0, 4);
                            stream.Write(data, 0, data.Length);
                            LogInfo($"SENT {json.Length} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Type} {operation.Parameters.Target}");
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
                bool part1 = socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (socket.Available == 0);
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
                    socket.Shutdown(SocketShutdown.Both);
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
