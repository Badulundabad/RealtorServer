using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using NLog;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();
        private System.Timers.Timer queueChecker = null;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public IdentityServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
        }

        public void Run()
        {
            queueChecker = new System.Timers.Timer();
            queueChecker.Interval = 100;
            queueChecker.AutoReset = true;
            queueChecker.Elapsed += (o, e) =>
            {
                //UpdateLog("is checking a queue");
                while (IncomingQueue.Count > 0)
                    Handle();
            };
            queueChecker.Start();
            logger.Info("Identity server has ran");
            UpdateLog("has ran");
        }
        public override void Stop()
        {
            queueChecker.Stop();
            logger.Info("Identity server has stopped");
            UpdateLog("has stopped");
        }
        public Boolean CheckAccess(String ipAddress, String token)
        {
            //if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null) return true;
            if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress) != null)
                return true;
            else return false;
        }
        private void Handle()
        {
            Operation operation = null;
            try
            {
                operation = IncomingQueue.Dequeue();
                Credential credential = FindMatchingCredential(operation.Name, operation.Data);

                if (operation.OperationParameters.Type == OperationType.Login && credential != null)
                {
                    if (!String.IsNullOrWhiteSpace(credential.IpAddress) && credential.IpAddress != operation.IpAddress)
                        LogoutPrevious(credential);
                    operation = Login(operation, credential);
                }
                else if (operation.OperationParameters.Type == OperationType.Update)
                {
                    //Send a list of agents
                    String[] agents = (from cred in credentialContext.Credentials.Local select new String(cred.Name.ToCharArray())).ToArray<String>();
                    operation.Data = JsonSerializer.Serialize(agents);
                    operation.IsSuccessfully = true;
                    outcomingQueue.Enqueue(operation);
                }
                else if (operation.OperationParameters.Type == OperationType.Logout && CheckAccess(operation.IpAddress, operation.Token))
                    operation = Logout(operation, credential);
                else if (operation.OperationParameters.Type == OperationType.Register && credential == null)
                {
                    if (!String.IsNullOrWhiteSpace(operation.Name) && !String.IsNullOrWhiteSpace(operation.Data))
                        operation = Register(operation);
                    else operation.IsSuccessfully = false;
                }
                else if (operation.OperationParameters.Type == OperationType.ToFire && credential != null)
                {
                    if (CheckAccess(operation.IpAddress, operation.Token))
                        operation = ToFire(operation, credential);
                    else operation.IsSuccessfully = false;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                logger.Error($"Identity server(Handle) {ex.Message}");
                UpdateLog($"(Handle) {ex.Message}");
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
            }
        }

        private Operation Login(Operation operation, Credential credential)
        {
            credential.IpAddress = operation.IpAddress;
            credential.Token = GetToken();
            operation.Data = credential.Token;
            operation.IsSuccessfully = true;
            logger.Info($"{operation.IpAddress} has logged in as {credential.Name}");
            UpdateLog($"{operation.IpAddress} has logged in as {credential.Name}");
            return operation;
        }
        private Operation Logout(Operation operation, Credential credential)
        {
            credential.IpAddress = "";
            credential.Token = "";
            operation.IsSuccessfully = true;
            logger.Info($"{credential.Name} has logged out");
            UpdateLog($"{credential.Name} has logged out");
            return operation;
        }
        private void LogoutPrevious(Credential credential)
        {
            outcomingQueue.Enqueue(new Operation()
            {
                IpAddress = credential.IpAddress,
                OperationParameters = new OperationParameters()
                {
                    Direction = OperationDirection.Identity,
                    Type = OperationType.Logout
                },
                Name = credential.Name,
                Token = credential.Token,
                IsSuccessfully = true
            });
            credential.IpAddress = "";
            credential.Token = "";
        }
        private Operation Register(Operation operation)
        {
            try
            {
                Credential credential = JsonSerializer.Deserialize<Credential>(operation.Data);
                credential.RegistrationDate = DateTime.Now;
                credentialContext.Credentials.Local.Add(credential);
                credentialContext.SaveChanges();

                operation.Data = "";
                operation.IsSuccessfully = true;
                logger.Info($"{operation.Name} has registered");
                UpdateLog($"{operation.Name} has registered");
                return operation;
            }
            catch (Exception ex)
            {
                logger.Error($"Identity server(Register) {ex.Message}");
                UpdateLog($"(Register) {ex.Message}");
                operation.IsSuccessfully = false;
                return operation;
            }
        }
        private Operation ToFire(Operation operation, Credential credential)
        {
            credentialContext.Credentials.Remove(credential);
            credentialContext.SaveChanges();
            operation.IsSuccessfully = true;
            logger.Info($"{operation.Name} has fired");
            UpdateLog($"{operation.Name} has fired");
            return operation;
        }
        private Credential FindMatchingCredential(String name, String password)
        {
            Credential match = credentialContext.Credentials.FirstOrDefault(cred =>
            cred.Name == name &&
            cred.Password == password);
            return match;
        }
        private Boolean CheckForLogin(String ipAddress, String token)
        {
            if (credentialContext.Credentials.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null)
                return true;
            else return false;
        }
        private String GetToken()
        {
            return "token";
            //return new Guid().ToString();
        }
    }
}
