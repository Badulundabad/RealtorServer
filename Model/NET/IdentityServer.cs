using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();
        private System.Timers.Timer queueChecker = null;

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
            UpdateLog("has ran");
        }
        public override void Stop()
        {
            queueChecker.Stop();
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
                //Ищем совпадающий credential
                Credential credential = FindMatchingCredential(operation.Name, operation.Data);

                if (operation.OperationParameters.Type == OperationType.Login && credential != null)
                {
                    if (!String.IsNullOrWhiteSpace(credential.IpAddress) && credential.IpAddress != operation.IpAddress)
                        LogoutPrevious(credential);
                    operation = this.Login(operation, credential);

                    UpdateLog($"{operation.IpAddress} has logged in as {credential.Name}");
                }
                else if(operation.OperationParameters.Type == OperationType.Update)
                {
                    //Send a list of agents
                    String[] agents = (from cred in credentialContext.Credentials.Local select new String(cred.Name.ToCharArray())).ToArray<String>();
                    operation.Data = JsonSerializer.Serialize(agents);
                    operation.IsSuccessfully = true;
                    outcomingQueue.Enqueue(operation);
                }
                else if (operation.OperationParameters.Type == OperationType.Logout && CheckAccess(operation.IpAddress, operation.Token))
                {
                    operation = this.Logout(operation, credential);
                    UpdateLog($"{credential.Name} has logged out");
                }
                else if (operation.OperationParameters.Type == OperationType.Register && credential == null)
                {
                    if (!String.IsNullOrWhiteSpace(operation.Name) && !String.IsNullOrWhiteSpace(operation.Data))
                    {
                        operation = this.Register(operation);
                        UpdateLog($"{operation.Name} has registered");
                    }
                    else operation.IsSuccessfully = false;
                }
                else if (operation.OperationParameters.Type == OperationType.ToFire && credential != null)
                {
                    //Need a special action to fire a worker 
                    if (CheckAccess(operation.IpAddress, operation.Token))
                    {
                        operation = this.ToFire(operation, credential);
                        UpdateLog($"{credential.Name} has fired");
                    }
                    else operation.IsSuccessfully = false;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                UpdateLog($"(Handle) {ex.Message}");
            }
            finally
            {
                outcomingQueue.Enqueue(operation);
                UpdateLog("added a mes to the outQueue");
            }
        }

        private Operation Login(Operation operation, Credential credential)
        {
            credential.IpAddress = operation.IpAddress;
            credential.Token = GetToken();
            operation.Data = credential.Token;
            operation.IsSuccessfully = true;
            return operation;
        }
        private Operation Logout(Operation operation, Credential credential)
        {
            credential.IpAddress = "";
            credential.Token = "";
            operation.IsSuccessfully = true;
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
            Credential credential = new Credential();
            credential.Name = operation.Name;
            credential.Password = operation.Data;
            credential.RegistrationDate = DateTime.Now;
            credentialContext.Credentials.Local.Add(credential);
            credentialContext.SaveChanges();

            operation.Data = "";
            operation.IsSuccessfully = true;
            return operation;
        }
        private Operation ToFire(Operation operation, Credential credential)
        {
            credentialContext.Credentials.Remove(credential);
            credentialContext.SaveChanges();
            operation.IsSuccessfully = true;
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
