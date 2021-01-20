using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class PrimaryFilter
    {
        public Boolean IsRunning { get; private set; }
        public Queue<Operation> IncomingOperations { get; private set; }
        public Queue<Operation> IdentityQueue { get; private set; }
        public Queue<Operation> NextQueue { get; private set; }
        public Queue<Operation> ReturnQueue { get; private set; }
        public List<String> TokenList { get; private set; }

        public PrimaryFilter()
        {
            IncomingOperations = new Queue<Operation>();
            IdentityQueue = new Queue<Operation>();
            NextQueue = new Queue<Operation>();
            ReturnQueue = new Queue<Operation>();
            TokenList = new List<String>();
            Start();
        }

        private async void Start()
        {
            await Task.Run(()=> 
            {
                IsRunning = true;
                while (IsRunning)
                {
                    while (IncomingOperations.Count > 0)
                    {
                        Handle(IncomingOperations.Dequeue());
                    }
                }
            });
        }

        private void Handle(Operation operation)
        {
            if(operation.OperationType == OperationType.Login || operation.OperationType == OperationType.Register)
                IdentityQueue.Enqueue(operation);
            else
            {
                if(operation.Token == "")
                {
                    operation.IsSuccessfully = false;
                    operation.Data = "";
                    ReturnQueue.Enqueue(operation);
                }
                else
                {

                }
            }
        }
    }
}
