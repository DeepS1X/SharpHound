using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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

            LocalGroupAPI("DOMAINA-WIN7", "Administrators");

            foreach (String DomainName in Domains)
            {
                string DomainSID = Helpers.GetDomainSid(DomainName);

                
            }
        }
        
        private void LocalGroupWinNT(string Target, string Group)
        {
            DirectoryEntry members = new DirectoryEntry(String.Format("WinNT://{0}/{1},group", Target, Group));
            foreach (object member in (System.Collections.IEnumerable) members.Invoke("Members"))
            {
                using (DirectoryEntry m = new DirectoryEntry(member))
                {
                    string path = m.Path.Replace("WinNT://","");
                    if (Regex.Matches(path,"/").Count == 1)
                    {
                        Console.WriteLine(path.Replace("/", "\\"));
                        Console.WriteLine(m.SchemaClassName);
                    }
                }
            }
        }

        [DllImport("NetAPI32.dll", CharSet = CharSet.Unicode)]
        public extern static int NetLocalGroupGetMembers(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            [MarshalAs(UnmanagedType.LPWStr)] string localgroupname,
            int level,
            out IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            IntPtr resume_handle);

        [DllImport("Netapi32.dll", SetLastError = true)]
        static extern int NetApiBufferFree(IntPtr Buffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LOCALGROUP_MEMBERS_INFO_2
        {
            public int lgrmi2_sid;
            public int lgrmi2_sidusage;
            public string lgrmi2_domainandname;
        }

        private void LocalGroupAPI(string Target, string Group)
        {
            int QueryLevel = 2;
            IntPtr PtrInfo = IntPtr.Zero;
            int EntriesRead = 0;
            int TotalRead = 0;
            IntPtr ResumeHandle = IntPtr.Zero;

            int val = NetLocalGroupGetMembers(Target, Group, QueryLevel, out PtrInfo, -1, out EntriesRead, out TotalRead, ResumeHandle);
            if (EntriesRead > 0)
            {
                LOCALGROUP_MEMBERS_INFO_2[] Members = new LOCALGROUP_MEMBERS_INFO_2[EntriesRead];
                IntPtr iter = PtrInfo;
                for (int i = 0; i < EntriesRead; i++)
                {
                    Members[i] = (LOCALGROUP_MEMBERS_INFO_2)Marshal.PtrToStructure(iter, typeof(LOCALGROUP_MEMBERS_INFO_2));
                    iter = (IntPtr)((int)iter + Marshal.SizeOf(typeof(LOCALGROUP_MEMBERS_INFO_2)));
                    string ObjectType = Members[i].lgrmi2_sidusage == 1 ? "User" : "Group";
                    string ObjectName = Members[i].lgrmi2_domainandname;
                }
                NetApiBufferFree(PtrInfo);
            }
        }
    }
}
