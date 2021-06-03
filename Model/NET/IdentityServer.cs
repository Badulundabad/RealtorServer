using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Exceptions;
using RealtyModel.Model;
using RealtyModel.Model.Operations;
using RealtorServer.Model.DataBase;
using RealtyModel.Service;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private List<Credential> LoggedInCredentials = new List<Credential>();
        public IdentityServer(Dispatcher dispatcher, Queue<Operation> queue) : base(dispatcher)
        {
            Dispatcher = dispatcher;
            OutcomingOperations = queue;
        }

        public async void HandleAsync(Operation operation)
        {
            await Task.Run(() =>
            {
                lock (handleLocker)
                {
                    try
                    {
                        if (operation.Parameters.Action == Act.Logout)
                            Logout(operation);
                        else
                        {
                            String password = BinarySerializer.Deserialize<String>(operation.Data);
                            Credential credential = FindMatchingCredential(operation.Name, password);

                            if (operation.Parameters.Action == Act.Login && credential != null)
                                Login(operation, credential);
                            else if (operation.Parameters.Action == Act.Register && credential == null)
                                Register(operation);
                            else
                                LogInfo($"something went wrong with {operation.Number}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"(Handle) {ex.Message}");
                    }
                }
            });
        }

        private void Login(Operation operation, Credential credential)
        {
            try
            {
                credential.IpAddress = operation.IpAddress;
                credential.Token = GetToken();
                LoggedInCredentials.Add(credential);

                LogInfo($"{operation.IpAddress} has logged in as {credential.Name}");

                operation.Data = BinarySerializer.Serialize<String>(credential.Token);
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
                OutcomingOperations.Enqueue(operation);
            }
        }
        private void Logout(Operation operation)
        {
            try
            {
                Credential credential = LoggedInCredentials.FirstOrDefault(c => c.IpAddress == operation.IpAddress && c.Name == operation.Name);
                if (credential != null)
                {
                    LoggedInCredentials.Remove(credential);
                    LogInfo($"{credential.Name} has logged out");
                    operation.IsSuccessfully = true;
                }
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                LogError($"(Logout) {ex.Message}");
            }
            finally
            {
                OutcomingOperations.Enqueue(operation);
            }
        }
        public void Logout(String ipAddress)
        {
            Credential credential = LoggedInCredentials.FirstOrDefault(c => c.IpAddress == ipAddress);
            if (credential != null)
            {
                LoggedInCredentials.Remove(credential);
                LogInfo($"{credential.Name} has logged out");
            }
        }
        private void Register(Operation operation)
        {
            try
            {
                using (CredentialContext context = new CredentialContext())
                {
                    Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
                    if (FindMatchingCredential(credential.Name, credential.Password) == null)
                    {
                        credential.RegistrationDate = DateTime.Now;
                        context.Credentials.Local.Add(credential);
                        context.SaveChanges();

                        LogInfo($"{operation.Name} has registered");
                        operation.IsSuccessfully = true;
                    }
                    else throw new InformationalException("such user already exists");
                }
            }
            catch (InformationalException ex)
            {
                operation.IsSuccessfully = false;
                LogInfo($"(Register) {ex.Message}");
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                LogError($"(Register) {ex.Message}");
            }
            finally
            {
                operation.Data = null;
                OutcomingOperations.Enqueue(operation);
            }
        }

        private void LogoutPrevious(Credential credential)
        {
            //OperationHandled?.Invoke(this, new Event.OperationHandledEventArgs(new Operation()
            //{
            //    IpAddress = credential.IpAddress.ToString(),
            //    Parameters = new Parameters() { Direction = Direction.Identity, Action = Act.Logout },
            //    Name = credential.Name,
            //    Token = credential.Token,
            //    IsSuccessfully = true
            //}));
            //credential.IpAddress = null;
            //credential.Token = (new Guid()).ToString();
        }

        private Credential FindMatchingCredential(String name, String password)
        {
            using (CredentialContext context = new CredentialContext())
                return context.Credentials.FirstOrDefault(cred =>
                                                                 cred.Name == name &&
                                                                 cred.Password == password);
        }
        private Boolean CheckForLogin(String ipAddress, String token)
        {
            using (CredentialContext context = new CredentialContext())
            {
                if (context.Credentials.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null)
                    return true;
                else return false;
            }
        }
        public Boolean CheckAccess(String ipAddress, String token)
        {
            //if (context.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null) return true;
            if (LoggedInCredentials.FirstOrDefault(cred => cred.IpAddress == ipAddress) != null)
                return true;
            else return false;
        }
        private String GetToken()
        {
            return (new Guid()).ToString();
            //return new Guid().ToString();
        }
    }
}
