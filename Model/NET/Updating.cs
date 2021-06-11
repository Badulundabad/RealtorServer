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
                    Flat flatToModify = context.Flats.Local.First(flat => flat.Id == modifiedFlat.Id);
                    if (modifiedFlat.Album.PhotoCollection.Length > 0)
                    {
                        Album oldAlbum = context.Albums.Find(modifiedFlat.AlbumId);
                        if (oldAlbum != null)
                            oldAlbum.PhotoCollection = modifiedFlat.Album.PhotoCollection;
                    }
                    if (modifiedFlat.Album.Location.Length > 0)
                    {
                        Album oldAlbum = context.Albums.Find(modifiedFlat.AlbumId);
                        if (oldAlbum != null)
                            oldAlbum.Location = modifiedFlat.Album.Location;
                    }
                    //if (modifiedFlat.Location.Street.Id > 0)
                    //    flatToModify.Location.Street = context.Streets.Find(modifiedFlat.Location.Street.Id) ?? modifiedFlat.Location.Street;
                    if (!modifiedFlat.Location.Equals(flatToModify.Location))
                    {
                        Debug.WriteLine("locations are not equal");
                        flatToModify.Location.GetValues(modifiedFlat.Location);
                        if (flatToModify.Location.Street.Id != modifiedFlat.Location.Street.Id)
                        {
                            Debug.WriteLine("streets are not equal");
                            flatToModify.Location.Street = context.Streets.Local.First(street => street.Id == flatToModify.Location.StreetId);
                            flatToModify.Location.StreetId = flatToModify.Location.Street.Id;
                        }
                    }
                    flatToModify.Agent = modifiedFlat.Agent;
                    flatToModify.Cost = modifiedFlat.Cost;
                    flatToModify.CustomerName = modifiedFlat.CustomerName;
                    flatToModify.CustomerPhoneNumbers = modifiedFlat.CustomerPhoneNumbers;
                    flatToModify.GeneralInfo = modifiedFlat.GeneralInfo;
                    flatToModify.Info = modifiedFlat.Info;
                    flatToModify.HasExclusive = modifiedFlat.HasExclusive;
                    flatToModify.LastCallTime = modifiedFlat.LastCallTime;
                    flatToModify.Preview = modifiedFlat.Preview;
                    flatToModify.LastUpdateTime = DateTime.Now;

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
