using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                throw new NotImplementedException();
            }
        }
        private Response UpdateFlat() {
            throw new NotImplementedException();
        }
    }
}
