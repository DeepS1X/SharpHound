using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class DomainTrust
    {
        enum TrustTypes
        {

        }
        public string SourceDomain { get; set; }
        public string SourceSID { get; set; }
        public string TargetDomain { get; set; }
        public string TargetSID { get; set; }
        
    }
}
