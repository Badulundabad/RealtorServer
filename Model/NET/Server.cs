using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Threading;
using NLog;
using RealtyModel.Service;
using RealtyModel.Model.Operations;
using Action = RealtyModel.Model.Operations.Action;

namespace RealtorServer.Model.NET
{
    public class Server
    {
        private TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 15000);
        private NetworkStream network;  
        private Dispatcher dispatcher;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public Dispatcher Dispatcher {
            get => dispatcher;
            protected set {
                dispatcher = value;
            }
        }
        public Server(Dispatcher dispatcher) {
            this.dispatcher = dispatcher;
        }

        private OperationHandling ChooseHandler(Operation operation) {
            if (operation.Action == Action.Login || operation.Action == Action.Register) {
                return new Identification(operation);
            } else if (operation.Action == Action.Request){
                return new Requesting(operation);
            } else if (operation.Action == Action.Add) {
                return new Adding(operation);
            } else if (operation.Action == Action.Update) {
                return new Updating(operation);
            } 
            else {
                throw new NotImplementedException();
            }
        }
        public async void RunAsync() {
            await Task.Run(() => {
                tcpListener.Start();
                while (true) {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    network = client.GetStream();
                    Operation operation = Transfer.ReceiveOperation(network);
                    
                    Response response = ChooseHandler(operation).Handle();
                    Transfer.SendResponse(response, network);
                }
            });
        }
        protected void LogInfo(String text) {
            Debug.WriteLine($"{DateTime.Now} {this.GetType().Name}   {text}");
            logger.Info($"{this.GetType().Name} {text}");
        }
        protected void LogError(String text) {
            Debug.WriteLine($"\n{DateTime.Now} ERROR {this.GetType().Name}     {text}\n");
            logger.Error($"{this.GetType().Name} {text}");
        }
    }
}
