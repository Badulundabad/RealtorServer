using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using RealtyModel.Service;
using System;
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
                return GetStreets();
            } else if (operation.Target == Target.RealtorObjects) {
                return GetRealtorObjects();
            } else if (operation.Target == Target.Album) {
                return GetAlbum();
            } else {
                throw new NotImplementedException();
            }
        }

        private Response GetStreets() {
            Street[] streets;
            using (RealtyContext context = new RealtyContext()) {
                streets = context.Streets.Local.ToArray();
            }
            Response response = new Response(BinarySerializer.Serialize(streets));
            LogInfo($"Sent streets to {operation.Name}");
            return response;
        }
        private Response GetRealtorObjects() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.NoCode);
            try {
                using (RealtyContext realtyContext = new RealtyContext()) {
                    Filter filter = BinarySerializer.Deserialize<Filter>(operation.Data);
                    Flat[] flats = realtyContext.Flats.Local.ToArray();
                    House[] houses = realtyContext.Houses.Local.ToArray();
                    Tuple<Flat[], House[]> objects = new Tuple<Flat[], House[]>(flats, houses);
                    response.Data = BinarySerializer.Serialize(objects);
                    LogInfo($"Sent realtor objects to {operation.Name}");
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                response.Code = ErrorCode.Unknown;
            }
            return response;
        }
        private Response GetAlbum() {
            Response response = new Response(Array.Empty<Byte>(), ErrorCode.NoCode);
            try {
                Int32 id = BinarySerializer.Deserialize<Int32>(operation.Data);
                using (RealtyContext context = new RealtyContext()) {
                    Album album = context.Albums.FirstOrDefault(a => a.Id == id);
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
