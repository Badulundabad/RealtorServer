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
        public Updating(Operation operation)
        {
            this.operation = operation;
        }
        public override Response Handle()
        {
            if (operation.Target == Target.Flat)
            {
                return UpdateFlat();
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        private Response UpdateFlat()
        {
            Response response = new Response(Array.Empty<Byte>(), ErrorCode.Unknown);
            try
            {
                Flat modifiedFlat = BinarySerializer.Deserialize<Flat>(operation.Data);
                using (RealtyContext context = new RealtyContext())
                {
                    SetDates(modifiedFlat);
                    Flat flatToModify = context.Flats.First(f => f.Id == modifiedFlat.Id);
                    AddOrUpdateAlbum(modifiedFlat.Album, context);
                    context.Entry(flatToModify).CurrentValues.SetValues(modifiedFlat);
                    
                    context.SaveChanges();
                    LogInfo($"Flat has modified");
                    response.Code = ErrorCode.FlatUpdatedSuccessfuly;
                }
            }
            catch (Exception ex)
            {
                LogError($"(AddFlat) {ex.Message}");
            }
            return response;
        }
    }
}
