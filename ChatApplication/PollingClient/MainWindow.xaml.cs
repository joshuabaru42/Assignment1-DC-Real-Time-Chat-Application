using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PollingClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IChannelService proxy;
        private DispatcherTimer pollTimer;
        private DateTime lastPollTime;
        public MainWindow()
        {
            InitializeComponent();
            InitializeWcfProxy();
            InitializePollingTimer();
        }

        private void InitializeWcfProxy()
        {
            ChannelFactory<IChannelService> channelFactory;
            NetTcpBinding tcp = new NetTcpBinding();

            string URL = "net.tcp://localhost:8100/DataService";
            channelFactory = new ChannelFactory<IChannelService>(tcp, URL);
            proxy = channelFactory.CreateChannel();
        }

        private void InitializePollingTimer()
        {
            lastPollTime = DateTime.MinValue;
            pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) 
            };
            pollTimer.Tick += PollTimer_Tick;
        }

        private void CreateChannelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string channelName = TxtChannelName.Text.Trim();
                bool created = proxy.CreateChannel(channelName);
                MessageBox.Show(created ? $"Channel '{channelName}' created!" : "Channel already exists");

            }
            catch(Exception ex)
            {
                MessageBox.Show($"Error creating channel: {ex.Message}");
            }
        }

        private void JoinChannelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string channelName = TxtChannelName.Text.Trim();
                string userId = TxtUserId.Text.Trim();

                bool joined = proxy.JoinChannel(channelName, userId);

                if (joined) 
                {
                    MessageBox.Show($"Joined '{channelName}' as {userId}!");
                    lastPollTime = DateTime.MinValue;
                    pollTimer.Start();
                    RefreshMembers();

                }
                else
                {
                    MessageBox.Show("Failed to join channel.");
                }


            }
            catch (Exception ex) 
            {
                MessageBox.Show($"Error joining channel : {ex.Message}");
            }
        }

        private void SendButton_CLick(object sender, RoutedEventArgs e)
        {
            try
            {

                string channelName = TxtChannelName.Text.Trim();
                string userId = TxtUserId.Text.Trim();
                string message = TxtMessage.Text.Trim();

                if (!string.IsNullOrEmpty(message))
                {
                    proxy.SendPublicMessage(userId, message);
                    TxtMessage.Clear();

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}");
            }
        }

        public void PollTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                string channelName = TxtChannelName.Text.Trim();
                string userId = TxtUserId.Text.Trim();

                List<PublicMessage> newMessages = proxy.GetNewPublicMessages(userId, lastPollTime);

                foreach (var msg in newMessages)
                {
                    MessageList.Items.Add($"[{msg.Timestamp:HH:mm:ss}] {msg.SenderId}: {msg.Content}");
                    if (msg.Timestamp > lastPollTime)
                    {
                        lastPollTime = msg.Timestamp;
                    }

                }

                RefreshMembers();
            }
            catch
            {

            }
        }

        private void RefreshMembers()
        {
            try
            {
                string channelName = TxtChannelName.Text.Trim();
                List<string> members = proxy.GetChannelMembers(channelName);

                MemberList.Items.Clear();
                foreach (var member in members){

                    MemberList.Items.Add(member);

                }
            }
            catch 
            { 
            
            }
        }
    }
}
