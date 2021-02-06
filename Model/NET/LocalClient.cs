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
        String name;
        String ipAddress;
        Boolean isConnected;
        Socket socket;
        Dispatcher dispatcher;
        ObservableCollection<LogMessage> log;
        Queue<Operation> incomingOperations;
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

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    isConnected = true;
                    UpdateLog("was connected");
                    Task sendMessagesTask = SendMessagesAsync(cancellationToken);
                    RecieveMessages(cancellationToken);
                }
                catch (Exception ex)
                {
                    UpdateLog($"{ipAddress}(Connect) {ex.Message}");
                }
                finally
                {
                    //UpdateLog($"{ipAddress} was disconnected");
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
            });
        }
        internal void Disconnect()
        {
            isConnected = false;
        }
        private void RecieveMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Byte[] buffer = new Byte[256];
                Int32 byteCount = 0;
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
                    else Disconnect();
                }
                catch (Exception ex)
                {
                    UpdateLog($"{ipAddress}(ReceiveMessages) {ex.Message}");
                }
            }
        }
        private async Task SendMessagesAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    while (SendQueue.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            String json = JsonSerializer.Serialize<Operation>(SendQueue.Dequeue());
                            Byte[] data = Encoding.UTF8.GetBytes(json);
                            socket.Send(data);
                        }
                        catch(OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            UpdateLog($"{ipAddress}(SendMessagesAsync) {ex.Message}");
                        }
                    }
                }
            });
        }
        private void UpdateLog(String text)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), text));
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
