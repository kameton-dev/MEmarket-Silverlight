using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace MEmarket.Models
{
    [DataContract]
    public class TrackedApp
    {
        [DataMember]
        public string Id { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Icon { get; set; }
        [DataMember]
        public string InstalledVersion { get; set; }
        [DataMember]
        public string RepositoryUrl { get; set; }
        [DataMember]
        public string AppUrl { get; set; }
        [DataMember]
        public DateTime InstallDate { get; set; }

        public string InstalledVersionText => MEmarket.Resources.AppResources.VersionText + InstalledVersion;
    }

    [DataContract]
    public class UpdateableApp : TrackedApp
    {
        [DataMember]
        public string NewVersion { get; set; }
        public string VersionProgress => InstalledVersion + " ➔ " + NewVersion;
        
        public string StatusText { get; set; } = MEmarket.Resources.AppResources.UpdAvailableText;
        public double ProgressValue { get; set; } = 0;
        public bool IsDownloading { get; set; } = false;
    }
}
