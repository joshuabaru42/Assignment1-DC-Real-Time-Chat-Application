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
using System.Threading;

namespace PollingClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IChannelService proxy;
        private Thread pollingThread; // Thread for polling messages(changed from DispatcherTimer to Thread)
        private volatile bool isPolling;
        private DateTime lastPollTime;
        private readonly string currentUserId;
        public MainWindow(string userId)
        {
            InitializeComponent();

            currentUserId = userId;
            TxtUserId.Text = currentUserId;

            InitializeWcfProxy();
            InitializePollingThread(); //Changed from InitializePollingTimer to InitializePollingThread
            StartPolling(); // Start the polling when the window is initialized
        }

        private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelList.SelectedItem != null)
            {
                TxtChannelName.Text = ChannelList.SelectedItem.ToString();
            }
        }
        private void InitializeWcfProxy()
        {
            ChannelFactory<IChannelService> channelFactory;
            NetTcpBinding tcp = new NetTcpBinding();

            string URL = "net.tcp://localhost:8100/DataService";
            channelFactory = new ChannelFactory<IChannelService>(tcp, URL);
            proxy = channelFactory.CreateChannel();
        }

        private void InitializePollingThread()//Changed from InitializePollingTimer to InitializePollingThread
        {
            lastPollTime = DateTime.MinValue; //Changed from DateTime.Now to DateTime.MinValue to ensure we get all messages since the beginning. Changed from DispatcherTimer to Thread for polling messages
            isPolling = false;
        }

        private void StartPolling() // Changed from StartPollingTimer to StartPolling to start the polling thread
        {
            if (pollingThread != null && pollingThread.IsAlive)
            {
                return;
            }

            isPolling = true;

            pollingThread = new Thread(PollingLoop);
            pollingThread.IsBackground = true;
            pollingThread.Start();
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
                string userId = currentUserId; // Use the currentUserId for joining the channel 

                bool joined = proxy.JoinChannel(currentUserId, channelName);

                if (joined) 
                {
                    MessageBox.Show($"Joined '{channelName}' as {userId}!");
                    lastPollTime = DateTime.MinValue;
                    StartPolling(); //Changed from StartPollingTimer to StartPolling to start the polling thread
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

        private void PollingLoop() // Changed from PollingTimer_Tick to PollingLoop for the polling thread
        {
            ChannelFactory<IChannelService> pollingFactory = null;
            IChannelService pollingProxy = null;

            try
            {
                NetTcpBinding tcp = new NetTcpBinding();
                string URL = "net.tcp://localhost:8100/DataService";

                pollingFactory = new ChannelFactory<IChannelService>(tcp, URL);
                pollingProxy = pollingFactory.CreateChannel();

                while (isPolling)
                {
                    string channelName = "";

                    Dispatcher.Invoke(() =>
                    {
                        channelName = TxtChannelName.Text.Trim();
                    });

                    List<PublicMessage> newMessages =
                        pollingProxy.GetNewPublicMessages(currentUserId, lastPollTime);

                    List<string> members =
                        pollingProxy.GetChannelMembers(channelName);

                    List<ChannelInfo> channels = pollingProxy.GetChannel();

                    Dispatcher.Invoke(() =>
                    {

                        foreach (PublicMessage msg in newMessages)
                        {
                            MessageList.Items.Add($"[{msg.Timestamp:HH:mm:ss}] {msg.SenderId}: {msg.Content}");

                            if (msg.Timestamp > lastPollTime)
                            {
                                lastPollTime = msg.Timestamp;
                            }
                        }

                        MemberList.Items.Clear();

                        foreach (string member in members)
                        {
                            MemberList.Items.Add(member);
                        }

                        string selectedChannel = TxtChannelName.Text.Trim();

                        ChannelList.Items.Clear();

                        foreach (ChannelInfo channel in channels)
                        {
                            ChannelList.Items.Add(channel.Name);
                        }

                        if (!string.IsNullOrWhiteSpace(selectedChannel) && ChannelList.Items.Contains(selectedChannel))
                        {
                            ChannelList.SelectedItem = selectedChannel;
                        }
                    });

                    Thread.Sleep(2000);
                }

                ((IClientChannel)pollingProxy).Close();
                pollingFactory.Close();
            }
            catch (Exception ex)
            {
                if (isPolling)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"Polling error: {ex.Message}",
                            "Polling Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }

                if (pollingProxy != null)
                {
                    ((IClientChannel)pollingProxy).Abort();
                }

                if (pollingFactory != null)
                {
                    pollingFactory.Abort();
                }
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
        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isPolling = true;// Changed from pollTimer.Stop() to isPolling = false to stop the polling thread

                proxy.SignOut(currentUserId);

                SignInWindow signInWindow = new SignInWindow();
                signInWindow.Show();

                this.Close();
            }
            catch (CommunicationException)
            {
                MessageBox.Show(
                    "Unable to communicate with the Chat Server.",
                    "Sign Out Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
