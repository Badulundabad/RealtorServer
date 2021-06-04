using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;

namespace RealtorServer.Model.NET
{
    class Adding : OperationHandling
    {
        public Adding(Operation operation)
        {
            this.operation = operation;
        }

        public override Response Handle()
        {
            if (operation.Target == Target.Flat)
            {
                return AddFlat();
            }
            else
            {
                LogWarn($"operation has a wrong target");
                return new Response(Array.Empty<byte>(), ErrorCode.WrongTarget);
            }
        }

        private Response AddFlat()
        {
            Response response = new Response(Array.Empty<byte>(), ErrorCode.Unknown);
            try
            {
                Flat flat = BinarySerializer.Deserialize<Flat>(operation.Data);
                if (!FindLocationDuplicate(flat))
                {
                    using (RealtyContext context = new RealtyContext())
                    {
                        context.Albums.Add(flat.Album);
                        context.SaveChanges();

                        if (flat.Customer.Id > 0)
                            flat.Customer = context.Customers.Find(flat.Customer.Id) ?? flat.Customer;
                        if (flat.Location.City.Id > 0)
                            flat.Location.City = context.Cities.Find(flat.Location.City.Id) ?? flat.Location.City;
                        if (flat.Location.District.Id > 0)
                            flat.Location.District = context.Districts.Find(flat.Location.District.Id) ?? flat.Location.District;
                        if (flat.Location.Street.Id > 0)
                            flat.Location.Street = context.Streets.Find(flat.Location.Street.Id) ?? flat.Location.Street;

                        flat.AlbumId = flat.Album.Id;
                        flat.RegistrationDate = DateTime.Now;
                        flat.LastUpdateTime = flat.RegistrationDate;
                        flat.RegistrationDate = flat.RegistrationDate;
                        
                        context.Flats.Add(flat);
                        context.SaveChanges();
                        LogInfo($"Flat has registered");
                        response.Code = ErrorCode.FlatAddedSuccessfuly;
                    }
                }
                else
                {
                    LogWarn("Flat already exists");
                    response.Code = ErrorCode.FlatDuplicate;
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
