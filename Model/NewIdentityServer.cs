using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using RealtyModel.Model;

namespace RealtorServer.Model
{
    public class NewIdentityServer
    {
        private Boolean isRunning = false;
        private Dispatcher dispatcher = null;
        private ObservableCollection<LogMessage> log;
        private Queue<Operation> identityQueue;
        private Queue<Operation> identityResults;
        private CredentialContext credentialContext;
        private Dictionary<String, String> loggedInClients;

        public delegate void ClientIdentification();
        public event ClientIdentification ClientLoggedIn;
        public event ClientIdentification ClientLoggedOut;

        public NewIdentityServer(Dispatcher dispatcher, ObservableCollection<LogMessage> log, Queue<Operation> inputQueue, Queue<Operation> outputQueue)
        {
            this.log = log;
            this.dispatcher = dispatcher;
            identityQueue = inputQueue;
            identityResults = outputQueue;
            credentialContext = new CredentialContext();
            loggedInClients = new Dictionary<String, String>();
        }


        public async Task RunAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    isRunning = true;
                    while (isRunning)
                    {
                        while (identityQueue.Count > 0)
                        {
                            Handle(identityQueue.Dequeue());
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateLog("(RunAsync) " + ex.Message);
                }
            });
        }


        public Boolean CheckAccess(String ipAddress)
        {
            if (loggedInClients.ContainsKey(ipAddress))
                return true;
            else return false;
        }


        private void Handle(Operation operation)
        {
            try
            {
                Credential credential = JsonSerializer.Deserialize<Credential>(operation.Data);
                if (operation.OperationType == OperationType.Login && FindMatch(credential))
                {
                    String token = GetToken();
                    credential.Token = token;
                    operation.Data = JsonSerializer.Serialize<Credential>(credential);
                    loggedInClients.Add(operation.IpAddress, token);
                    operation.IsSuccessfully = true;
                    UpdateLog($"{operation.IpAddress} has logged in as {credential.Name}");
                }
                else if(operation.OperationType == OperationType.Logout && CheckAccess(operation.IpAddress))
                {
                    loggedInClients.Remove(operation.IpAddress);
                    operation.IsSuccessfully = true;
                    UpdateLog($"{credential.Name} has logged out");
                }
                else if (operation.OperationType == OperationType.Register && !FindMatch(credential))
                {
                    if (!String.IsNullOrWhiteSpace(credential.Name) && !String.IsNullOrWhiteSpace(credential.Password))
                    {
                        credentialContext.Credentials.Local.Add(credential);
                        Credential registeredCredential = credentialContext.Credentials.FirstOrDefault(cre =>
                            cre.Name == credential.Name &&
                            cre.Password == credential.Password);
                        operation.Data = JsonSerializer.Serialize<Credential>(registeredCredential);
                        credentialContext.SaveChanges();
                        operation.IsSuccessfully = true;
                        UpdateLog($"{credential.Name} has registered");
                    }
                    else operation.IsSuccessfully = false;
                }
                else if(operation.OperationType == OperationType.ToFire)
                {
                    if (credentialContext.Credentials.FirstOrDefault(cred => cred.Name == credential.Name) != null)
                    {
                        String value = "";
                        if (loggedInClients.TryGetValue(operation.Token, out value) && value == "Admin")
                        {
                            credentialContext.Credentials.Remove(credential);
                            credentialContext.SaveChanges();
                            operation.IsSuccessfully = true;
                            UpdateLog($"{credential.Name} has fired");
                        }
                        else operation.IsSuccessfully = false;
                    }
                    else operation.IsSuccessfully = false;
                }
                else operation.IsSuccessfully = false;
            }
            catch (Exception ex)
            {
                operation.IsSuccessfully = false;
                UpdateLog("(Handle) " + ex.Message);
            }
            finally
            {
                identityResults.Enqueue(operation);
            }
        }


        private Boolean FindMatch(Credential credential)
        {
            Credential match = credentialContext.Credentials.FirstOrDefault(cred =>
            cred.Name == credential.Name &&
            cred.Password == credential.Password);

            if (match == null)
                return false;
            else
                return true;
        }


        private String GetToken()
        {
            return (new Guid()).ToString();
        }


        private void UpdateLog(String text)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                log.Add(new LogMessage(DateTime.Now.ToString("dd:MM:yyyy hh:mm"), "IdentityServer" + text));
            }));
        }
    }
}
