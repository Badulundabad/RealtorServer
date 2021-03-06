using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public abstract class Server : INotifyPropertyChanged
    {
        protected String serverName = "";
        protected Boolean isRunning = false;
        protected Dispatcher dispatcher;
        protected ObservableCollection<LogMessage> log;
        protected Queue<Operation> incomingQueue;
        protected Queue<Operation> outcomingQueue;
        protected CancellationToken serverToken;

        public Queue<Operation> IncomingQueue
        {
            get => incomingQueue;
            protected set => incomingQueue = value;
        }

        public Boolean IsRunning
        {
            get => isRunning;
            protected set
            {
                isRunning = value;
                OnProperyChanged();
            }
        }

        public Server(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> output)
        {
            serverName = GetType().Name;
            this.dispatcher = dispatcher;
            this.log = log;
            outcomingQueue = output;
        }

        public virtual async void RunAsync()
        {
            await Task.Run(() =>
            {
            });
        }
        public virtual void Stop()
        {
            IsRunning = false;
        }
        protected void UpdateLog(String text)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm:ss"), $"{serverName} {text}"));
            }));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnProperyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
