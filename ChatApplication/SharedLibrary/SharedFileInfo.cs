using System;
using System.Runtime.Serialization;

namespace SharedLibrary
{
    [DataContract]
    public class SharedFileInfo
    {
        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public string SharedBy { get; set; }

        [DataMember]
        public string ChannelName { get; set; }

        [DataMember]
        public long Length { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }
    }
}
