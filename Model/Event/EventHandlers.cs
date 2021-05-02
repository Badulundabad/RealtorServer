using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.Event
{
    public class EventHandlers
    {
        public delegate void DisconnectedEventHandler(object sender, DisconnectedEventArgs e);
        public delegate void OperationReceivedEventHandler(object sender, OperationReceivedEventArgs e);
        public delegate void OperationHandledEventHandler(object sender, OperationHandledEventArgs e);
    }
}
