using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
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
            EnumerateGroupMembership();
        }

        private void EnumerateGroupMembership()
        {
            List<string> Domains = new List<string>();
            //string Filename = options.CSVPrefix.Equals("") ? "group_membership.csv" : options.CSVPrefix + "_group_membership.csv";
            //StreamWriter writer = new StreamWriter(Path.Combine(options.CSVFolder, Filename));
            String[] props = new String[] { "samaccountname", "distinguishedname", "cn", "dnshostname", "samaccounttype", "primarygroupid", "memberof" };
            int counter = 0;
            if (options.SearchForest)
            {

            }else if (options.Domain != null)
            {
                Domains.Add(Helpers.GetDomain(options.Domain).Name);
            }else
            {
                Domains.Add(Helpers.GetDomain().Name);
            }

            foreach (string DomainName in Domains)
            {
                options.WriteVerbose("Starting Group Membership Enumeration for " + DomainName);

                DirectorySearcher DomainSearcher = Helpers.GetDomainSearcher(DomainName);
                DomainSearcher.Filter = "(memberof=*)";
                
                DomainSearcher.PropertiesToLoad.AddRange(props);

                foreach (SearchResult result in DomainSearcher.FindAll())
                {
                    if (counter % 1000 == 0)
                    {
                        options.WriteVerbose("Group objects enumerated: " + counter.ToString());
                        //writer.Flush();
                    }

                    string DistinguishedName = result.Properties["distinguishedname"][0].ToString();

                    if (DistinguishedName.Contains("ForeignSecurityPrincipals") || DistinguishedName.Contains("S-1-5-21"))
                    {
                        string Translated = Helpers.ConvertSID(result.Properties["cn"][0].ToString());
                        string Final = Helpers.ConvertADName(@"DOMAINA\Administrator", Helpers.ADSTypes.ADS_NAME_TYPE_NT4, Helpers.ADSTypes.ADS_NAME_TYPE_DN);
                        Console.WriteLine(Final);
                    }
                }
            }
        }
    }
}
