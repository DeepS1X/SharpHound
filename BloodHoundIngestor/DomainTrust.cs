using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class DomainTrust
    {
        public string SourceDomain { get; set; }
        public string SourceSID { get; set; }
        public string TargetDomain { get; set; }
        public string TargetSID { get; set; }
        public string TrustType { get; set; }
        public string TrustDirection { get; set; }
        public bool Transitive { get; set; }

        public DomainTrust()
        {

        }

        public string ToCSV()
        {
            return String.Format("{0},{1},{2},{3},{4}", SourceDomain,TargetDomain,TrustDirection,TrustType, "True");
        }
    }

}
