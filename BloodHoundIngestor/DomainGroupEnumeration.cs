using BloodHoundIngestor.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BloodHoundIngestor
{
    class DomainGroupEnumeration
    {
        private Helpers Helpers;
        private Options options;
        private ConcurrentDictionary<string, string> GroupDNMappings;
        private ConcurrentDictionary<string, string> PrimaryGroups;
        StreamWriter w = null;
        static int count = 0;

        public DomainGroupEnumeration(Options cli)
        {
            Helpers = Helpers.Instance;
            options = cli;
        }

        private void WriteString(string val)
        {
            lock (w)
            {
                w.WriteLine(val);
            }
        }

        public void EnumerateGroupMembership()
        {
            Console.WriteLine("Starting Group Member Enumeration");
            List<string> Domains = new List<string>();
            
            if (options.URI == null)
            {
                w = new StreamWriter(options.GetFilePath("group_memberships.csv"));
                WriteString("GroupName,AccountName,AccountType");
            }
            String[] props = new String[] { "samaccountname", "distinguishedname", "cn", "dnshostname", "samaccounttype", "primarygroupid", "memberof" };
            if (options.SearchForest)
            {
                Domains = Helpers.GetForestDomains();
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
                GroupDNMappings = new ConcurrentDictionary<string, string>();
                PrimaryGroups = new ConcurrentDictionary<string, string>();
                string DomainSid = Helpers.GetDomainSid(DomainName);

                DirectorySearcher DomainSearcher = Helpers.GetDomainSearcher(DomainName);
                DomainSearcher.Filter = "(memberof=*)";
                
                DomainSearcher.PropertiesToLoad.AddRange(props);

                Parallel.ForEach(DomainSearcher.FindAll().OfType<SearchResult>(), result =>
                {
                    if (count % 1000 == 0 && count > 0)
                    {
                        options.WriteVerbose("Group objects enumerated: " + count.ToString());
                        w.Flush();
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

                    }
                    else
                    {
                        MemberDomain = DistinguishedName.Substring(DistinguishedName.IndexOf("DC=")).Replace("DC=", "").Replace(",", ".");
                    }

                    Interlocked.Increment(ref count);

                    string SAMAccountType = null;
                    string SAMAccountName = null;
                    string AccountName = null;
                    ResultPropertyValueCollection SAT = result.Properties["samaccounttype"];
                    if (SAT.Count == 0)
                    {
                        //options.WriteVerbose("Unknown Account Type");
                        return;
                    }
                    else
                    {
                        SAMAccountType = SAT[0].ToString();
                    }
                    string[] groups = new string[] { "268435456", "268435457", "536870912", "536870913" };
                    string[] computers = new string[] { "805306369" };
                    string[] users = new string[] { "805306368" };
                    if (groups.Contains(SAMAccountType))
                    {
                        ObjectType = "group";
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
                    else if (computers.Contains(SAMAccountType))
                    {
                        ObjectType = "computer";
                        try
                        {
                            AccountName = result.Properties["dnshostname"][0].ToString();
                        }
                        catch
                        {
                            AccountName = null;
                        }

                    }
                    else if (users.Contains(SAMAccountType))
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

                    if (AccountName == null)
                    {
                        return;
                    }

                    if (AccountName.StartsWith("@"))
                    {
                        return;
                    }

                    ResultPropertyValueCollection PGI = result.Properties["primarygroupid"];
                    string PrimaryGroup = null;
                    if (PGI.Count > 0)
                    {
                        string PrimaryGroupSID = DomainSid + "-" + PGI[0].ToString();
                        string PrimaryGroupName = null;
                        if (PrimaryGroups.ContainsKey(PrimaryGroupSID))
                        {
                            PrimaryGroups.TryGetValue(PrimaryGroupSID, out PrimaryGroupName);
                        }
                        else
                        {
                            string raw = Helpers.ConvertSIDToName(PrimaryGroupSID);
                            if (!raw.StartsWith("S-1-"))
                            {
                                PrimaryGroupName = raw.Split('\\').Last();
                                PrimaryGroups.TryAdd(PrimaryGroupSID, PrimaryGroupName);
                            }
                        }
                        if (PrimaryGroupName != null)
                        {
                            PrimaryGroup = PrimaryGroupName + "@" + DomainName;
                            if (w != null)
                            {
                                WriteString(String.Format("{0},{1},{2}", PrimaryGroup, AccountName, ObjectType));
                            }
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
                                GroupDNMappings.TryGetValue(DNString,out GroupName);
                            }
                            else
                            {
                                GroupName = Helpers.ConvertADName(DNString, Helpers.ADSTypes.ADS_NAME_TYPE_DN, Helpers.ADSTypes.ADS_NAME_TYPE_NT4);
                                if (GroupName != null)
                                {
                                    GroupName = GroupName.Split('\\').Last();
                                }
                                else
                                {
                                    GroupName = DNString.Substring(0, DNString.IndexOf(",")).Split('=').Last();
                                }
                                GroupDNMappings.TryAdd(DNString,GroupName);
                            }
                            if (w != null)
                            {
                                WriteString(String.Format("{0}@{1},{2},{3}", GroupName, DomainName, AccountName, ObjectType));
                            }
                        }
                    }
                });
            }

            w.Flush();
            w.Close();
            w.Dispose();
        }
    }

    public class Enumerator
    {
        public void ConsumeAndEnumerate(Object inq, Object outq, Object dnmap, Object pgmap)
        {
            EnumerationQueue<SearchResult> inQueue = (EnumerationQueue<SearchResult>)inq;
            EnumerationQueue<GroupMembershipInfo> outQueue = (EnumerationQueue<GroupMembershipInfo>)outq;
            ConcurrentQueue<string> q;
        }
    }
}
