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
            Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
            bool hasMatchingCredentials = new CredentialContext().Credentials.FirstOrDefault(x => x.Name == credential.Name && x.Password == credential.Password) != null;
            if (hasMatchingCredentials) {
                LogInfo($"{operation.Ip} has logged in as {credential.Name}");
                return new Response(BinarySerializer.Serialize(true));
            } else {
                LogInfo($"{operation.Ip} could not log in");
                return new Response(BinarySerializer.Serialize(false), ErrorCode.Credential);
            }
        }
        private Response Register() {
            try {
                Credential credential = BinarySerializer.Deserialize<Credential>(operation.Data);
                using (CredentialContext context = new CredentialContext()) {
                    if (!context.Credentials.Any(cred => cred.Name == credential.Name)) {
                        context.Credentials.Add(credential);
                        context.SaveChanges();
                        LogInfo($"{credential.Name} has been registered");
                        return new Response(BinarySerializer.Serialize(true), ErrorCode.Successful);
                    } else {
                        LogWarn($"Agent {credential.Name} already exists");
                        return new Response(BinarySerializer.Serialize(false), ErrorCode.AgentExists);
                    }
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                return new Response(BinarySerializer.Serialize(false), ErrorCode.Unknown);
            }
        }
        public override Response Handle() {
            if (operation.Action == Action.Login)
                return VerifyCredentials();
            else if (operation.Action == Action.Register)
                return Register();
            else
                throw new Exception();
        }
    }
}
