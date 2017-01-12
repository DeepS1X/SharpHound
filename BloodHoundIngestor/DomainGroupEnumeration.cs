using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class DomainGroupEnumeration
    {
        private Helpers Helpers;
        private Options options;
        private Dictionary<string, string> GroupDNMappings;

        public DomainGroupEnumeration(Options cli)
        {
            Helpers = Helpers.Instance;
            options = cli;
            GroupDNMappings = new Dictionary<string, string>();
        }

        private void EnumerateGroupMembership()
        {
            List<string> Domains = new List<string>();
            String[] props = new String[] { "samaccountname", "distinguishedname", "cn", "dnshostname", "samaccounttype", "primarygroupid", "memberof" };
            if (options.SearchForest)
            {

            }else if (options.Domain != null)
            {

            }else
            {
                Domains.Add(Helpers.GetDomain().Name);
            }

            foreach (string DomainName in Domains)
            {
                options.WriteVerbose("Starting Group Membership Enumeration for " + DomainName);

                DirectorySearcher DomainSearcher = Helpers.GetDomainSearcher(DomainName);
                DomainSearcher.Filter = "(memberof=)";
                
                DomainSearcher.PropertiesToLoad.AddRange(props);

            }
        }
    }
}
