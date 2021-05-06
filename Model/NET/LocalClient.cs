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
                        if (socket.Available > 0)
                        {
                            byte[] buffer = new byte[256];
                            StringBuilder response = new StringBuilder();
                            Int32 expectedSize = 0;

                            do
                            {
                                socket.Receive(buffer);
                                String received = Encoding.UTF8.GetString(buffer);

                                if (expectedSize == 0)
                                {
                                    String[] parts = received.Split(new String[] { ";" }, StringSplitOptions.None);
                                    expectedSize = Int32.Parse(parts[0]);
                                    response.Append(parts[1]);
                                }
                                else if (received.Contains("<<<<"))
                                {
                                    String[] parts = received.Split(new String[] { "<<<<" }, StringSplitOptions.None);
                                    response.Append(parts[0]);
                                }
                                else response.Append(received);
                            }
                            while (socket.Available > 0);

                            if (response.Length == expectedSize)
                            {
                                HandleResponseAsync(response.ToString(), expectedSize);
                            }
                            else
                            {
                                LogInfo($"RECEIVED WRONG BYTE COUNT: {response.Length} OF {expectedSize}");
                                //отправить на повтор
                            }
                        }
                        Task.Delay(10).Wait();
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
                    Operation operation = JsonSerializer.Deserialize<Operation>(data);
                    LogInfo($"RECEIVED {expectedSize} BYTES {operation.Number} - {operation.Parameters.Direction} {operation.Parameters.Type} {operation.Parameters.Target}");
                    operation.IpAddress = IpAddress.ToString();
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
                            if (operation.Parameters.Type == OperationType.Login && operation.IsSuccessfully)
                                Name = operation.Name;
                            else if (operation.Parameters.Type == OperationType.Logout && operation.IsSuccessfully)
                                Name = "";

                            String json = JsonSerializer.Serialize(operation);
                            Byte[] data = Encoding.UTF8.GetBytes($"{json.Length};{json}<<<<");

                            socket.Send(data);
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
                        Task.Delay(500).Wait();
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
