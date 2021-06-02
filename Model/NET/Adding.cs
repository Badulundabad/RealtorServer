using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    class Adding : OperationHandling
    {
        public Adding(Operation operation) {
            this.operation = operation;
        }
        private Response AddFlat() {
            Flat flat = BinarySerializer.Deserialize<Flat>(operation.Data);
            using (RealtyContext realtyContext = new RealtyContext()) {
                realtyContext.Flats.Add(flat);
                realtyContext.SaveChanges();
            }
            Response response = new Response(Array.Empty<byte>(), ErrorCode.FlatAddedSuccessfuly);
            return response;
        }
        public override Response Handle() {
            if(operation.Target == Target.Flat) {
                return AddFlat();
            } else {
                throw new NotImplementedException();
            }
        }
    }
}
