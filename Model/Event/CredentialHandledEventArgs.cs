using RealtyModel.Model;
using RealtyModel.Model.Operations;
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
        public Direction Direction { get => Direction.Identity; }
        public Act Action { get; set; }
        public Target Target { get => Target.Agent; }

        public CredentialHandledEventArgs(object data, Boolean isSuccessfully, Act action)
        {
            Data = data;
            IsSuccessfully = isSuccessfully;
            Action = action;
        }
    }
}
