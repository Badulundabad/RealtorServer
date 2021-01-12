using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel;

namespace RealtorServer.Model
{
    public class IdentityServer
    {
        Boolean isRunning;
        Dispatcher uiDispatcher;
        public Queue<Operation> IdentityQueue { get; private set; }
        public ObservableCollection<User> Users { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }
        public ObservableCollection<Operation> IdentityResults { get; private set; }

        public IdentityServer(Dispatcher dispatcher)
        {
            uiDispatcher = dispatcher;
            IdentityContext dataBase = new IdentityContext();
            Users = dataBase.Users.Local;
            Log = new ObservableCollection<LogMessage>();
            IdentityQueue = new Queue<Operation>();
            IdentityResults = new ObservableCollection<Operation>();
        }

        public async void RunAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    isRunning = true;
                    UpdateLog("is working");
                    while (isRunning)
                    {
                        while (IdentityQueue.Count > 0)
                        {
                            Operation nextIdentity = IdentityQueue.Dequeue();
                            User identityData = JsonSerializer.Deserialize<User>(nextIdentity.Data);
                            if (nextIdentity.OperationType == OperationType.Login)
                            {
                                if (Users.Where(user => user.Name == identityData.Name && user.Password == identityData.Password).Count() == 1)
                                    nextIdentity.IsSuccessfully = true;
                                else nextIdentity.IsSuccessfully = false;
                            }
                            else if (nextIdentity.OperationType == OperationType.Register)
                            {
                                if (Users.Where(user => user.Name == identityData.Name && user.Password == identityData.Password).Count() == 0)
                                {
                                    nextIdentity.IsSuccessfully = true;
                                    uiDispatcher.BeginInvoke(new Action(() =>
                                    {
                                        Users.Add(identityData);
                                    }));
                                }
                                else nextIdentity.IsSuccessfully = false;
                            }
                            else nextIdentity.IsSuccessfully = false;
                            IdentityResults.Add(nextIdentity);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog("(RunAsync)threw an exception: " + ex.Message);
                }
                finally
                {
                    UpdateLog("was stopped");
                }
            });
        }
        public void Stop() 
        {
            try
            {
                isRunning = false;
                IdentityQueue.Clear();
            }
            catch(Exception ex)
            {
                UpdateLog("(Stop)threw an exception: " + ex.Message);
            }
        }
        private void UpdateLog(String message)
        {
            uiDispatcher.BeginInvoke(new Action(() =>
            {
                Log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yy hh:mm"), "Identity server " + message));
            }));
        }
    }
}
