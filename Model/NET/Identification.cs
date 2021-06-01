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
    class Identification : OperationHandling {
        public Identification() {
        }
        public Identification(Operation operation) {
            this.operation = operation;
        }
        private Response HandleLogin() {
            Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
            bool hasMatchingCredentials = new CredentialContext().Credentials.FirstOrDefault(x => x.Name == credential.Name && x.Password == credential.Password) != null;
            if (hasMatchingCredentials) {
                return new Response(BinarySerializer.Serialize(true), ErrorCode.NoCode);
            } else {
                return new Response(BinarySerializer.Serialize(false), ErrorCode.Credential);
            }
        }
        private Response HandleRegistration() {
            throw new NotImplementedException();
        }
        public override Response Handle() {
            if (operation.Action == Action.Login) {
                return HandleLogin();
            } else {
                return HandleRegistration();
            }
        }
    }
}
