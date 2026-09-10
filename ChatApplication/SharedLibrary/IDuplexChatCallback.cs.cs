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
    }
}
