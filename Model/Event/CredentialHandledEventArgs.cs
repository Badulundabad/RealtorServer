using RealtyModel.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.Event
{
    public class CredentialHandledEventArgs
    {
        public object Data { get; set; }
        public Boolean IsSuccessfully { get; set; }
        public OperationDirection Direction { get => OperationDirection.Identity; }
        public OperationType OperationType { get; set; }
        public TargetType Target { get => TargetType.Agent; }

        public CredentialHandledEventArgs(object data, Boolean isSuccessfully, OperationType operationType)
        {
            Data = data;
            IsSuccessfully = isSuccessfully;
            OperationType = operationType;
        }
    }
}
