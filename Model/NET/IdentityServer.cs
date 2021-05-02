using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
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
                //if (!String.IsNullOrWhiteSpace(credential.IpAddress) && credential.IpAddress != operation.IpAddress)
                //    LogoutPrevious(credential);
                credential.IpAddress = operation.IpAddress;
                credential.Token = GetToken();

                LogInfo($"{operation.IpAddress} has logged in as {credential.Name}");

                operation.Data = credential.Token;
                operation.IsSuccessfully = true;
            }
            catch (Exception ex)
            {
                operation.Data = "";
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
                credential.IpAddress = "";
                credential.Token = "";
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
                operation.Data = "";
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
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



        private void SendAgentList(Operation operation)
        {
            String[] agents = (from cred in credentialContext.Credentials.Local select new String(cred.Name.ToCharArray())).ToArray<String>();
            operation.Data = JsonSerializer.Serialize(agents);
            operation.IsSuccessfully = true;
            OutcomingOperations.Enqueue(operation);
        }
        private void ToFire(Operation operation, Credential credential)
        {
            try
            {
                credentialContext.Credentials.Remove(credential);
                credentialContext.SaveChanges();
                operation.IsSuccessfully = true;
                LogInfo($"{operation.Name} has fired");
            }
            catch (Exception ex)
            {
                operation.Data = "";
                operation.IsSuccessfully = false;
                LogError($"(ToFire) {ex.Message}");
            }
            finally
            {
                OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(operation));
            }
        }
    }
}
