using SharedLibrary;
using System.Collections.Generic;
using System.Windows;
using System.ServiceModel;

namespace DuplexClient
{
    [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class DuplexCallback : IDuplexChatCallback
    {
        private readonly MainWindow mainWindow;
        public DuplexCallback(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
        }
        public void OnChannelsUpdated(List<ChannelInfo> channels)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.UpdateChannels(channels);
            });
        }

        public void OnMembersUpdated(string channelName, List<string> members)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.UpdateMembers(channelName, members);
            });
        }

        public void OnPublicMessageReceived(PublicMessage message)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.DisplayPublicMessage(message);
            });
        }

        public void OnPrivateMessageReceived(PrivateMessage message)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.DisplayPrivateMessage(message);
            });
        }

        public void OnFilesUpdated(string channelName, List<SharedFileInfo> files)
        {
            mainWindow.Dispatcher.Invoke(() =>
            {
                mainWindow.UpdateFiles(channelName, files);
            });
        }
    }
}