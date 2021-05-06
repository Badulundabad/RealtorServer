using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Service;
using static RealtorServer.Model.Event.EventHandlers;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();
        public event OperationHandledEventHandler OperationHandled;

        public IdentityServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Dispatcher = dispatcher;
            OutcomingOperations = new OperationQueue();
            IncomingOperations = new OperationQueue();
            IncomingOperations.Enqueued += (s, e) => Handle();
        }
        private void Handle()
        {
            lock (handleLocker)
            {
                if (IncomingOperations != null && IncomingOperations.Count > 0)
                {
                    Operation operation = IncomingOperations.Dequeue();
                    Credential credential = FindMatchingCredential(operation.Name, JsonSerializer.Deserialize<String>(operation.Data));

                    if (operation.Parameters.Type == OperationType.Login && credential != null)
                        Login(operation, credential);
                    else if (operation.Parameters.Type == OperationType.Logout && CheckAccess(operation.IpAddress, operation.Token))
                        Logout(operation, credential);
                    else if (operation.Parameters.Type == OperationType.Register && credential == null)
                        Register(operation);
                    else
                        LogInfo($"something went wrong with {operation.Number}");
                }
            }
        }

        private void Login(Operation operation, Credential credential)
        {
            try
            {
                //if (!String.IsNullOrWhiteSpace(credential.IpAddress) && credential.IpAddress != operation.IpAddress)
                //    LogoutPrevious(credential);
                credential.IpAddress = operation.IpAddress;
                credential.Token = GetToken();

                LogInfo($"{operation.IpAddress} has logged in as {credential.Name}");

                operation.Data = Encoding.UTF8.GetBytes(credential.Token.ToString());
                operation.IsSuccessfully = true;
            }
            catch (Exception ex)
            {
                operation.Data = null;
                operation.IsSuccessfully = false;
                LogError($"(Login) {ex.Message}");
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void Logout(Operation operation, Credential credential)
        {
            try
            {
                credential.IpAddress = null;
                credential.Token = Guid.Empty;
                LogInfo($"{credential.Name} has logged out");
                operation.IsSuccessfully = true;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                LogError($"(Logout) {ex.Message}");
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
        private void Register(Operation operation)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(operation.Name) && !String.IsNullOrWhiteSpace(Encoding.UTF8.GetString(operation.Data)))
                {
                    Credential credential = JsonSerializer.Deserialize<Credential>(operation.Data);
                    credential.RegistrationDate = DateTime.Now;
                    credentialContext.Credentials.Local.Add(credential);
                    credentialContext.SaveChanges();

                    LogInfo($"{operation.Name} has registered");

                    operation.IsSuccessfully = true;
                }
                else
                    operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                LogError($"(Register) {ex.Message}");
            }
            finally
            {
                operation.Data = null;
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }

        private void LogoutPrevious(Credential credential)
        {
            OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(new Operation()
            {
                IpAddress = credential.IpAddress.ToString(),
                Parameters = new OperationParameters() { Direction = OperationDirection.Identity, Type = OperationType.Logout },
                Name = credential.Name,
                Token = credential.Token,
                IsSuccessfully = true
            }));
            credential.IpAddress = null;
            credential.Token = Guid.Empty;
        }

        private Credential FindMatchingCredential(String name, String password)
        {
            return credentialContext.Credentials.FirstOrDefault(cred =>
                                                                cred.Name == name &&
                                                                cred.Password == password);
        }
        private Boolean CheckForLogin(String ipAddress, Guid token)
        {
            if (credentialContext.Credentials.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null)
                return true;
            else return false;
        }
        public Boolean CheckAccess(String ipAddress, Guid token)
        {
            //if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null) return true;
            if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress) != null)
                return true;
            else return false;
        }
        private Guid GetToken()
        {
            return new Guid();
            //return new Guid().ToString();
        }
    }
}
