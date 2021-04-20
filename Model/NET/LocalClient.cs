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

namespace RealtorServer.Model.NET
{
    public class LocalClient : INotifyPropertyChanged
    {
        #region Fields
        private String name = "";
        private String ipAddress = "";
        private Boolean isConnected = false;
        private Socket socket = null;
        private Dispatcher dispatcher = null;
        private Queue<Operation> incomingOperations = null;
        private ObservableCollection<LogMessage> log = null;
        private static Logger logger = LogManager.GetCurrentClassLogger();
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
        public Queue<Operation> SendQueue { get; private set; }
        #endregion

        public LocalClient(Dispatcher dispatcher, Socket socket, ObservableCollection<LogMessage> log, Queue<Operation> input)
        {
            this.log = log;
            this.socket = socket;
            this.dispatcher = dispatcher;
            incomingOperations = input;
            SendQueue = new Queue<Operation>();
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public async void ConnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    isConnected = true;
                    using (Timer queueChecker = new Timer((o) => CheckOutQueue(), new object(), 0, 100))
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
            SendQueue = new Queue<Operation>();
        }

        private void ReceiveMessages()
        {
            while (isConnected)
            {
                if (socket.Poll(100000, SelectMode.SelectRead))
                {
                    String message = "";
                    try
                    {
                        message = ReceiveMessage();
                        if (!string.IsNullOrWhiteSpace(message))
                            AddMessageToQueue(message);
                        else Disconnect();
                    }
                    catch (Exception ex)
                    {
                        isConnected = false;
                        logger.Error($"{ipAddress}(ReceiveMessages) {ex.Message} in {message}");
                        UpdateLog($"(ReceiveMessages) {ex.Message}");
                    }
                }
            }
        }
        private String ReceiveMessage()
        {
            StringBuilder incomingMessage = new StringBuilder();
            try
            {
                Byte[] buffer = new Byte[1500];
                Int32 byteCount;
                do
                {
                    byteCount = socket.Receive(buffer);
                    incomingMessage.Append(Encoding.UTF8.GetString(buffer), 0, byteCount);
                }
                while (socket.Available > 0);

                return incomingMessage.ToString();
            }
            catch(Exception ex)
            {
                logger.Error($"{ipAddress}(ReceiveMessages) {ex.Message}");
                UpdateLog($"(ReceiveMessages) {ex.Message}");
                return null;
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
            while (isConnected && SendQueue.Count > 0)
            {
                try
                {
                    Operation operation = SendQueue.Dequeue();
                    if (operation != null)
                    {
                        String json = JsonSerializer.Serialize<Operation>(operation);
                        Byte[] data = Encoding.UTF8.GetBytes(json);
                        socket.Send(data);
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
