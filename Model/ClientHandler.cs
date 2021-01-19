using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class ClientHandler : INotifyPropertyChanged
    {
        #region Fields
        String name;
        String ipAddress;
        Boolean isConnected;
        Socket workSocket;
        Server server;
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
                OnPropertyChanged();
            }
        }
        #endregion

        public ClientHandler(Socket socket, Server server)
        {
            this.workSocket = socket;
            this.server = server;
            Name = "unknown";
            IpAddress = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
        }

        public void Connect()
        {
            try
            {
                IsConnected = true;
                server.UpdateLog(IpAddress, "has connected");
                SendMessagesAsync();
                RecieveMessages();
            }
            catch (Exception ex)
            {
                server.UpdateLog(IpAddress, $"(Connect)threw an exception: {ex.Message}");
            }
            finally
            {
                Disconnect();
                server.UpdateLog(IpAddress, "has disconnected");
                workSocket.Shutdown(SocketShutdown.Both);
                workSocket.Close();
            }
        }
        internal void Disconnect()
        {
            IsConnected = false;
        }
        private void RecieveMessages()
        {
            while (IsConnected)
            {
                Byte[] buffer = new Byte[256];
                Int32 byteCount = 0;
                StringBuilder incomingMessage = new StringBuilder();

                try
                {
                    do
                    {
                        byteCount = workSocket.Receive(buffer);
                        incomingMessage.Append(Encoding.UTF8.GetString(buffer), 0, byteCount);
                    }
                    while (workSocket.Available > 0);
                    if (!String.IsNullOrWhiteSpace(incomingMessage.ToString()))
                        server.UpdateOperationQueue(IpAddress, incomingMessage.ToString());
                    else Disconnect();
                }
                catch (Exception ex)
                {
                    Disconnect();
                    server.UpdateLog(IpAddress, "(ReceiveMessages)threw an exception: " + ex.Message);
                }
            }
        }
        internal async void SendMessagesAsync()
        {
            await Task.Run(() =>
            {
                while (IsConnected)
                {
                    if (server.OperationResults.Count > 0)
                    {
                        List<Operation> results = server.OperationResults.Where<Operation>
                        (
                            op => op.IpAddress == this.IpAddress || op.IpAddress == "broadcast"
                        ).ToList();
                     
                        if (results != null)
                        {
                            for(Int32 i = 0; i < results.Count; i++)
                            {
                                try
                                {
                                    String json = JsonSerializer.Serialize<Operation>(results[i]);
                                    Byte[] data = Encoding.UTF8.GetBytes(json);
                                    workSocket.Send(data);
                                    server.OperationResults.Remove(results[i]);
                                    server.UpdateLog("Server", $"sent a message to {IpAddress}");
                                }
                                catch(Exception ex)
                                {

                                }
                            }
                        }
                    }
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
    }
}
