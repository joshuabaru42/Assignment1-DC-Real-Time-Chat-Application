using System.Collections.Generic;
using SharedLibrary;
using System;

namespace ChatServer
{
    internal sealed class ChatState
    {
        private static readonly ChatState _instance = new ChatState();

        public static ChatState Instance => _instance;

        public object StateLock { get; } = new object();

        public HashSet<string> LoggedInUsers { get; } = new HashSet<string>();

        public Dictionary<string, HashSet<string>> Channels { get; } = new Dictionary<string, HashSet<string>>();

        public Dictionary<string, string> UserChannels { get; } = new Dictionary<string, string>();

        public Dictionary<string, List<PublicMessage>> PublicMessages { get; } = new Dictionary<string, List<PublicMessage>>();

        public Dictionary<string, DateTime> UserJoinTimes { get; } = new Dictionary<string, DateTime>();

        public Dictionary<string, IDuplexChatCallback> Callbacks { get; } = new Dictionary<string, IDuplexChatCallback>();
        private ChatState()
        {
        }
    }
}
