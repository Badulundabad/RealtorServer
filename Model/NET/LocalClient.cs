using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;
using NLog;
using static RealtorServer.Model.Event.EventHandlers;
using RealtorServer.Model.Event;
using System.Diagnostics;

namespace RealtorServer.Model.NET
{
    public class LocalClient : INotifyPropertyChanged
    {
        #region Fields
        private object streamSendLocker = new object();
        private object socketSendLocker = new object();
        private String name = "";
        private String ipAddress = "none";
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
        public String IpAddress
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
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            OutcomingOperations = new OperationQueue();
            OutcomingOperations.Enqueued += (s, e) => SendOverStream();
        }

        public async void ReceiveOverStreamAsync()
        {
            await Task.Run((Action)(() =>
            {
                IsConnected = true;
                Int32 bytes = 0;
                try
                {
                    while (IsConnected)
                    {
                        if (stream.DataAvailable)
                        {
                            StringBuilder response = new StringBuilder();
                            do
                            {
                                Byte[] buffer = new Byte[4096];
                                Int32 byteCount = stream.Read(buffer, 0, buffer.Length);
                                bytes += byteCount;
                                String data = Encoding.UTF8.GetString(buffer);
                                response.Append(data, 0, byteCount);
                            }
                            while (stream.DataAvailable && IsConnected);
                            if (IsConnected)
                            {
                                GetOperation(response.ToString(), bytes);
                                bytes = 0;
                            }
                        }
                        Task.Delay(10).Wait();
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(ReceiveOverStreamAsync) {bytes}kbytes {ex.Message}");
                }
                finally
                {
                    LogInfo("has stopped");
                }
            }));
        }
        private void GetOperation(String message, Int32 byteCount)
        {
            Operation receivedOperation = JsonSerializer.Deserialize<Operation>(message);
            LogInfo($"received {byteCount}kbytes {receivedOperation.OperationNumber}");
            receivedOperation.IpAddress = IpAddress;
            OperationReceived?.Invoke(this, new OperationReceivedEventArgs(receivedOperation));
        }
        private void SendOverStream()
        {
            lock (streamSendLocker)
            {
                try
                {
                    if (OutcomingOperations != null)
                    {
                        Operation operation = OutcomingOperations.Dequeue();
                        if (operation.OperationParameters.Type == OperationType.Login && operation.IsSuccessfully)
                            Name = operation.Name;
                        else if (operation.OperationParameters.Type == OperationType.Logout && operation.IsSuccessfully)
                            Name = "";
                        String json = JsonSerializer.Serialize(operation);
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        stream.Write(data, 0, data.Length);
                        LogInfo($"sent {operation.OperationNumber}");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(SendOverStream) {ex.Message}");
                }
            }
        }
        public async void DisconnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    IsConnected = false;
                    OutcomingOperations = null;
                    Task.Delay(1000).Wait();
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    socket.Dispose();
                    stream.Close();
                    stream.Dispose();
                }
                catch (Exception ex)
                {
                    LogError($"(DisconnectAsync) {ex.Message}");
                }
                finally
                {
                    LogInfo("has disconnected");
                    Disconnected?.Invoke(this, new DisconnectedEventArgs());
                }
            });
        }
        private void OnPropertyChanged([CallerMemberName] String prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
        private void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} {IpAddress} {text}");
            logger.Info(text);
        }
        private void LogError(String text)
        {
            Debug.WriteLine($"{DateTime.Now} {IpAddress} {text}");
            logger.Error(text);
        }
        public async void CheckConnectionAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        if (GetSocketStatus())
                            Task.Delay(1000).Wait();
                        else
                        {
                            DisconnectAsync();
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    DisconnectAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{DateTime.Now} (ConnectAsync){ex.Message}");
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
        //Резервные методы - кандидаты на удаление
        public async void ReceiveOverSocketAsync()
        {
            await Task.Run(() =>
            {
                IsConnected = true;
                Int32 byteCount = 0;
                try
                {
                    while (IsConnected)
                    {
                        if (socket.Poll(100000, SelectMode.SelectRead))
                        {
                            StringBuilder response = new StringBuilder();
                            do
                            {
                                Byte[] buffer = new Byte[1024];
                                byteCount = socket.Receive(buffer);
                                String data = Encoding.UTF8.GetString(buffer);
                                response.Append(data, 0, byteCount);
                            }
                            while (socket.Available > 0);
                            GetOperation(response.ToString(), byteCount);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"(ReceiveDataAsync) {ex.Message}");
                }
                finally
                {
                    LogInfo("has stopped");
                }
            });
        }
        private void SendOverSocket()
        {
            lock (socketSendLocker)
            {
                try
                {
                    Operation operation = OutcomingOperations.Dequeue();
                    String json = JsonSerializer.Serialize(operation);
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    Int32 count = socket.Send(data);
                    LogInfo($"sent {json}");
                }
                catch (Exception ex)
                {
                    LogError($"(SendMessagesAsync) {ex.Message}");
                }
            }
        }
    }
}
