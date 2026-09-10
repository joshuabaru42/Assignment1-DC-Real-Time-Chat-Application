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

    }
}