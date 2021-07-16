using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Operations;
using RealtyModel.Model.Tools;
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
            using (AgentContext context = new AgentContext()) {
                bool hasMatchingAgent = context.Agents.FirstOrDefault(a => a.NickName == credential.Name && a.Password == credential.Password) != null;
                if (hasMatchingAgent) {
                    int id = context.Agents.First(a => a.NickName == credential.Name && a.Password == credential.Password).Id;
                    Tuple<bool, int> pair = new Tuple<bool, int>(true, id);
                    LogInfo($"{operation.Ip} logged in as {credential.Name}");
                    return new Response(BinarySerializer.Serialize(pair));
                } else {
                    LogInfo($"{operation.Ip} could not log in");
                    Tuple<bool, int> pair = new Tuple<bool, int>(false, 0);
                    return new Response(BinarySerializer.Serialize(pair), ErrorCode.Credential);
                }
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
