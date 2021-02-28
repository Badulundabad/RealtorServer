using RealtyModel.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RealtorServer.Model.NET
{
    public class FilterServer : Server
    {
        public FilterServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output, CancellationToken cancellationToken) : base (dispatcher, log, output, token)
    }
}
