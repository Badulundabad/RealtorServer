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
        private Operation operation;
        public Operation Operation { get => operation; set => operation = value; }
        public OperationHandledEventArgs(Operation operation)
        {
            this.operation = operation;
        }
    }
}
