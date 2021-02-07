using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public class LocalServer : Server
    {
        private Socket listeningSocket;

        public ObservableCollection<Task> OnlineClients { get; private set; }

        public LocalServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
            OnlineClients = new ObservableCollection<Task>();
        }

        public override async void RunAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                IsRunning = true;
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                try
                {
                    IPEndPoint serverAddress = new IPEndPoint(IPAddress.Any, 8005);
                    using (listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        listeningSocket.Bind(serverAddress);
                        listeningSocket.Listen(10);
                        UpdateLog(" has ran");

                        Socket clientSocket;
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (listeningSocket.Poll(100000, SelectMode.SelectRead))
                            {
                                clientSocket = listeningSocket.Accept();
                                LocalClient client = new LocalClient(dispatcher, clientSocket, log, IncomingQueue);
                                client.ConnectAsync(cancellationTokenSource, cancellationTokenSource.Token);
                                //dispatcher.BeginInvoke(new Action(() => OnlineClients.Add(connectClientTask)));
                                UpdateLog("New client has connected");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    cancellationTokenSource.Cancel();
                    while (OnlineClients.Count > 0)
                    {
                        Int32 count = OnlineClients.Count;
                        for(Int32 i = 0; i < count; i++)
                        {
                            //Quite possibly if that part didn't work
                            if(OnlineClients[i].Status == TaskStatus.Faulted || OnlineClients[i].Status == TaskStatus.Canceled)
                            {
                                OnlineClients.Remove(OnlineClients[i]);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog("(RunAsync) " + ex.Message);
                }
                finally
                {
                    UpdateLog(" was stopped");
                    IsRunning = false;
                }
            });
        }
    }
}
