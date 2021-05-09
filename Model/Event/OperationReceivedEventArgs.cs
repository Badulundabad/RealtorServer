using RealtyModel.Model.Operations;

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
