using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace ChatServer
{
    [ServiceBehavior(
        ConcurrencyMode = ConcurrencyMode.Multiple,
        UseSynchronizationContext = false)]
    public class ChatService : IChannelService, IDuplexChatService
    {
        public bool SignIn(string userId, out string reason)
        {
            reason = "";

            if (string.IsNullOrWhiteSpace(userId))
            {
                reason = "User ID cannot be empty.";
                return false;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock) //added lock to ensure thread safety when accessing shared state
            {
                if (state.LoggedInUsers.Contains(cleanUserId))
                {
                    reason = "User ID is already in use.";
                    return false;
                }

                state.LoggedInUsers.Add(cleanUserId);

                return true;
            }
        }
        public void SignOut(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock) //added lock to ensure thread safety when accessing shared state
            {
                LeaveChannel(cleanUserId);

                state.UserJoinTimes.Remove(cleanUserId);
                state.LoggedInUsers.Remove(cleanUserId);
            }
        }
        public bool CreateChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            string cleanChannelName = channelName.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock)
            {
                if (state.Channels.ContainsKey(cleanChannelName))
                {
                    return false;
                }

                state.Channels.Add(cleanChannelName, new HashSet<string>());
            }

            NotifyChannelsUpdated();

            return true;
        }

        public bool JoinChannel(string userId, string channelName)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            string cleanUserId = userId.Trim();
            string cleanChannelName = channelName.Trim();

            ChatState state = ChatState.Instance;
            string oldChannelName = null;

            lock (state.StateLock)
            {
                if (!state.LoggedInUsers.Contains(cleanUserId))
                {
                    return false;
                }

                if (!state.Channels.ContainsKey(cleanChannelName))
                {
                    return false;
                }

                if (state.UserChannels.ContainsKey(cleanUserId))
                {
                    oldChannelName = state.UserChannels[cleanUserId];

                    if (state.Channels.ContainsKey(oldChannelName))
                    {
                        state.Channels[oldChannelName].Remove(cleanUserId);
                    }
                }

                state.Channels[cleanChannelName].Add(cleanUserId);
                state.UserChannels[cleanUserId] = cleanChannelName;
                state.UserJoinTimes[cleanUserId] = DateTime.Now;
            }

            if (!string.IsNullOrWhiteSpace(oldChannelName) && oldChannelName != cleanChannelName)
            {
                NotifyMembersUpdated(oldChannelName);
            }

            NotifyMembersUpdated(cleanChannelName);

            return true;
        }

        public void LeaveChannel(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;
            string channelName = null;

            lock (state.StateLock)
            {
                if (!state.UserChannels.ContainsKey(cleanUserId))
                {
                    return;
                }

                channelName = state.UserChannels[cleanUserId];

                if (state.Channels.ContainsKey(channelName))
                {
                    state.Channels[channelName].Remove(cleanUserId);
                }

                state.UserChannels.Remove(cleanUserId);
                state.UserJoinTimes.Remove(cleanUserId);
            }

            NotifyMembersUpdated(channelName);
        }

        public List<ChannelInfo> GetChannel()
        {
            ChatState state = ChatState.Instance;

            lock (state.StateLock) //added lock to ensure thread safety when accessing shared state
            {
                List<ChannelInfo> channelList = new List<ChannelInfo>();

                foreach (KeyValuePair<string, HashSet<string>> channel in state.Channels)
                {
                    ChannelInfo channelInfo = new ChannelInfo();

                    channelInfo.Name = channel.Key;
                    channelInfo.Members = new List<string>(channel.Value);

                    channelList.Add(channelInfo);
                }

                return channelList;
            }
        }

        public void SendPublicMessage(string userId, string message)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string cleanUserId = userId.Trim();
            string cleanMessage = message.Trim();

            ChatState state = ChatState.Instance;
            string channelName;
            PublicMessage publicMessage;

            lock (state.StateLock)
            {
                if (!state.LoggedInUsers.Contains(cleanUserId))
                {
                    return;
                }

                if (!state.UserChannels.ContainsKey(cleanUserId))
                {
                    return;
                }

                channelName = state.UserChannels[cleanUserId];

                if (!state.PublicMessages.ContainsKey(channelName))
                {
                    state.PublicMessages[channelName] = new List<PublicMessage>();
                }

                publicMessage = new PublicMessage();

                publicMessage.SenderId = cleanUserId;
                publicMessage.Content = cleanMessage;
                publicMessage.Timestamp = DateTime.Now;

                state.PublicMessages[channelName].Add(publicMessage);
            }

            NotifyPublicMessage(channelName, publicMessage);
        }

        public List<PublicMessage> GetNewPublicMessages(string userId, DateTime lastCheck)
        {
            List<PublicMessage> newMessages = new List<PublicMessage>();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return newMessages;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock) //added lock to ensure thread safety when accessing shared state
            {
                if (!state.LoggedInUsers.Contains(cleanUserId))
                {
                    return newMessages;
                }

                if (!state.UserChannels.ContainsKey(cleanUserId))
                {
                    return newMessages;
                }

                if (!state.UserJoinTimes.ContainsKey(cleanUserId))
                {
                    return newMessages;
                }

                string channelName = state.UserChannels[cleanUserId];

                if (!state.PublicMessages.ContainsKey(channelName))
                {
                    return newMessages;
                }

                DateTime joinTime = state.UserJoinTimes[cleanUserId];
                DateTime effectiveTime = lastCheck > joinTime ? lastCheck : joinTime;

                foreach (PublicMessage message in state.PublicMessages[channelName])
                {
                    if (message.Timestamp > effectiveTime)
                    {
                        newMessages.Add(message);
                    }
                }

                return newMessages;
            }
        }

        public List<string> GetChannelMembers(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return new List<string>();
            }

            string cleanChannelName = channelName.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock) //added lock to ensure thread safety when accessing shared state
            {
                if (!state.Channels.ContainsKey(cleanChannelName))
                {
                    return new List<string>();
                }

                return new List<string>(state.Channels[cleanChannelName]);
            }
        }
        private void NotifyChannelsUpdated()
        {
            ChatState state = ChatState.Instance;

            List<KeyValuePair<string, IDuplexChatCallback>> callbacks;

            lock (state.StateLock)
            {
                callbacks = new List<KeyValuePair<string, IDuplexChatCallback>>(state.Callbacks);
            }

            List<ChannelInfo> channels = GetChannel();
            List<string> deadUsers = new List<string>();

            foreach (KeyValuePair<string, IDuplexChatCallback> entry in callbacks)
            {
                try
                {
                    ICommunicationObject callbackChannel = entry.Value as ICommunicationObject;

                    if (callbackChannel == null || callbackChannel.State != CommunicationState.Opened)
                    {
                        deadUsers.Add(entry.Key);
                        continue;
                    }

                    entry.Value.OnChannelsUpdated(channels);
                }
                catch (CommunicationException)
                {
                    deadUsers.Add(entry.Key);
                }
                catch (TimeoutException)
                {
                    deadUsers.Add(entry.Key);
                }
            }

            foreach (string userId in deadUsers)
            {
                Console.WriteLine($"Dead callback detected: {userId}");
            }
            CleanupDisconnectedUsers(deadUsers);
        }
        private void NotifyMembersUpdated(string channelName)
        {
            ChatState state = ChatState.Instance;

            List<KeyValuePair<string, IDuplexChatCallback>> callbacks;
            List<string> members;

            lock (state.StateLock)
            {
                callbacks = new List<KeyValuePair<string, IDuplexChatCallback>>(state.Callbacks);

                if (state.Channels.ContainsKey(channelName))
                {
                    members = new List<string>(state.Channels[channelName]);
                }
                else
                {
                    members = new List<string>();
                }
            }

            List<string> deadUsers = new List<string>();

            foreach (KeyValuePair<string, IDuplexChatCallback> entry in callbacks)
            {
                try
                {
                    ICommunicationObject callbackChannel = entry.Value as ICommunicationObject;

                    if (callbackChannel == null || callbackChannel.State != CommunicationState.Opened)
                    {
                        deadUsers.Add(entry.Key);
                        continue;
                    }

                    entry.Value.OnMembersUpdated(channelName, members);
                }
                catch (CommunicationException)
                {
                    deadUsers.Add(entry.Key);
                }
                catch (TimeoutException)
                {
                    deadUsers.Add(entry.Key);
                }
            }

            if (deadUsers.Count > 0)
            {
                foreach (string userId in deadUsers)
                {
                    Console.WriteLine($"Dead callback detected: {userId}");
                }

                CleanupDisconnectedUsers(deadUsers);
            }
        }

        private void NotifyPublicMessage(string channelName, PublicMessage message)
        {
            ChatState state = ChatState.Instance;

            List<KeyValuePair<string, IDuplexChatCallback>> callbacks =
                new List<KeyValuePair<string, IDuplexChatCallback>>();

            lock (state.StateLock)
            {
                foreach (KeyValuePair<string, IDuplexChatCallback> entry in state.Callbacks)
                {
                    string userId = entry.Key;

                    if (state.UserChannels.ContainsKey(userId) &&
                        state.UserChannels[userId] == channelName)
                    {
                        callbacks.Add(entry);
                    }
                }
            }

            List<string> deadUsers = new List<string>();

            foreach (KeyValuePair<string, IDuplexChatCallback> entry in callbacks)
            {
                try
                {
                    ICommunicationObject callbackChannel = entry.Value as ICommunicationObject;

                    if (callbackChannel == null || callbackChannel.State != CommunicationState.Opened)
                    {
                        deadUsers.Add(entry.Key);
                        continue;
                    }

                    entry.Value.OnPublicMessageReceived(message);
                }
                catch (CommunicationException)
                {
                    deadUsers.Add(entry.Key);
                }
                catch (TimeoutException)
                {
                    deadUsers.Add(entry.Key);
                }
            }

            if (deadUsers.Count > 0)
            {
                foreach (string userId in deadUsers)
                {
                    Console.WriteLine($"Dead callback detected: {userId}");
                }

                CleanupDisconnectedUsers(deadUsers);
            }
        }

        private void CleanupDisconnectedUsers(List<string> deadUsers)
        {
            ChatState state = ChatState.Instance;
            HashSet<string> affectedChannels = new HashSet<string>();

            lock (state.StateLock)
            {
                foreach (string userId in deadUsers)
                {
                    state.Callbacks.Remove(userId);

                    if (state.UserChannels.ContainsKey(userId))
                    {
                        string channelName = state.UserChannels[userId];

                        if (state.Channels.ContainsKey(channelName))
                        {
                            state.Channels[channelName].Remove(userId);
                            affectedChannels.Add(channelName);
                        }

                        state.UserChannels.Remove(userId);
                    }

                    state.UserJoinTimes.Remove(userId);
                    state.LoggedInUsers.Remove(userId);
                    Console.WriteLine($"Disconnected user cleaned up: {userId}");
                }
            }

            foreach (string channelName in affectedChannels)
            {
                NotifyMembersUpdated(channelName);
            }
        }
        public bool RegisterCallback(string userId) //  RegisterCallback method is not used in the polling implementation, but it is kept for compatibility with the duplex implementation
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;

            IDuplexChatCallback callback =
                OperationContext.Current.GetCallbackChannel<IDuplexChatCallback>();

            lock (state.StateLock)
            {
                if (!state.LoggedInUsers.Contains(cleanUserId))
                {
                    return false;
                }

                state.Callbacks[cleanUserId] = callback;
            }

            callback.OnChannelsUpdated(GetChannel());
            string channelName;
            lock (state.StateLock)
            {
                channelName = state.UserChannels.ContainsKey(cleanUserId) ? state.UserChannels[cleanUserId] : null;
            }
            if (!string.IsNullOrWhiteSpace(channelName)) NotifyFilesUpdated(channelName);

            return true;
        }

        public void UnregisterCallback(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            string cleanUserId = userId.Trim();

            ChatState state = ChatState.Instance;

            lock (state.StateLock)
            {
                state.Callbacks.Remove(cleanUserId);
            }
        }

        public bool SendPrivateMessage(string userId, string recipientId, string message, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(userId)) { reason = "You must be signed in."; return false; }
            if (string.IsNullOrWhiteSpace(recipientId)) { reason = "Enter a recipient user ID."; return false; }
            if (string.IsNullOrWhiteSpace(message)) { reason = "Private message cannot be empty."; return false; }
            IDuplexChatCallback recipientCallback = null;
            IDuplexChatCallback senderCallback = null;
            PrivateMessage privateMessage;
            ChatState state = ChatState.Instance;
            string sender = userId.Trim();
            string recipient = recipientId.Trim();
            lock (state.StateLock)
            {
                if (!state.LoggedInUsers.Contains(sender)) { reason = "The sender is not signed in."; return false; }
                if (!state.LoggedInUsers.Contains(recipient)) { reason = "That user is not currently signed in."; return false; }
                if (!state.UserChannels.ContainsKey(sender)) { reason = "You must join a channel first."; return false; }
                if (!state.UserChannels.ContainsKey(recipient)) { reason = "That user is not currently in a channel."; return false; }
                if (state.UserChannels[sender] != state.UserChannels[recipient]) { reason = "Private messages can only be sent to members of your current channel."; return false; }
                privateMessage = new PrivateMessage { SenderId = sender, RecipientId = recipient, Content = message.Trim(), Timestamp = DateTime.Now };
                state.Callbacks.TryGetValue(sender, out senderCallback);
                state.Callbacks.TryGetValue(recipient, out recipientCallback);
            }
            NotifyPrivateMessage(recipientCallback, recipient, privateMessage);
            if (state.Callbacks.ContainsKey(sender)) NotifyPrivateMessage(senderCallback, sender, privateMessage);
            return true;
        }

        public bool ShareFile(string userId, string fileName, byte[] content, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(fileName) || content == null) { reason = "File information is incomplete."; return false; }
            if (content.Length > 2 * 1024 * 1024) { reason = "Files must be 2 MB or smaller."; return false; }
            string extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".bmp" && extension != ".txt") { reason = "This file type is not allowed."; return false; }
            ChatState state = ChatState.Instance;
            string user = userId.Trim(); string channel;
            SharedFileInfo info;
            lock (state.StateLock)
            {
                if (!state.UserChannels.TryGetValue(user, out channel)) { reason = "You must join a channel first."; return false; }
                if (!state.SharedFiles.ContainsKey(channel)) state.SharedFiles[channel] = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                if (!state.FileMetadata.ContainsKey(channel)) state.FileMetadata[channel] = new List<SharedFileInfo>();
                state.SharedFiles[channel][fileName] = content;
                info = new SharedFileInfo { FileName = fileName, SharedBy = user, ChannelName = channel, Length = content.Length, Timestamp = DateTime.Now };
                state.FileMetadata[channel].RemoveAll(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                state.FileMetadata[channel].Add(info);
            }
            NotifyFilesUpdated(channel);
            return true;
        }

        public byte[] DownloadFile(string userId, string channelName, string fileName)
        {
            ChatState state = ChatState.Instance;
            lock (state.StateLock)
            {
                string user = userId == null ? null : userId.Trim(); string channel = channelName == null ? null : channelName.Trim();
                if (user == null || !state.UserChannels.ContainsKey(user) || state.UserChannels[user] != channel || !state.SharedFiles.ContainsKey(channel)) return null;
                state.SharedFiles[channel].TryGetValue(fileName, out byte[] content);
                return content;
            }
        }

        private void NotifyPrivateMessage(IDuplexChatCallback callback, string userId, PrivateMessage message)
        {
            if (callback == null) return;
            try { callback.OnPrivateMessageReceived(message); }
            catch (CommunicationException) { CleanupDisconnectedUsers(new List<string> { userId }); }
            catch (TimeoutException) { CleanupDisconnectedUsers(new List<string> { userId }); }
        }

        private void NotifyFilesUpdated(string channelName)
        {
            ChatState state = ChatState.Instance; List<SharedFileInfo> files; List<IDuplexChatCallback> callbacks = new List<IDuplexChatCallback>();
            lock (state.StateLock)
            {
                files = state.FileMetadata.ContainsKey(channelName) ? new List<SharedFileInfo>(state.FileMetadata[channelName]) : new List<SharedFileInfo>();
                foreach (KeyValuePair<string, IDuplexChatCallback> entry in state.Callbacks)
                    if (state.UserChannels.ContainsKey(entry.Key) && state.UserChannels[entry.Key] == channelName) callbacks.Add(entry.Value);
            }
            foreach (IDuplexChatCallback callback in callbacks) try { callback.OnFilesUpdated(channelName, files); } catch (CommunicationException) { }
            catch (TimeoutException) { }
        }
    }
}