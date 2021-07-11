using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
using System.Diagnostics;
using System.Linq;

namespace RealtorServer.Model.NET
{
    class Adding : OperationHandling
    {
        public Adding(Operation operation) {
            this.operation = operation;
        }

        public override Response Handle() {
            if (operation.Target == Target.Flat) {
                return AddFlat();
            } else {
                LogWarn($"operation has a wrong target");
                return new Response(Array.Empty<byte>(), ErrorCode.WrongTarget);
            }
        }

        private Response AddFlat() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            //try
            //{
            Flat flat = BinarySerializer.Deserialize<Flat>(operation.Data);
            if (!IsDuplicate(flat)) {
                using (RealtyContext context = new RealtyContext()) {
                    flat.AlbumId = AddOrUpdateAlbum(flat.Album, context);
                    SetDates(flat);
                    AddStreetIfNotExist(flat, context);
                    context.Flats.Add(flat);
                    context.SaveChanges();
                    LogInfo($"Flat has been registered");
                    response.Code = ErrorCode.FlatAddedSuccessfuly;
                }
            } else {
                LogWarn("Flat already exists");
                response.Code = ErrorCode.FlatDuplicate;
            }
            //}
            //catch (Exception ex)
            //{
            //    LogError($"(AddFlat) {ex.Message}");
            //}
            return response;
        }

        

    }
}
