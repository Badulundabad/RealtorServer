using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = RealtyModel.Model.Operations.Action;

namespace RealtorServer.Model.NET
{
    class Identification : OperationHandling
    {
        public Identification() {
        }
        public Identification(Operation operation) {
            this.operation = operation;
        }
        private Response VerifyCredentials() {
            SymmetricEncryption encrypted = BinarySerializer.Deserialize<SymmetricEncryption>(operation.Data);
            LogInfo("Received encrypted credentials");
            Credential credential = encrypted.Decrypt<Credential>();
            LogInfo("Decrypted credentials");
            bool hasMatchingCredentials = new CredentialContext().Credentials.FirstOrDefault(x => x.Name == credential.Name && x.Password == credential.Password) != null;
            if (hasMatchingCredentials) {
                LogInfo($"{operation.Ip} has logged in as {credential.Name}");
                return new Response(BinarySerializer.Serialize(true));
            } else {
                LogInfo($"{operation.Ip} could not log in");
                return new Response(BinarySerializer.Serialize(false), ErrorCode.Credential);
            }
        }
        public override Response Handle() {
            if (operation.Action == Action.Login)
                return VerifyCredentials();
            else
                throw new Exception();
        }
    }
}
