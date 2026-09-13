using System.Collections.Generic;
using System.ServiceModel;

namespace SharedLibrary
{
    public interface IDuplexChatCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnChannelsUpdated(List<ChannelInfo> channels);

        [OperationContract(IsOneWay = true)]
        void OnMembersUpdated(string channelName, List<string> members);

        [OperationContract(IsOneWay = true)]
        void OnPublicMessageReceived(PublicMessage message);

        [OperationContract(IsOneWay = true)]
        void OnPrivateMessageReceived(PrivateMessage message);

        [OperationContract(IsOneWay = true)]
        void OnFilesUpdated(string channelName, List<SharedFileInfo> files);
    }
}
