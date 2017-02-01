using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace BloodHoundIngestor
{
    class DomainGroupEnumeration
    {
        private Helpers Helpers;
        private Options options;
        private Dictionary<string, string> GroupDNMappings;
        private Dictionary<string, string> PrimaryGroups;

        public DomainGroupEnumeration(Options cli)
        {
            Helpers = Helpers.Instance;
            options = cli;
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
                GroupDNMappings = new Dictionary<string, string>();
                PrimaryGroups = new Dictionary<string, string>();
                string DomainSid = Helpers.GetDomainSid(DomainName);

                DirectorySearcher DomainSearcher = Helpers.GetDomainSearcher(DomainName);
                DomainSearcher.Filter = "(memberof=*)";
                
                DomainSearcher.PropertiesToLoad.AddRange(props);

                foreach (SearchResult result in DomainSearcher.FindAll())
                {
                    if (counter % 1000 == 0 && counter > 0)
                    {
                        options.WriteVerbose("Group objects enumerated: " + counter.ToString());
                        //writer.Flush();
                    }
                    string MemberDomain = null;
                    string DistinguishedName = result.Properties["distinguishedname"][0].ToString();
                    string ObjectType = null;

                    if (DistinguishedName.Contains("ForeignSecurityPrincipals") && DistinguishedName.Contains("S-1-5-21"))
                    {
                        try
                        {
                            string Translated = Helpers.ConvertSIDToName(result.Properties["cn"][0].ToString());
                            string Final = Helpers.ConvertADName(Translated, Helpers.ADSTypes.ADS_NAME_TYPE_NT4, Helpers.ADSTypes.ADS_NAME_TYPE_DN);
                            MemberDomain = Final.Split('/')[0];
                        }
                        catch
                        {
                            options.WriteVerbose("Error converting " + DistinguishedName);
                        }

                    } else
                    {
                        MemberDomain = DistinguishedName.Substring(DistinguishedName.IndexOf("DC=")).Replace("DC=", "").Replace(",", ".");
                    }

                    counter++;

                    string SAMAccountType = null;
                    string SAMAccountName = null;
                    string AccountName = null;
                    ResultPropertyValueCollection SAT = result.Properties["samaccounttype"];
                    if (SAT.Count == 0)
                    {
                        options.WriteVerbose("Unknown Account Type");
                        continue;
                    }else
                    {
                        SAMAccountType = SAT[0].ToString();
                    }
                    string[] groups = new string[] { "268435456", "268435457", "536870912", "536870913"};
                    string[] computers = new string[] { "805306369" };
                    string[] users = new string[] { "805306368" };
                    if (groups.Contains(SAMAccountType))
                    {
                        ObjectType = "group";
                        ResultPropertyValueCollection SAN = result.Properties["samaccountname"];
                        if (SAN.Count > 0)
                        {
                            SAMAccountName = SAN[0].ToString();
                        }else
                        {
                            SAMAccountName = Helpers.ConvertSIDToName(result.Properties["cn"][0].ToString());
                            if (SAMAccountName == null)
                            {
                                SAMAccountName = result.Properties["cn"][0].ToString();
                            }
                        }
                        AccountName = String.Format("{0}@{1}", SAMAccountName, MemberDomain);
                    }else if (computers.Contains(SAMAccountType))
                    {
                        ObjectType = "computer";
                        AccountName = result.Properties["dnshostname"][0].ToString();
                    }else if (users.Contains(SAMAccountType))
                    {
                        ObjectType = "user";
                        ResultPropertyValueCollection SAN = result.Properties["samaccountname"];
                        if (SAN.Count > 0)
                        {
                            SAMAccountName = SAN[0].ToString();
                        }
                        else
                        {
                            SAMAccountName = Helpers.ConvertSIDToName(result.Properties["cn"][0].ToString());
                            if (SAMAccountName == null)
                            {
                                SAMAccountName = result.Properties["cn"][0].ToString();
                            }
                        }
                        AccountName = String.Format("{0}@{1}", SAMAccountName, MemberDomain);
                    }

                    if (AccountName.StartsWith("@") || AccountName == null)
                    {
                        continue;
                    }

                    ResultPropertyValueCollection PGI = result.Properties["primarygroupid"];
                    string PrimaryGroup = null;

                    if (PGI.Count > 0)
                    {
                        string PrimaryGroupSID = DomainSid + "-" + PGI[0].ToString();
                        string PrimaryGroupName = null;
                        if (PrimaryGroups.ContainsKey(PrimaryGroupSID))
                        {
                            PrimaryGroupName = PrimaryGroups[PrimaryGroupSID];
                        }else
                        {
                            string raw = Helpers.ConvertSIDToName(PrimaryGroupSID);
                            if (!raw.StartsWith("S-1-"))
                            {
                                PrimaryGroupName = raw.Split('\\').Last();
                                PrimaryGroups[PrimaryGroupSID] = PrimaryGroupName;
                            }
                        }
                        if (PrimaryGroupName != null)
                        {
                            PrimaryGroup = PrimaryGroupName + "@" + DomainName;
                            Console.WriteLine(PrimaryGroup + "," + AccountName + "," + ObjectType);
                        }
                        
                    }

                    ResultPropertyValueCollection MemberOf = result.Properties["memberof"];
                    if (MemberOf.Count > 0)
                    {
                        foreach (var dn in MemberOf)
                        {
                            string DNString = dn.ToString();
                            string GroupDomain = DNString.Substring(DNString.IndexOf("DC=")).Replace("DC=", "").Replace(",", ".");
                            string GroupName = null;
                            if (GroupDNMappings.ContainsKey(DNString))
                            {
                                GroupName = GroupDNMappings[DNString];
                            }else
                            {
                                GroupName = Helpers.ConvertADName(DNString, Helpers.ADSTypes.ADS_NAME_TYPE_DN, Helpers.ADSTypes.ADS_NAME_TYPE_NT4);
                                if (GroupName != null)
                                {
                                    GroupName = GroupName.Split('\\').Last();
                                }else
                                {
                                    GroupName = DNString.Substring(0, DNString.IndexOf(",")).Split('=').Last();
                                }
                                GroupDNMappings[DNString] = GroupName;
                            }
                            Console.WriteLine(GroupName + "@" + GroupDomain + "," + AccountName + "," + ObjectType);
                        }
                    }
                }
            }
        }
    }
}
