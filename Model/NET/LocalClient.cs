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
            get=> isConnected;
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

        public async void ConnectAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    isConnected = true;
                    using (Timer queueChecker = new Timer((o) => CheckOutQueue(), new object(), 0, 100)) 
                        RecieveMessages();
                }
                catch (Exception ex)
                {
                    UpdateLog($"(Connect) {ex.Message}");
                }
                finally
                {
                    UpdateLog("has disconnected");
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
            });
        }
        public void Disconnect()
        {
            SendQueue = new Queue<Operation>();
            //Send a disconnect message
            isConnected = false;
        }

        private void RecieveMessages()
        {
            while (isConnected)
            {
                if (socket.Poll(100000, SelectMode.SelectRead))
                {
                    Byte[] buffer = new Byte[1500];
                    Int32 byteCount;
                    StringBuilder incomingMessage = new StringBuilder();

                    try
                    {
                        do
                        {
                            byteCount = socket.Receive(buffer);
                            incomingMessage.Append(Encoding.UTF8.GetString(buffer), 0, byteCount);
                        }
                        while (socket.Available > 0);

                        if (!string.IsNullOrWhiteSpace(incomingMessage.ToString()))
                        {
                            Operation receivedOperation = JsonSerializer.Deserialize<Operation>(incomingMessage.ToString());
                            receivedOperation.IpAddress = IpAddress;
                            incomingOperations.Enqueue(receivedOperation);
                        }
                        UpdateLog("Receive" + incomingMessage.ToString());
                    }
                    catch (Exception ex)
                    {
                        isConnected = false;
                        UpdateLog($"{ipAddress}(ReceiveMessages) {ex.Message}");
                    }
                }
            }
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
                        UpdateLog("Send " + json);
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog($"{ipAddress}(SendMessagesAsync) {ex.Message}");
                }
            }
        }

        //Maybe this need to delete
        private void Send(Operation operation)
        {
            try
            {
                String json = JsonSerializer.Serialize<Operation>(operation);
                Byte[] data = Encoding.UTF8.GetBytes(json);
                socket.Send(data);
            }
            catch (Exception ex)
            {
                isConnected = false;
                UpdateLog($"{ipAddress}(SendMessagesAsync) {ex.Message}");
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
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
