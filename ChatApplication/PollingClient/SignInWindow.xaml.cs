using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SharedLibrary;
using System.ServiceModel;

namespace PollingClient
{
    public partial class SignInWindow : Window
    {
        public SignInWindow()
        {
            InitializeComponent();
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string userId = UserIdTextBox.Text.Trim();

            NetTcpBinding tcpBinding = new NetTcpBinding();

            ChannelFactory<IChannelService> channelFactory =
                new ChannelFactory<IChannelService>(
                    tcpBinding,
                    "net.tcp://localhost:8100/DataService");

            IChannelService proxy = channelFactory.CreateChannel();

            try
            {
                string reason;

                bool success = proxy.SignIn(userId, out reason);

                if (success)
                {
                    MainWindow mainWindow = new MainWindow(userId);
                    mainWindow.Show();

                    this.Close();
                }
                else
                {
                    MessageBox.Show(
                        reason,
                        "Sign In Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                ((IClientChannel)proxy).Close();
                channelFactory.Close();
            }
            catch (TimeoutException)
            {
                MessageBox.Show(
                    "The server did not respond in time.",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                channelFactory.Abort();
            }
            catch (CommunicationException)
            {
                MessageBox.Show(
                    "Unable to communicate with the Chat Server.",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                channelFactory.Abort();
            }
        }
    }
}
