using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    class Requesting : OperationHandling
    {
        public Requesting(Operation operation) {
            this.operation = operation;
        }
        private byte[] HandleLocationRequest() {
            
        }
        public override byte[] Handle() {
            if (operation.Target == Target.Locations) {
                return HandleLocationRequest();
            } else {
                throw new NotImplementedException();
            }
        }

    }
}
