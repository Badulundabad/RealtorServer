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
using static RealtorServer.Model.Event.EventHandlers;
using System.Diagnostics;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public event OperationHandledEventHandler OperationHandled;
        public IdentityServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log) : base(dispatcher, log)
        {
            Log = log;
            Dispatcher = dispatcher;
            OutcomingOperations = new OperationQueue();
            IncomingOperations = new OperationQueue();
            IncomingOperations.Enqueued += (s, e) => Handle();
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
            lock (handleLocker)
            {
                while (IncomingOperations.Count > 0)
                {
                    Operation operation = IncomingOperations.Dequeue();
                    Credential credential = FindMatchingCredential(operation.Name, operation.Data);

                    if (operation.OperationParameters.Type == OperationType.Login && credential != null)
                        Login(operation, credential);
                    else if (operation.OperationParameters.Type == OperationType.Logout && CheckAccess(operation.IpAddress, operation.Token))
                        Logout(operation, credential);
                    else if (operation.OperationParameters.Type == OperationType.Register && credential == null)
                        Register(operation);
                }
            }
        }

        private void Login(Operation operation, Credential credential)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(credential.IpAddress) && credential.IpAddress != operation.IpAddress)
                    LogoutPrevious(credential);
                credential.IpAddress = operation.IpAddress;
                credential.Token = GetToken();
                Debug.WriteLine($"{operation.IpAddress} has logged in as {credential.Name}");
                logger.Info($"{operation.IpAddress} has logged in as {credential.Name}");
                //UpdateLog($"{operation.IpAddress} has logged in as {credential.Name}");

                operation.Data = credential.Token;
                operation.IsSuccessfully = true;
            }
            catch (Exception ex)
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
                Debug.WriteLine($"Identity server(Login) {ex.Message}");
                logger.Error($"Identity server(Login) {ex.Message}");
                //UpdateLog($"(Login) {ex.Message}");
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
                //OutcomingOperations.Enqueue(operation);
            }
        }
        private void Logout(Operation operation, Credential credential)
        {
            try
            {
                credential.IpAddress = "";
                credential.Token = "";
                Debug.WriteLine($"{credential.Name} has logged out");
                logger.Info($"{credential.Name} has logged out");
                //UpdateLog($"{credential.Name} has logged out");
                operation.IsSuccessfully = true;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                Debug.WriteLine($"Identity server(Logout) {ex.Message}");
                logger.Error($"Identity server(Logout) {ex.Message}");
                //UpdateLog($"(Logout) {ex.Message}");
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
                //OutcomingOperations.Enqueue(operation);
            }
        }
        private void LogoutPrevious(Credential credential)
        {
            OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(new Operation()
            {
                IpAddress = credential.IpAddress,
                OperationParameters = new OperationParameters() { Direction = OperationDirection.Identity, Type = OperationType.Logout },
                Name = credential.Name,
                Token = credential.Token,
                IsSuccessfully = true
            }));
            //OutcomingOperations.Enqueue();
            credential.IpAddress = "";
            credential.Token = "";
        }
        private void Register(Operation operation)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(operation.Name) && !String.IsNullOrWhiteSpace(operation.Data))
                {
                    Credential credential = JsonSerializer.Deserialize<Credential>(operation.Data);
                    credential.RegistrationDate = DateTime.Now;
                    credentialContext.Credentials.Local.Add(credential);
                    credentialContext.SaveChanges();
                    Debug.WriteLine($"{operation.Name} has registered");
                    logger.Info($"{operation.Name} has registered");
                    //UpdateLog($"{operation.Name} has registered");
                    operation.IsSuccessfully = true;
                }
                else
                    operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                Debug.WriteLine($"Identity server(Register) {ex.Message}");
                logger.Error($"Identity server(Register) {ex.Message}");
                //UpdateLog($"(Register) {ex.Message}");
            }
            finally
            {
                operation.Data = "";
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
                //OutcomingOperations.Enqueue(operation);
            }
        }

        private void SendAgentList(Operation operation)
        {
            String[] agents = (from cred in credentialContext.Credentials.Local select new String(cred.Name.ToCharArray())).ToArray<String>();
            operation.Data = JsonSerializer.Serialize(agents);
            operation.IsSuccessfully = true;
            OutcomingOperations.Enqueue(operation);
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
