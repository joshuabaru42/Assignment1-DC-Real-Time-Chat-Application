using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;


namespace SharedLibrary
{
    [DataContract]
    public class ChannelInfo
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public List<String> Members { get; set; } = new List<string>();
    }
}
