using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using RealtyModel.Model.Operations;
using Action = RealtyModel.Model.Operations.Action;
using System.Runtime.CompilerServices;
using RealtyModel.Model.Tools;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using System.Linq;

namespace RealtorServer.Model.NET
{
    public class Server
    {
        private TcpListener tcpListener;
        private NetworkStream network;
        private IPAddress currentIp;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Server()
        {
            currentIp = GetLocalIPAddress();
            tcpListener = new TcpListener(currentIp, 15000);
        }
        public static IPAddress GetLocalIPAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip;
                }
            }
            throw new ArgumentException("No network adapters with an IPv4 address in the system");
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
                        LogError(ex.InnerException.Message);
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
            Debug.WriteLine($"{DateTime.Now} INFO    The server started listening on {currentIp}\n");
            Console.WriteLine($"{DateTime.Now} INFO    The server started listening on {currentIp}\n");
            logger.Info("");
            logger.Info("");
            logger.Info($"    The server started listening on {currentIp}\n\n");
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
