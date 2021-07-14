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
                return AddHouse(); ;
            }
        }
        private Response AddHouse() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            House house = BinarySerializer.Deserialize<House>(operation.Data);
            if (!IsDuplicate(house)) {
                using (RealtyContext context = new RealtyContext()) {
                    house.AlbumId = AddOrUpdateAlbum(house.Album, context);
                    SetDates(house);
                    AddStreetIfNotExist(house, context);
                    context.Houses.Add(house);
                    context.SaveChanges();
                    LogInfo($"A new house was added by {operation.Name}");
                    response.Code = ErrorCode.ObjectAddedSuccessfuly;
                }
            } else {
                LogWarn($"{operation.Name} tried to add an existing house");
                response.Code = ErrorCode.ObjectDuplicate;
            }
            return response;
        }
        private Response AddFlat() {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            try {
                Flat flat = BinarySerializer.Deserialize<Flat>(operation.Data);
                //if (!IsDuplicate(flat)) {
                    using (RealtyContext context = new RealtyContext()) {
                        flat.AlbumId = AddOrUpdateAlbum(flat.Album, context);
                        SetDates(flat);
                        AddStreetIfNotExist(flat, context);
                        context.Flats.Add(flat);
                        context.SaveChanges();
                        LogInfo($"A new flat was added by {operation.Name}");
                        response.Code = ErrorCode.ObjectAddedSuccessfuly;
                    }
                //} else {
                //    LogWarn($"{operation.Name} tried to add an existing flat");
                //    response.Code = ErrorCode.ObjectDuplicate;
                //}
            } catch (Exception ex) {
                LogError(ex.Message);
            }
            return response;
        }



    }
}
