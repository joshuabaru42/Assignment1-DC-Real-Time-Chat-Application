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
using Microsoft.Win32;

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
        private DateTime lastPrivatePollTime;
        private readonly string currentUserId;
        private readonly Dictionary<string, PrivateChatWindow> privateWindows = new Dictionary<string, PrivateChatWindow>();
        private bool hasSignedOut;
        public MainWindow(string userId)
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

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
            tcp.ReceiveTimeout = TimeSpan.FromMinutes(10);
            tcp.SendTimeout = TimeSpan.FromMinutes(10);
            tcp.OpenTimeout = TimeSpan.FromSeconds(10);
            tcp.CloseTimeout = TimeSpan.FromSeconds(10);
            tcp.MaxReceivedMessageSize = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxArrayLength = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxBytesPerRead = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxStringContentLength = 4 * 1024 * 1024;

            string URL = "net.tcp://localhost:8100/DataService";
            channelFactory = new ChannelFactory<IChannelService>(tcp, URL);
            proxy = channelFactory.CreateChannel();
        }

        private void InitializePollingThread()//Changed from InitializePollingTimer to InitializePollingThread
        {
            lastPollTime = DateTime.MinValue; //Changed from DateTime.Now to DateTime.MinValue to ensure we get all messages since the beginning. Changed from DispatcherTimer to Thread for polling messages
            lastPrivatePollTime = DateTime.MinValue;
            isPolling = false;
        }

        private void OpenPrivateChatButton_Click(object sender, RoutedEventArgs e)
        {
            string recipient = MemberList.SelectedItem == null ? "" : MemberList.SelectedItem.ToString();
            if (string.IsNullOrWhiteSpace(recipient) || recipient == currentUserId)
            {
                MessageBox.Show("Select another signed-in member first.");
                return;
            }
            ShowPrivateWindow(recipient);
        }

        private PrivateChatWindow ShowPrivateWindow(string participant)
        {
            PrivateChatWindow window;
            if (!privateWindows.TryGetValue(participant, out window))
            {
                window = new PrivateChatWindow(participant, message =>
                {
                    string reason;
                    bool sent = proxy.SendPrivateMessage(currentUserId, participant, message, out reason);
                    return sent ? null : reason;
                });
                window.Closed += (sender, args) => privateWindows.Remove(participant);
                privateWindows[participant] = window;
            }
            if (!window.IsVisible) window.Show();
            window.Activate();
            return window;
        }

        private void ShareFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() != true) return;
            byte[] content = System.IO.File.ReadAllBytes(dialog.FileName);
            if (content.Length > 2 * 1024 * 1024)
            {
                MessageBox.Show("Files must be 2 MB or smaller.");
                return;
            }
            string reason;
            if (!proxy.ShareFile(currentUserId, System.IO.Path.GetFileName(dialog.FileName), content, out reason))
                MessageBox.Show(reason);
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

        private void LeaveChannelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                proxy.LeaveChannel(currentUserId);
                lastPollTime = DateTime.MinValue;
                lastPrivatePollTime = DateTime.MinValue;
                MessageList.Items.Clear();
                MemberList.Items.Clear();
                FileList.Items.Clear();
                MessageBox.Show("You have left the channel.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error leaving channel: {ex.Message}");
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

        private void PollingLoop()
        {
            ChannelFactory<IChannelService> pollingFactory = null;
            IChannelService pollingProxy = null;

            try
            {
                NetTcpBinding tcp = CreatePollingBinding();
                string URL = "net.tcp://localhost:8100/DataService";

                pollingFactory = new ChannelFactory<IChannelService>(tcp, URL);
                pollingProxy = pollingFactory.CreateChannel();

                while (isPolling)
                {
                    try
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
                        List<PrivateMessage> privateMessages = pollingProxy.GetNewPrivateMessages(currentUserId, lastPrivatePollTime);
                        List<SharedFileInfo> files = pollingProxy.GetSharedFiles(currentUserId);

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

                        foreach (PrivateMessage privateMessage in privateMessages)
                        {
                            PrivateChatWindow window = ShowPrivateWindow(privateMessage.SenderId == currentUserId ? privateMessage.RecipientId : privateMessage.SenderId);
                            window.AddMessage(privateMessage);
                            if (privateMessage.Timestamp > lastPrivatePollTime) lastPrivatePollTime = privateMessage.Timestamp;
                        }

                        FileList.Items.Clear();
                        foreach (SharedFileInfo file in files) FileList.Items.Add(file.FileName + " (" + file.Length + " bytes)");

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
                    }
                    catch (CommunicationException)
                    {
                        AbortPollingProxy(pollingProxy, pollingFactory);
                        pollingProxy = null;
                        pollingFactory = null;

                        if (!isPolling) break;

                        Thread.Sleep(1000);
                        pollingFactory = new ChannelFactory<IChannelService>(CreatePollingBinding(), URL);
                        pollingProxy = pollingFactory.CreateChannel();
                    }

                    Thread.Sleep(2000);
                }

                ((IClientChannel)pollingProxy).Close();
                pollingFactory.Close();
            }
            catch (Exception ex)
            {
                if (isPolling)
                {
                    Dispatcher.Invoke(() => MessageBox.Show(
                        $"Polling error: {ex.Message}",
                        "Polling Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                }

                if (pollingProxy != null) ((IClientChannel)pollingProxy).Abort();
                if (pollingFactory != null) pollingFactory.Abort();
            }
        }

        private NetTcpBinding CreatePollingBinding()
        {
            NetTcpBinding tcp = new NetTcpBinding();
            tcp.ReceiveTimeout = TimeSpan.FromMinutes(10);
            tcp.SendTimeout = TimeSpan.FromMinutes(10);
            tcp.OpenTimeout = TimeSpan.FromSeconds(10);
            tcp.CloseTimeout = TimeSpan.FromSeconds(10);
            tcp.MaxReceivedMessageSize = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxArrayLength = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxBytesPerRead = 4 * 1024 * 1024;
            tcp.ReaderQuotas.MaxStringContentLength = 4 * 1024 * 1024;
            return tcp;
        }

        private void AbortPollingProxy(IChannelService pollingProxy, ChannelFactory<IChannelService> pollingFactory)
        {
            if (pollingProxy != null) ((IClientChannel)pollingProxy).Abort();
            if (pollingFactory != null) pollingFactory.Abort();
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
                isPolling = false;

                SignOutFromServer();
                hasSignedOut = true;

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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isPolling = false;

            if (!hasSignedOut)
            {
                SignOutFromServer();
                hasSignedOut = true;
            }
        }

        private void SignOutFromServer()
        {
            try
            {
                if (proxy != null)
                {
                    proxy.SignOut(currentUserId);
                    ((IClientChannel)proxy).Close();
                }
            }
            catch (CommunicationException)
            {
                ((IClientChannel)proxy)?.Abort();
            }
            catch (TimeoutException)
            {
                ((IClientChannel)proxy)?.Abort();
            }
        }
    }
}
