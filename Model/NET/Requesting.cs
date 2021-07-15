using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using RealtyModel.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RealtorServer.Model.NET
{
    class Requesting : OperationHandling
    {
        public Requesting(Operation operation) {
            this.operation = operation;
        }
        public override Response Handle() {
            if (operation.Target == Target.Locations) {
                return RequestStreets();
            } else if (operation.Target == Target.RealtorObjects) {
                return RequestRealtorObjects();
            } else if (operation.Target == Target.Album) {
                return RequestAlbum();
            } else if (operation.Target == Target.Agent) {
                return RequestCredentials();
            } else {
                throw new NotImplementedException();
            }
        }
        private Response RequestCredentials() {
            List<Credential> credentials = new List<Credential>();
            using (CredentialContext context = new CredentialContext()) {
                credentials.AddRange(context.Credentials.Local);
            }
            LogInfo($"Retrieved {credentials.Count} credentials");
            Response response = new Response(new SymmetricEncryption(credentials).Encrypt<List<Credential>>(), ErrorCode.NoCode);
            LogInfo($"Encrypted credentials");
            LogInfo($"Sent credentials to {operation.Name}");
            return response;
        }
        private Response RequestStreets() {
            List<string> streets = new List<string>();
            using (RealtyContext context = new RealtyContext()) {
                foreach (Street s in context.Streets.Local) {
                    streets.Add(s.Name);
                }
            }
            LogInfo($"Retrieved {streets.Count} streets");
            string[] namesOfStreets = streets.OrderBy(s => s).ToArray();
            LogInfo("Sorted streets");
            Response response = new Response(BinarySerializer.Serialize(namesOfStreets));
            LogInfo($"Sent streets to {operation.Name}");
            return response;
        }
        private Response RequestRealtorObjects() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.NoCode);
            try {
                using (RealtyContext realtyContext = new RealtyContext()) {
                    Filtration filtration = BinarySerializer.Deserialize<Filtration>(operation.Data);
                    List<Flat> flats = realtyContext.Flats.Local.ToList();
                    LogInfo($"Retrieved {flats.Count} flats");
                    filtration.Filter(flats);
                    LogInfo($"Filtered flats — {flats.Count}");
                    List<House> houses = realtyContext.Houses.Local.ToList();
                    LogInfo($"Retrieved {houses.Count} houses");
                    filtration.Filter(houses);
                    LogInfo($"Filtered houses — {houses.Count}");
                    Tuple<Flat[], House[]> objects = new Tuple<Flat[], House[]>(flats.ToArray(), houses.ToArray());
                    response.Data = BinarySerializer.Serialize(objects);
                    LogInfo($"Sent realtor objects to {operation.Name}");
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                response.Code = ErrorCode.Unknown;
            }
            return response;
        }
        private Response RequestAlbum() {
            Response response = new Response(Array.Empty<Byte>(), ErrorCode.NoCode);
            try {
                Int32 id = BinarySerializer.Deserialize<Int32>(operation.Data);
                using (RealtyContext context = new RealtyContext()) {
                    Album album = context.Albums.FirstOrDefault(a => a.Id == id);
                    LogInfo($"Retrieved the album");
                    if (album != null) {
                        response.Data = BinarySerializer.Serialize(album);
                        LogInfo($"Sent album #{album.Id} to {operation.Name}");
                    }
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                response.Code = ErrorCode.Unknown;
            }
            return response;
        }
    }
}
