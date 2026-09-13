using System;
using System.Runtime.Serialization;

namespace SharedLibrary
{
    [DataContract]
    public class PrivateMessage
    {
        [DataMember]
        public string SenderId { get; set; }

        [DataMember]
        public string RecipientId { get; set; }

        [DataMember]
        public string Content { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }
    }
}
