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
    }
}
