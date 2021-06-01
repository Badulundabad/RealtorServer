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
        private CredentialContext credentialContext = new CredentialContext();
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
                        String password = BinarySerializer.Deserialize<String>(operation.Data);
                        Credential credential = FindMatchingCredential(operation.Name, password);

                        if (operation.Parameters.Action == Act.Login && credential != null)
                            Login(operation, credential);
                        else if (operation.Parameters.Action == Act.Logout && CheckAccess(operation.IpAddress, operation.Token))
                            Logout(operation, credential);
                        else if (operation.Parameters.Action == Act.Register && credential == null)
                            Register(operation);
                        else
                            LogInfo($"something went wrong with {operation.Number}");
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
        private void Logout(Operation operation, Credential credential)
        {
            try
            {
                credential.IpAddress = "";
                credential.Token = (new Guid().ToString());
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
                OutcomingOperations.Enqueue(operation);
            }
        }
        public void Logout(String ipAddress)
        {
            Credential credential = credentialContext.Credentials.FirstOrDefault(c => c.IpAddress == ipAddress);
            if (credential != null)
            {
                credential.Token = "";
                credential.IpAddress = "";
            }
        }
        private void Register(Operation operation)
        {
            try
            {
                Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
                if (FindMatchingCredential(credential.Name, credential.Password) == null)
                {
                    credential.RegistrationDate = DateTime.Now;
                    credentialContext.Credentials.Local.Add(credential);
                    credentialContext.SaveChanges();

                    LogInfo($"{operation.Name} has registered");
                    operation.IsSuccessfully = true;
                }
                else throw new InformationalException("such user already exists");
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
            return credentialContext.Credentials.FirstOrDefault(cred =>
                                                                cred.Name == name &&
                                                                cred.Password == password);
        }
        private Boolean CheckForLogin(String ipAddress, String token)
        {
            if (credentialContext.Credentials.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null)
                return true;
            else return false;
        }
        public Boolean CheckAccess(String ipAddress, String token)
        {
            //if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress && cred.Token == token) != null) return true;
            if (credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress) != null)
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
