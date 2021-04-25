using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;
using NLog;
using System.Windows;

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
        private Socket socket = null;
        private NetworkStream stream = null;
        private Dispatcher dispatcher = null;
        private Queue<Operation> incomingOperations = null;
        private ObservableCollection<LogMessage> log = null;
        private static Logger logger = LogManager.GetCurrentClassLogger();
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

        public LocalClient(Dispatcher dispatcher, Socket socket, ObservableCollection<LogMessage> log, Queue<Operation> input)
        {
            this.log = log;
            this.socket = socket;
            this.dispatcher = dispatcher;
            incomingOperations = input;
            stream = new NetworkStream(socket);
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            OutcomingOperations = new OperationQueue();
            OutcomingOperations.Enqueued += (s, e) => SendOverSocket();
            //OutcomingOperations.Enqueued += (s, e) => SendOverStream();
        }

        public async void ReceiveOverStreamAsync()
        {
            await Task.Run(() =>
            {
                IsConnected = true;
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
                                try
                                {
                                    Int32 byteCount = stream.Read(buffer, 0, buffer.Length);
                                    String data = Encoding.UTF8.GetString(buffer);
                                    response.Append(data, 0, byteCount);
                                }
                                catch (Exception ex)
                                {
                                    UpdateLog($"{IpAddress}(ReceiveDataAsync do-while) {ex.Message}");
                                    logger.Error($"{IpAddress}(ReceiveDataAsync do-while)\n{ex.Message}\n{buffer.Length}");
                                }
                            }
                            while (stream.DataAvailable);
                            GetOperation(response.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{IpAddress}(ReceiveDataAsync) {ex.Message}");
                    UpdateLog($"{IpAddress}(ReceiveDataAsync) {ex.Message}");
                }
                finally
                {
                    logger.Info($"{IpAddress} has disconnected");
                    UpdateLog($"{IpAddress} has disconnected");
                }
            });
        }
        private void SendOverStream()
        {
            lock (streamSendLocker)
            {
                try
                {
                    Operation operation = OutcomingOperations.Dequeue();
                    String json = JsonSerializer.Serialize(operation);
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    stream.Write(data, 0, data.Length);
                    logger.Info($"{IpAddress} sent {json}");
                    UpdateLog($"sent {json}");
                }
                catch (Exception ex)
                {
                    logger.Error($"{ipAddress}(SendMessagesAsync) {ex.Message}");
                    UpdateLog($"(SendMessagesAsync) {ex.Message}");
                }
            }
        }
        public async void ReceiveOverSocketAsync()
        {
            await Task.Run(() =>
            {
                IsConnected = true;
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
                                Int32 byteCount = socket.Receive(buffer);
                                String data = Encoding.UTF8.GetString(buffer);
                                response.Append(data, 0, byteCount);
                            }
                            while (socket.Available > 0);
                            GetOperation(response.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{IpAddress}(ReceiveDataAsync) {ex.Message}");
                    UpdateLog($"{IpAddress}(ReceiveDataAsync) {ex.Message}");
                }
                finally
                {
                    logger.Info($"{IpAddress} has disconnected");
                    UpdateLog($"{IpAddress} has disconnected");
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
                    socket.Send(data);
                    logger.Info($"{IpAddress} sent {json}");
                    UpdateLog($"sent {json}");
                }
                catch (Exception ex)
                {
                    logger.Error($"{ipAddress}(SendMessagesAsync) {ex.Message}");
                    UpdateLog($"(SendMessagesAsync) {ex.Message}");
                }
            }
        }

        private void GetOperation(String message)
        {
            Operation receivedOperation = JsonSerializer.Deserialize<Operation>(message);
            receivedOperation.IpAddress = IpAddress;
            incomingOperations.Enqueue(receivedOperation);
            logger.Info($"{IpAddress} received {message}");
            UpdateLog($"received {message}");
        }

        public void Disconnect()
        {
            IsConnected = false;
        }
        private void UpdateLog(String text)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), $"{ipAddress} {text}"));
            }));
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
