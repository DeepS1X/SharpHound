using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class DomainTrustMapping
    {
        private Helpers Helpers;
        private Globals Globals;
        private List<string> SeenDomains;
        private Stack<Domain> Tracker;

        public DomainTrustMapping()
        {
            Helpers = Helpers.Instance;
            Globals = Globals.Instance;
            SeenDomains = new List<string>();
            GetDomainTrusts();
        }

        public void GetDomainTrusts()
        {
            Tracker.Push(Helpers.GetDomain());

            while (Tracker.Count > 0)
            {
                Domain CurrentDomain = Tracker.Pop();
                Globals.WriteVerbose("Enumerating trusts for " + CurrentDomain.Name);
                SeenDomains.Add(CurrentDomain.Name);
                GetNetDomainTrust(CurrentDomain);
            }
        }

        private TrustRelationshipInformationCollection GetNetDomainTrust(Domain Domain)
        {
            TrustRelationshipInformationCollection Trusts =  Domain.GetAllTrustRelationships();
            Console.WriteLine(Trusts);
            return Trusts;
        }

        
    }
}
