using System.Collections.Generic;
using System.ServiceModel;

namespace SharedLibrary
{
    [ServiceContract(CallbackContract = typeof(IDuplexChatCallback))]
    public interface IDuplexChatService
    {
        [OperationContract]
        bool SignIn(string userId, out string reason);

        [OperationContract]
        void SignOut(string userId);

        [OperationContract]
        bool CreateChannel(string channelName);

        [OperationContract]
        bool JoinChannel(string userId, string channelName);

        [OperationContract]
        void LeaveChannel(string userId);

        [OperationContract]
        List<ChannelInfo> GetChannel();

        [OperationContract]
        List<string> GetChannelMembers(string channelName);

        [OperationContract]
        void SendPublicMessage(string userId, string message);

        [OperationContract]
        bool RegisterCallback(string userId);

        [OperationContract]
        void UnregisterCallback(string userId);
    }
}