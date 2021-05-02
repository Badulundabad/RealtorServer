using RealtyModel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.Event
{
    public class OperationReceivedEventArgs
    {
        private Operation operation;
        public Operation Operation { get => operation; set => operation = value; }
        public OperationReceivedEventArgs(Operation operation)
        {
            this.operation = operation;
        }
    }
}
