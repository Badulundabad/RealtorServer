using System;
using System.Linq;
using System.Windows.Threading;
using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Operations;

namespace RealtorServer.Model.NET
{
    public class IdentityServer : Server
    {
        private CredentialContext credentialContext = new CredentialContext();

        public IdentityServer(Dispatcher dispatcher) : base(dispatcher)
        {
            Dispatcher = dispatcher;
        }
        private void Register(Operation operation)
        {
            try
            {
                String password = BinarySerializer.Deserialize<String>(operation.Data);
                if (!String.IsNullOrWhiteSpace(password))
                {
                    Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
                    credential.RegistrationDate = DateTime.Now;
                    credentialContext.Credentials.Local.Add(credential);
                    credentialContext.SaveChanges();

                    operation.IsSuccessfully = true;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                LogError($"(Register) {ex.Message}");
            }
            finally
            {
                operation.Data = null;
            }
        }

        public Boolean CheckAccess(String ipAddress, String token)
        {
            return credentialContext.Credentials.Local.FirstOrDefault(cred => cred.IpAddress == ipAddress) != null;
        }
    }
}
