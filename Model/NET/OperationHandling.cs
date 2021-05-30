using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    class OperationHandling
    {
        protected Operation operation;
        public virtual byte[] Handle() {
            throw new NotImplementedException();
        }
    }
}
