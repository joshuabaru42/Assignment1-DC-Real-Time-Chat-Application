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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ServiceModel;
using SharedLibrary;
using System.ComponentModel;

namespace DuplexClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IDuplexChatService proxy;
        private DuplexChannelFactory<IDuplexChatService> channelFactory;
        private DuplexCallback callback;
        private string currentUserId;
        private string currentChannelName;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDuplexProxy();
            this.Closing += Window_Closing;
        }

        private void InitializeDuplexProxy()
        {
            callback = new DuplexCallback(this);

            InstanceContext callbackContext = new InstanceContext(callback);

            NetTcpBinding tcpBinding = new NetTcpBinding();

            string URL = "net.tcp://localhost:8200/DuplexService";

            channelFactory = new DuplexChannelFactory<IDuplexChatService>(
                callbackContext,
                tcpBinding,
                URL);

            proxy = channelFactory.CreateChannel();
        }
        public void DisplayPublicMessage(PublicMessage message)
        {
            MessageList.Items.Add($"[{message.Timestamp:HH:mm:ss}] {message.SenderId}: {message.Content}");
        }
        private void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ChannelInfo> channels = proxy.GetChannel();

                MessageBox.Show(
                    $"Connected successfully to the Duplex Service.\nAvailable channels: {channels.Count}",
                    "Duplex Connection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"Unable to communicate with the Duplex Service.\n{ex.Message}",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string userId = TxtUserId.Text.Trim();

            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show(
                    "Please enter a User ID.",
                    "Sign In",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                string reason;

                bool success = proxy.SignIn(userId, out reason);

                if (success)
                {
                    currentUserId = userId;
                    bool callbackRegistered = proxy.RegisterCallback(userId);

                    if (callbackRegistered)
                    {
                        StatusText.Text = $"Signed in as {userId} - Callback registered";
                        TxtUserId.IsReadOnly = true;
                        SignInButton.IsEnabled = false;
                    }
                    else
                    {
                        MessageBox.Show(
                            "Sign in succeeded, but callback registration failed.",
                            "Callback Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show(
                        reason,
                        "Sign In Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"Unable to communicate with the Chat Server.\n{ex.Message}",
                    "Connection Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

        }
        private void RefreshChannelsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ChannelInfo> channels = proxy.GetChannel();

                ChannelList.Items.Clear();

                foreach (ChannelInfo channel in channels)
                {
                    ChannelList.Items.Add(channel.Name);
                }
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"Unable to load channels.\n{ex.Message}",
                    "Channel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void JoinChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ChannelList.SelectedItem == null)
            {
                MessageBox.Show(
                    "Please select a channel first.",
                    "Join Channel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string channelName = ChannelList.SelectedItem.ToString();
            string userId = TxtUserId.Text.Trim();

            string previousChannelName = currentChannelName;
            currentChannelName = channelName;

            MemberList.Items.Clear();
            MessageList.Items.Clear();

            try
            {
                bool joined = proxy.JoinChannel(userId, channelName);

                if (joined)
                {
                    CurrentChannelText.Text = $"Current Channel: {channelName}";
                }
                else
                {
                    currentChannelName = previousChannelName;

                    MessageBox.Show(
                        "Failed to join channel.",
                        "Join Channel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (CommunicationException ex)
            {
                currentChannelName = previousChannelName;

                MessageBox.Show(
                    $"Unable to join channel.\n{ex.Message}",
                    "Join Channel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void UpdateChannels(List<ChannelInfo> channels)
        {
            string selectedChannel = ChannelList.SelectedItem?.ToString();

            ChannelList.Items.Clear();

            foreach (ChannelInfo channel in channels)
            {
                ChannelList.Items.Add(channel.Name);
            }

            if (!string.IsNullOrWhiteSpace(selectedChannel) && ChannelList.Items.Contains(selectedChannel))
            {
                ChannelList.SelectedItem = selectedChannel;
            }
        }

        public void UpdateMembers(string channelName, List<string> members)
        {
            if (channelName != currentChannelName)
            {
                return;
            }

            MemberList.Items.Clear();

            foreach (string member in members)
            {
                MemberList.Items.Add(member);
            }
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(currentUserId))
                {
                    proxy.UnregisterCallback(currentUserId);
                    proxy.SignOut(currentUserId);
                }

                IClientChannel clientChannel = proxy as IClientChannel;

                if (clientChannel != null && clientChannel.State != CommunicationState.Faulted)
                {
                    clientChannel.Close();
                }

                if (channelFactory != null && channelFactory.State != CommunicationState.Faulted)
                {
                    channelFactory.Close();
                }
            }
            catch (CommunicationException)
            {
                (proxy as IClientChannel)?.Abort();
                channelFactory?.Abort();
            }
            catch (TimeoutException)
            {
                (proxy as IClientChannel)?.Abort();
                channelFactory?.Abort();
            }
        }
        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            string message = TxtMessage.Text.Trim();

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                MessageBox.Show(
                    "Please sign in first.",
                    "Send Message",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(currentChannelName))
            {
                MessageBox.Show(
                    "Please join a channel first.",
                    "Send Message",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show(
                    "Please enter a message.",
                    "Send Message",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                proxy.SendPublicMessage(currentUserId, message);
                TxtMessage.Clear();
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"Unable to send message.\n{ex.Message}",
                    "Send Message Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

    }
}