using RealtyModel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.Event
{
    public class OperationHandledEventArgs
    {
        public Operation Operation { get; set; }

        public OperationHandledEventArgs(Operation operation)
        {
            Operation = operation;
        }
    }
}
