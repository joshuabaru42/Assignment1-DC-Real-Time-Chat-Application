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
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

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
        private const long MaximumSharedFileSize = 2L * 1024L * 1024L;
        private readonly Dictionary<string, PrivateChatWindow> privateWindows = new Dictionary<string, PrivateChatWindow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PrivateMessage>> privateHistories = new Dictionary<string, List<PrivateMessage>>(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            InitializeDuplexProxy();
            this.Closing += Window_Closing;
        }

        private void CreateChannelButton_Click(object sender, RoutedEventArgs e)
        {
            string channelName = TxtNewChannelName.Text.Trim();
            if (string.IsNullOrWhiteSpace(channelName)) return;
            if (!proxy.CreateChannel(channelName)) MessageBox.Show("A channel with that name already exists.", "Create Channel");
        }

        private void InitializeDuplexProxy()
        {
            callback = new DuplexCallback(this);

            InstanceContext callbackContext = new InstanceContext(callback);

            NetTcpBinding tcpBinding = new NetTcpBinding();
            ConfigureBinding(tcpBinding);

            string URL = "net.tcp://localhost:8200/DuplexService";

            channelFactory = new DuplexChannelFactory<IDuplexChatService>(
                callbackContext,
                tcpBinding,
                URL);

            proxy = channelFactory.CreateChannel();
        }

        private static void ConfigureBinding(NetTcpBinding binding)
        {
            const int maximumMessageSize = 4 * 1024 * 1024;

            binding.MaxReceivedMessageSize = maximumMessageSize;
            binding.MaxBufferSize = maximumMessageSize;
            binding.ReaderQuotas.MaxArrayLength = maximumMessageSize;
            binding.ReaderQuotas.MaxStringContentLength = maximumMessageSize;
        }

        public void DisplayPrivateMessage(PrivateMessage message)
        {
            string participantId = string.Equals(message.SenderId, currentUserId, StringComparison.OrdinalIgnoreCase)
                ? message.RecipientId
                : message.SenderId;

            if (string.IsNullOrWhiteSpace(participantId))
            {
                return;
            }

            PrivateChatWindow conversation = GetOrCreatePrivateWindow(participantId);

            if (!privateHistories.ContainsKey(participantId))
            {
                privateHistories[participantId] = new List<PrivateMessage>();
            }

            privateHistories[participantId].Add(message);
            conversation.AddMessage(message);
            if (!conversation.IsVisible)
            {
                conversation.Show();
            }
            conversation.Activate();
        }

        public void UpdateFiles(string channelName, List<SharedFileInfo> files)
        {
            if (channelName != currentChannelName) return;
            FileList.Items.Clear();
            foreach (SharedFileInfo file in files) FileList.Items.Add(file);
            FileList.DisplayMemberPath = "FileName";
        }

        private void OpenPrivateConversationButton_Click(object sender, RoutedEventArgs e)
        {
            string participantId = TxtPrivateRecipient.Text.Trim();
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(currentChannelName))
            {
                MessageBox.Show("Sign in and join a channel first.", "Private Conversation");
                return;
            }

            if (string.IsNullOrWhiteSpace(participantId) ||
                string.Equals(participantId, currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Enter another member's user ID.", "Private Conversation");
                return;
            }

            GetOrCreatePrivateWindow(participantId).Show();
        }

        private PrivateChatWindow GetOrCreatePrivateWindow(string participantId)
        {
            PrivateChatWindow conversation;
            if (privateWindows.TryGetValue(participantId, out conversation))
            {
                return conversation;
            }

            conversation = new PrivateChatWindow(participantId, message => SendPrivateMessage(participantId, message));
            conversation.Owner = this;
            conversation.Closed += (sender, args) => privateWindows.Remove(participantId);
            privateWindows[participantId] = conversation;

            List<PrivateMessage> history;
            if (privateHistories.TryGetValue(participantId, out history))
            {
                foreach (PrivateMessage message in history)
                {
                    conversation.AddMessage(message);
                }
            }

            return conversation;
        }

        private string SendPrivateMessage(string participantId, string message)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(currentChannelName))
            {
                return "Sign in and join a channel first.";
            }

            string reason;
            bool sent = proxy.SendPrivateMessage(currentUserId, participantId, message, out reason);
            return sent ? null : reason;
        }

        private void ShareFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Filter = "Allowed files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.txt" };
            if (dialog.ShowDialog() != true) return;

            FileInfo selectedFile = new FileInfo(dialog.FileName);
            if (selectedFile.Length > MaximumSharedFileSize)
            {
                MessageBox.Show("Files must be 2 MB or smaller.", "Share File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string reason;
                bool shared = proxy.ShareFile(currentUserId, System.IO.Path.GetFileName(dialog.FileName), File.ReadAllBytes(dialog.FileName), out reason);
                if (!shared) MessageBox.Show(reason, "Share File");
            }
            catch (CommunicationObjectFaultedException)
            {
                RecoverDuplexChannel();
                MessageBox.Show("The file transfer connection was reset. Please try again.", "Share File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(ex.Message, "Share File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecoverDuplexChannel()
        {
            try
            {
                (proxy as IClientChannel)?.Abort();
                channelFactory?.Abort();
            }
            finally
            {
                InitializeDuplexProxy();
            }

            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                try
                {
                    proxy.RegisterCallback(currentUserId);
                }
                catch (CommunicationException)
                {
                    StatusText.Text = "Connection reset - please sign in again";
                }
            }
        }

        private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SharedFileInfo file = FileList.SelectedItem as SharedFileInfo;
            if (file == null) return;
            byte[] content = proxy.DownloadFile(currentUserId, currentChannelName, file.FileName);
            if (content == null) return;
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), file.FileName);
            File.WriteAllBytes(path, content);
            Process.Start(path);
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

        private void LeaveChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(currentChannelName))
            {
                MessageBox.Show(
                    "You are not currently in a channel.",
                    "Leave Channel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                proxy.LeaveChannel(currentUserId);
                currentChannelName = null;
                CurrentChannelText.Text = "Current Channel: None";
                MemberList.Items.Clear();
                MessageList.Items.Clear();
                FileList.Items.Clear();
                TxtPrivateRecipient.Clear();
            }
            catch (CommunicationException ex)
            {
                MessageBox.Show(
                    $"Unable to leave the channel.\n{ex.Message}",
                    "Leave Channel Error",
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
                foreach (PrivateChatWindow privateWindow in new List<PrivateChatWindow>(privateWindows.Values))
                {
                    privateWindow.Close();
                }

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