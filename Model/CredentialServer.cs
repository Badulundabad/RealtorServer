using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class CredentialServer
    {
        Boolean isRunning;
        Dispatcher uiDispatcher;
        public Queue<Operation> IdentityQueue { get; private set; }
        public ObservableCollection<Credential> Credentials { get; private set; }
        public ObservableCollection<LogMessage> Log { get; private set; }
        public ObservableCollection<Operation> IdentityResults { get; private set; }

        public CredentialServer(Dispatcher dispatcher)
        {
            uiDispatcher = dispatcher;
            CredentialContext dataBase = new CredentialContext();
            Credentials = dataBase.Credentials.Local;
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
                            Credential identityData = JsonSerializer.Deserialize<Credential>(nextIdentity.Data);
                            if (nextIdentity.OperationType == OperationType.Login)
                            {
                                if (Credentials.Where(user => user.Name == identityData.Name && user.Password == identityData.Password).Count() == 1)
                                    nextIdentity.IsSuccessfully = true;
                                else nextIdentity.IsSuccessfully = false;
                            }
                            else if (nextIdentity.OperationType == OperationType.Register)
                            {
                                if (Credentials.Where(user => user.Name == identityData.Name && user.Password == identityData.Password).Count() == 0)
                                {
                                    nextIdentity.IsSuccessfully = true;
                                    uiDispatcher.BeginInvoke(new Action(() =>
                                    {
                                        Credentials.Add(identityData);
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
