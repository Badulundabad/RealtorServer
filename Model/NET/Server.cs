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
        protected object handleLocker = new object();
        private String name = "";
        private Boolean isRunning = false;
        private Dispatcher dispatcher;
        private OperationQueue incomingOperations; 
        private OperationQueue outcomingOperations; 
        private ObservableCollection<LogMessage> log;

        public String Name
        {
            get => name;
            protected set
            {
                name = value;
            }
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
        public Dispatcher Dispatcher
        {
            get => dispatcher;
            protected set
            {
                dispatcher = value;
            }
        }
        public OperationQueue IncomingOperations
        {
            get => incomingOperations;
            protected set
            {
                incomingOperations = value;
            }
        }
        public OperationQueue OutcomingOperations
        {
            get => outcomingOperations;
            protected set
            {
                outcomingOperations = value;
            }
        }
        public ObservableCollection<LogMessage> Log
        {
            get => log;
            protected set
            {
                log = value;
            }
        }

        public Server(Dispatcher dispatcher, ObservableCollection<LogMessage> log)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            name = GetType().Name;
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
                log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm:ss"), $"{name} {text}"));
            }));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnProperyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
