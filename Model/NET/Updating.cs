using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using RealtyModel.Model.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RealtorServer.Model.NET
{
    class Updating : OperationHandling
    {
        public Updating(Operation operation) {
            this.operation = operation;
        }
        public override Response Handle() {
            if (operation.Target == Target.Flat) {
                return UpdateFlat();
            } else if (operation.Target == Target.House) {
                return UpdateHouse();
            } else {
                return UpdateAgents();
            }
        }
        private Response UpdateAgents() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            try {
                SymmetricEncryption encrypted = BinarySerializer.Deserialize<SymmetricEncryption>(operation.Data);
                LogInfo($"Received agents");
                List<Agent> modifiedAgents = encrypted.Decrypt<List<Agent>>();
                LogInfo($"Decrypted agents");
                using (AgentContext context = new AgentContext()) {
                    context.Agents.Local.Clear();
                    foreach (Agent a in modifiedAgents) {
                        a.NickName = $"{a.Surname}{a.Name[0]}{a.Patronymic[0]}";
                        context.Agents.Local.Add(a);
                    }
                    context.SaveChanges();
                    LogInfo($"Updated {modifiedAgents.Count} agents");
                }
                response.Data = BinarySerializer.Serialize(true);
                response.Code = ErrorCode.CredentialUpdatedSuccessfuly;
            } catch (Exception) {
                response.Data = BinarySerializer.Serialize(false);
                response.Code = ErrorCode.Unknown;
            }
            LogInfo($"Sent a response");
            return response;
        }

        private Response UpdateFlat() {
            Response response = new Response(Array.Empty<Byte>(), ErrorCode.Unknown);
            try {
                Flat modifiedFlat = BinarySerializer.Deserialize<Flat>(operation.Data);
                using (RealtyContext context = new RealtyContext()) {
                    SetDates(modifiedFlat);
                    Flat flatToModify = context.Flats.First(f => f.Id == modifiedFlat.Id);
                    AddOrUpdateAlbum(modifiedFlat.Album, context);
                    context.Entry(flatToModify).CurrentValues.SetValues(modifiedFlat);

                    context.SaveChanges();
                    LogInfo($"Flat #{modifiedFlat.Id} has was updated by {operation.Name}");
                    response.Code = ErrorCode.ObjectUpdatedSuccessfuly;
                }
            } catch (Exception ex) {
                LogError(ex.InnerException.Message);
                response.Code = ErrorCode.Unknown;
            }
            LogInfo($"Sent a response");
            return response;
        }
        private Response UpdateHouse() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            try {
                House modifiedHouse = BinarySerializer.Deserialize<House>(operation.Data);
                using (RealtyContext context = new RealtyContext()) {
                    SetDates(modifiedHouse);
                    House houseToModify = context.Houses.First(h => h.Id == modifiedHouse.Id);
                    AddOrUpdateAlbum(modifiedHouse.Album, context);
                    context.Entry(houseToModify).CurrentValues.SetValues(modifiedHouse);

                    context.SaveChanges();
                    response.Code = ErrorCode.ObjectUpdatedSuccessfuly;
                    LogInfo($"House #{modifiedHouse.Id} has was updated by {operation.Name}");
                }
            } catch (Exception ex) {
                LogError(ex.InnerException.Message);
                response.Code = ErrorCode.Unknown;
            }
            LogInfo($"Sent a response");
            return response;
        }
    }
}
