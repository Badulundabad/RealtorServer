using RealtyModel.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RealtorServer.Model
{
    public class RealtyServer : Server
    {
        private RealtyContext RealtyContext = new RealtyContext();
        private AlbumContext AlbumContext = new AlbumContext();

        public RealtyServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> input, Queue<Operation> output) : base(dispatcher, log, input, output)
        {
            this.dispatcher = dispatcher;
            this.log = log;
            incomingQueue = input;
            outcomingQueue = output;
        }

        public override async Task RunAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    while (isRunning)
                    {
                        while (incomingQueue.Count > 0)
                        {
                            Handle(incomingQueue.Dequeue());
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog("(RunAsync) " + ex.Message);
                }
                finally
                {
                    UpdateLog(" has stopped");
                }
            });
        }

        private void Handle(Operation operation)
        {

        }
    }
}
