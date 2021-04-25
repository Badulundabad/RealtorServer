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
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
        }

        public async void RunAsync()
        {
            await Task.Run(() =>
            {
                OutcomingOperations = new OperationQueue();
                stream = new NetworkStream(socket);
                isConnected = true;
                try
                {
                    ReceiveMessages();
                }
                catch (Exception ex)
                {
                    logger.Error($"{IpAddress}(ConnectAsync) {ex.Message}");
                    UpdateLog($"(Connect) {ex.Message}");
                }
                finally
                {
                    logger.Info($"{IpAddress} has disconnected");
                    UpdateLog("has disconnected");
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
            });
        }
        public void Disconnect()
        {
            isConnected = false;
        }

        private void ReceiveOperations()
        {
            while (isConnected)
            {
                if (stream.DataAvailable)
                {
                    try
                    {
                        StringBuilder response = new StringBuilder();
                        do
                        {
                            Byte[] buffer = new Byte[1024];
                            try
                            {
                                Int32 byteCount = stream.Read(buffer, 0, buffer.Length);
                                String data = Encoding.UTF8.GetString(buffer);
                                response.Append(data, 0, byteCount);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"(Receive-do-while)\n{ex.Message}\n{buffer.Length}");
                            }
                        }
                        while (stream.DataAvailable);
                        AddMessageToQueue(response.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"(Receive) {ex.Message}");
                    }
                }
            }
        }
        private void AddMessageToQueue(String message)
        {
            Operation receivedOperation = JsonSerializer.Deserialize<Operation>(message);
            receivedOperation.IpAddress = IpAddress;
            incomingOperations.Enqueue(receivedOperation);
            logger.Info($"{IpAddress} received {message}");
            UpdateLog($"received {message}");
        }
        private void CheckOutQueue()
        {
            while (isConnected && OutcomingOperations.Count > 0)
            {
                try
                {
                    Operation operation = OutcomingOperations.Dequeue();
                    if (operation != null)
                    {
                        String json = JsonSerializer.Serialize(operation);
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        stream.Write(data, 0, data.Length);

                        logger.Info($"{IpAddress} sent {json}");
                        UpdateLog($"sent {json}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"{ipAddress}(SendMessagesAsync) {ex.Message}");
                    UpdateLog($"(SendMessagesAsync) {ex.Message}");
                }
            }
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
