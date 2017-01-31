using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class LocalAdminEnumeration
    {

        private Helpers Helpers;
        private Options options;
        private Dictionary<string, string> GroupDNMappings;
        private Dictionary<string, string> PrimaryGroups;

        public LocalAdminEnumeration(Options cli)
        {
            Helpers = Helpers.Instance;
            options = cli;
            EnumerateLocalAdmins();
        }

        private void EnumerateLocalAdmins()
        {
            List<string> Domains = new List<string>();
            if (options.SearchForest)
            {

            }
            else if (options.Domain != null)
            {
                Domains.Add(Helpers.GetDomain(options.Domain).Name);
            }
            else
            {
                Domains.Add(Helpers.GetDomain().Name);
            }

            foreach (String DomainName in Domains)
            {

            }
        }
    }
}
