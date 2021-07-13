using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using RealtyModel.Service;
using RealtyModel.Model.Operations;
using Action = RealtyModel.Model.Operations.Action;
using System.Runtime.CompilerServices;

namespace RealtorServer.Model.NET
{
    public class Server
    {
        private TcpListener tcpListener;
        private NetworkStream network;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Server()
        {
            if (Debugger.IsAttached)
                tcpListener = new TcpListener(IPAddress.Parse("192.168.8.102"), 15000);
            else tcpListener = new TcpListener(IPAddress.Parse("192.168.1.250"), 15000);
        }

        public async void RunAsync()
        {
            await Task.Run(() =>
            {
                tcpListener.Start();
                LogServerStart();
                while (true)
                {
                    try
                    {
                        using (TcpClient client = tcpListener.AcceptTcpClient())
                        {
                            string ipAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                            LogNewSession(ipAddress);
                            network = client.GetStream();

                            Operation operation = Transfer.ReceiveOperation(network);
                            operation.Ip = ipAddress;
                            Response response = ChooseHandler(operation).Handle();
                            Transfer.SendResponse(response, network);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex.Message);
                    }
                }
            });
        }
        private OperationHandling ChooseHandler(Operation operation)
        {
            if (operation.Action == Action.Login || operation.Action == Action.Register)
            {
                return new Identification(operation);
            }
            else if (operation.Action == Action.Request)
            {
                return new Requesting(operation);
            }
            else if (operation.Action == Action.Add)
            {
                return new Adding(operation);
            }
            else if (operation.Action == Action.Update)
            {
                return new Updating(operation);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private void LogServerStart() {
            Debug.WriteLine($"{DateTime.Now} INFO    The server started listening\n");
            Console.WriteLine($"{DateTime.Now} INFO    The server started listening\n");
            logger.Info("");
            logger.Info("");
            logger.Info($"    The server started listening\n\n");
        }
        private void LogNewSession(string text) {
            Debug.WriteLine($"\n{DateTime.Now} INFO    {text} initiated a new session");
            Console.WriteLine($"\n{DateTime.Now} INFO    {text} initiated a new session");
            logger.Info("");
            logger.Info($"    {text} initiated a new session");
        }
        private void LogError(String text) {
            Debug.WriteLine($"{DateTime.Now} ERROR    {text}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now} ERROR    {text}");
            Console.ResetColor();
            logger.Error($"    {text}");
        }
    }
}
