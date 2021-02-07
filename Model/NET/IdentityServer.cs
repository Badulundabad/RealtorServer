using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();
        private Dictionary<String, String> signedClients = new Dictionary<String, String>();

        public IdentityServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output) : base(dispatcher, log, output)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            outcomingQueue = output;
            IncomingQueue = new Queue<Operation>();
        }

        public override async void RunAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    UpdateLog(" has ran");
                    while (true)
                    {
                        while (IncomingQueue.Count > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Handle();
                        }
                    }
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    UpdateLog("(RunAsync) " + ex.Message);
                }
                finally
                {
                    UpdateLog(" was stopped");
                }
            });
        }
        public Boolean CheckAccess(String ipAddress, String token)
        {
            if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null)
                return true;
            else return false;
        }
        private void Handle()
        {
            Operation operation = new Operation();
            try
            {
                operation = IncomingQueue.Dequeue();
                Credential credential = FindMatchingCredential(operation.Name, operation.Data);

                if (operation.OperationParameters.Type == OperationType.Login && credential != null)
                {
                    if (credential.IpAddress != operation.IpAddress || credential.IpAddress != "")
                        LogoutPrevious(credential);
                    operation = Login(operation, credential);

                    UpdateLog($"{operation.IpAddress} has logged in as {credential.Name}");
                }
                else if (operation.OperationParameters.Type == OperationType.Logout && CheckAccess(operation.IpAddress, operation.Token))
                {
                    operation = Logout(operation, credential);
                    UpdateLog($"{credential.Name} has logged out");
                }
                else if (operation.OperationParameters.Type == OperationType.Register && credential == null)
                {
                    if (!String.IsNullOrWhiteSpace(operation.Name) && !String.IsNullOrWhiteSpace(operation.Data))
                    {
                        operation = Register(operation);
                        UpdateLog($"Alibaba has registered");
                    }
                    else operation.IsSuccessfully = false;
                }
                else if (operation.OperationParameters.Type == OperationType.ToFire && credential != null)
                {
                    //Need a special action to fire a worker 
                    if (CheckAccess(operation.IpAddress, operation.Token))
                    {
                        operation = ToFire(operation, credential);
                        UpdateLog($"{credential.Name} has fired");
                    }
                    else operation.IsSuccessfully = false;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                UpdateLog("(Handle) " + ex.Message);
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
            //Try without that - credentialContext.Credentials.Local.Add(credential);
            credentialContext.SaveChanges();
            operation.Data = credential.Token;
            operation.IsSuccessfully = true;
            return operation;
        }
        private Operation Logout(Operation operation, Credential credential)
        {
            credential.IpAddress = "";
            credential.Token = "";
            //Try without that - credentialContext.Credentials.Local.Add(credential);
            credentialContext.SaveChanges();
            operation.IsSuccessfully = true;
            return operation;
        }
        private void LogoutPrevious(Credential credential)
        {
            credential.IpAddress = "";
            credential.Token = "";
            //Try without that - credentialContext.Credentials.Local.Add(credential);
            credentialContext.SaveChanges();
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
        }
        private Operation Register(Operation operation)
        {
            Agent agent = new Agent();
            Credential credential = new Credential();
            credential.Agent = agent;
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
