using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using RealtyModel.Service;
using RealtyModel.Model.Operations;
using Action = RealtyModel.Model.Operations.Action;

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
                tcpListener = new TcpListener(IPAddress.Parse("192.168.1.34"), 15000);
            else tcpListener = new TcpListener(IPAddress.Parse("192.168.1.250"), 15000);
        }

        public async void RunAsync()
        {
            await Task.Run(() =>
            {
                tcpListener.Start();
                LogInfo("Server has started to listen");
                while (true)
                {
                    try
                    {
                        using (TcpClient client = tcpListener.AcceptTcpClient())
                        {
                            LogInfo($"{((IPEndPoint)client.Client.RemoteEndPoint).Address} has connected");
                            network = client.GetStream();

                            Operation operation = Transfer.ReceiveOperation(network);
                            Response response = ChooseHandler(operation).Handle();
                            Transfer.SendResponse(response, network);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"(RunAsync) {ex.Message}");
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

        private void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} INFO    {text}");
            Console.WriteLine($"{DateTime.Now} INFO    {text}");
            logger.Info($"    {text}");
        }
        private void LogError(String text)
        {
            Debug.WriteLine($"\n{DateTime.Now} ERROR    {text}\n");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now} ERROR    {text}");
            Console.ForegroundColor = ConsoleColor.White;
            logger.Error($"    {text}");
        }
    }
}
