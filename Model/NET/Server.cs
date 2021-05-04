using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NLog;
using RealtyModel.Model;

namespace RealtorServer.Model.NET
{
    public abstract class Server : INotifyPropertyChanged
    {
        private String name = "";
        private Boolean isRunning = false;
        private Dispatcher dispatcher;
        private OperationQueue incomingOperations; 
        private OperationQueue outcomingOperations; 
        private static Logger logger = LogManager.GetCurrentClassLogger();
        protected object handleLocker = new object();
        public event PropertyChangedEventHandler PropertyChanged;

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

        public Server(Dispatcher dispatcher)
        {
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
        protected void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} {this.GetType().Name} {text}");
            logger.Info($"{this.GetType().Name} {text}");
        }
        protected void LogError(String text)
        {
            Debug.WriteLine($"\n{DateTime.Now} ERROR {this.GetType().Name} {text}\n");
            logger.Error($"{this.GetType().Name} {text}");
        }
        protected void OnProperyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
