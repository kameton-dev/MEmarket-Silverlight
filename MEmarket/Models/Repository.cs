using System.Runtime.Serialization;
using System.Collections.Generic;

namespace MEmarket.Models
{
    [DataContract]
    public class Repository
    {
        [DataMember]
        public string Url { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Creator { get; set; }
        [DataMember]
        public string LastUpdated { get; set; }
        [DataMember]
        public List<string> SupportedOs { get; set; }
    }
}
