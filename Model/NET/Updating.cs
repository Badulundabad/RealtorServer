using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
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
            } else {
                return UpdateHouse();
            }
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
                    response.Code = ErrorCode.ObjectUpdatedSuccessfuly;
                    LogInfo($"Flat #{modifiedFlat.Id} has been updated by {operation.Name}");
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                response.Code = ErrorCode.Unknown;
            }
            return response;
        }
        private Response UpdateHouse() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            try {
                House modifiedHouse = BinarySerializer.Deserialize<House>(operation.Data);
                using(RealtyContext context = new RealtyContext()) {
                    SetDates(modifiedHouse);
                    House houseToModify = context.Houses.First(h => h.Id == modifiedHouse.Id);
                    AddOrUpdateAlbum(modifiedHouse.Album, context);
                    context.Entry(houseToModify).CurrentValues.SetValues(modifiedHouse);

                    context.SaveChanges();
                    response.Code = ErrorCode.ObjectUpdatedSuccessfuly;
                    LogInfo($"House #{modifiedHouse.Id} has been updated by {operation.Name}");
                }
            } catch (Exception ex) {
                LogError(ex.Message);
                response.Code = ErrorCode.Unknown;
            }
            return response;
        }
    }
}
