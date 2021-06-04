using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;

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
                Flat modifiedflat = BinarySerializer.Deserialize<Flat>(operation.Data);
                using (RealtyContext context = new RealtyContext())
                {
                    if (modifiedflat.Album.PhotoCollection.Length > 0)
                    {
                        Album oldAlbum = context.Albums.Find(modifiedflat.AlbumId);
                        if (oldAlbum != null)
                            oldAlbum.PhotoCollection = modifiedflat.Album.PhotoCollection;
                    }
                    if (modifiedflat.Album.Location.Length > 0)
                    {
                        Album oldAlbum = context.Albums.Find(modifiedflat.AlbumId);
                        if (oldAlbum != null)
                            oldAlbum.Location = modifiedflat.Album.Location;
                    }
                    if (modifiedflat.Location.Street.Id > 0)
                        modifiedflat.Location.Street = context.Streets.Find(modifiedflat.Location.Street.Id) ?? modifiedflat.Location.Street;

                    modifiedflat.LastUpdateTime = DateTime.Now;

                    context.Flats.Add(modifiedflat);
                    context.SaveChanges();
                    LogInfo($"Flat has registered");
                    response.Code = ErrorCode.FlatAddedSuccessfuly;
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
