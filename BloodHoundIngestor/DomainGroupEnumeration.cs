using BloodHoundIngestor.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static int count = 0;

        public DomainGroupEnumeration(Options cli)
        {
            Helpers = Helpers.Instance;
            options = cli;
        }

        public void EnumerateGroupMembership()
        {
            Console.WriteLine("Starting Group Member Enumeration");

            ConcurrentQueue<GroupMembershipInfo> outq = new ConcurrentQueue<GroupMembershipInfo>();
            List<string> Domains = new List<string>();
            
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
            Writer w = new Writer();
            Thread write = new Thread(unused => w.Write(outq, options));
            write.Start();

            Stopwatch watch = Stopwatch.StartNew();

            foreach (string DomainName in Domains)
            {
                ConcurrentQueue<GroupEnumObject> inq = new ConcurrentQueue<GroupEnumObject>();
                Console.WriteLine("Starting Group Membership Enumeration for " + DomainName);
                GroupDNMappings = new ConcurrentDictionary<string, string>();
                PrimaryGroups = new ConcurrentDictionary<string, string>();
                string DomainSid = Helpers.GetDomainSid(DomainName);

                DirectorySearcher DomainSearcher = Helpers.GetDomainSearcher(DomainName);
                DomainSearcher.Filter = "(memberof=*)";
                
                DomainSearcher.PropertiesToLoad.AddRange(props);

                List<Thread> threads = new List<Thread>();

                for (int i = 0; i < options.Threads; i++)
                {
                    Enumerator e = new Enumerator();
                    Thread consumer = new Thread(unused => e.ConsumeAndEnumerate(inq, outq, GroupDNMappings, PrimaryGroups, options));
                    consumer.Start();
                    threads.Add(consumer);
                }

                foreach (SearchResult r in DomainSearcher.FindAll())
                {
                    inq.Enqueue(new GroupEnumObject{ result = r, DomainSID = DomainSid, DomainName=DomainName });
                }

                for (int i = 0; i < options.Threads; i++)
                {
                    inq.Enqueue(null);
                }

                foreach (var t in threads)
                {
                    t.Join();
                }

                DomainSearcher.Dispose();
                Console.WriteLine("Done group enumeration for domain: " + DomainName);
            }

            watch.Stop();
            Console.WriteLine("Group Member Enumeration done in " + watch.Elapsed);

            outq.Enqueue(null);
            write.Join();
        }
    }

    public class Writer
    {
        public void Write(Object outq, Object cli)
        {
            int localcount = 0;
            ConcurrentQueue<GroupMembershipInfo> outQueue = (ConcurrentQueue<GroupMembershipInfo>)outq;
            Options o = (Options)cli;

            if (o.URI == null)
            {
                using (StreamWriter writer = new StreamWriter(o.GetFilePath("group_memberships.csv")))
                {
                    writer.WriteLine("GroupName,AccountName,AccountType");
                    while (true)
                    {
                        while (outQueue.IsEmpty)
                        {
                            Thread.Sleep(25);
                        }

                        try
                        {
                            GroupMembershipInfo info;
                            
                            if (outQueue.TryDequeue(out info))
                            {
                                if (info == null)
                                {
                                    writer.Flush();
                                    break;
                                }
                                writer.WriteLine(info.ToCSV());

                                localcount++;
                                if (localcount % 1000 == 0)
                                {
                                    writer.Flush();
                                }
                            }   
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

        }
    }

    public class Enumerator
    {
        public void ConsumeAndEnumerate(Object inq, Object outq, Object dnmap, Object pgmap, Object cli)
        {
            ConcurrentQueue<GroupEnumObject> inQueue = (ConcurrentQueue<GroupEnumObject>)inq;
            ConcurrentQueue<GroupMembershipInfo> outQueue = (ConcurrentQueue<GroupMembershipInfo>)outq;
            ConcurrentDictionary<string, string> GroupDNMappings = (ConcurrentDictionary<string, string>)dnmap;
            ConcurrentDictionary<string, string> PrimaryGroups = (ConcurrentDictionary<string, string>)pgmap;
            Options options = (Options)cli;
            Helpers Helpers = Helpers.Instance;

            while (true)
            {
                SearchResult result;
                string DomainSid;
                string DomainName;
                GroupEnumObject get;
                while (inQueue.IsEmpty)
                {
                    Thread.Sleep(25);
                }

                if (inQueue.TryDequeue(out get))
                {
                    if (get == null)
                    {
                        break;
                    }

                    result = get.result;
                    DomainSid = get.DomainSID;
                    DomainName = get.DomainName;

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
                        continue;
                    }

                    if (AccountName.StartsWith("@"))
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
                            if (Helpers.IsWritingCSV())
                            {
                                GroupMembershipInfo info = new GroupMembershipInfo
                                {
                                    GroupName = PrimaryGroup,
                                    AccountName= AccountName,
                                    ObjectType = ObjectType
                                };
                                outQueue.Enqueue(info);
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
                                GroupDNMappings.TryGetValue(DNString, out GroupName);
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
                                GroupDNMappings.TryAdd(DNString, GroupName);
                            }

                            GroupMembershipInfo info = new GroupMembershipInfo
                            {
                                GroupName = GroupName + "@" + DomainName,
                                AccountName = AccountName,
                                ObjectType = ObjectType
                            };
                            outQueue.Enqueue(info);
                        }
                    }
                    Interlocked.Increment(ref DomainGroupEnumeration.count);
                    if (DomainGroupEnumeration.count % 1000 == 0)
                    {
                        options.WriteVerbose("Groups Enumerated: " + DomainGroupEnumeration.count);
                    }
                }
            }
        }
    }
}
