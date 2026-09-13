using System;
using System.Windows;
using SharedLibrary;

namespace DuplexClient
{
    public partial class PrivateChatWindow : Window
    {
        private readonly Func<string, string> sendMessage;

        public string ParticipantId { get; private set; }

        public PrivateChatWindow(string participantId, Func<string, string> sendMessage)
        {
            InitializeComponent();

            ParticipantId = participantId;
            this.sendMessage = sendMessage;
            Title = "Private Conversation - " + participantId;
        }

        public void AddMessage(PrivateMessage message)
        {
            string direction = message.SenderId == ParticipantId ? message.SenderId : "You";
            ConversationList.Items.Add(string.Format("[{0:HH:mm:ss}] {1}: {2}", message.Timestamp, direction, message.Content));
            ConversationList.ScrollIntoView(ConversationList.Items[ConversationList.Items.Count - 1]);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string content = ReplyTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            try
            {
                string reason = sendMessage(content);
                if (string.IsNullOrWhiteSpace(reason))
                {
                    ReplyTextBox.Clear();
                }
                else
                {
                    MessageBox.Show(reason, "Private Message", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Private Message Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
