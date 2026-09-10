using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary
{
    [ServiceContract]
    public interface IChannelService
    {
        [OperationContract]
        bool CreateChannel(string channelName);

        [OperationContract]
        bool JoinChannel(string userId, string channelName);

        [OperationContract]
        void LeaveChannel(string userId);

        [OperationContract]
        List<ChannelInfo> GetChannel();

        [OperationContract]
        void SendPublicMessage(string userId, string message);

        [OperationContract]
        List<PublicMessage> GetNewPublicMessages(string userId, DateTime lastCheck);

        [OperationContract]
        List<string> GetChannelMembers(string channelName);


    }
}
