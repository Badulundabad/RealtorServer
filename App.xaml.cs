using RealtorServer.View;
using RealtorServer.ViewModel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RealtorServer
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        private RealtorServerViewModel viewModel = new RealtorServerViewModel();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow = new MainWindow() { DataContext = viewModel };
            MainWindow.Show();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            viewModel.Server.DisconnectAllClients();
            viewModel.StopCommand.Execute(new object());
            base.OnExit(e);
        }
    }
}
