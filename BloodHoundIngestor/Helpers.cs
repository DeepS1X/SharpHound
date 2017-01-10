using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class Helpers
    {
        private static Helpers instance;

        private Dictionary<String, Domain> DomainResolveCache;
        private Globals Globals;

        public static Helpers Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Helpers();
                }
                return instance;
            }
        }

        public Helpers()
        {
            DomainResolveCache = new Dictionary<string, Domain>();
            Globals = Globals.Instance;
        }

        public DirectorySearcher GetDomainSearcher(string Domain = null, string SearchBase = null)
        {
            Domain TargetDomain = GetDomain(Domain);
            if (TargetDomain == null)
            {
                Console.WriteLine("Failed to get a domain. Exiting.");
                Environment.Exit(0);
                return null;
            }

            string DomainName = TargetDomain.Name;
            string Server = TargetDomain.PdcRoleOwner.Name;
            string SearchString = "LDAP://";
            SearchString += Server + "/";
            if (SearchBase != null)
            {
                SearchString += SearchBase;
            }else
            {
                string DomainDN = DomainName.Replace(".", ",DC=");
                SearchString += "DC=" + DomainDN;
            }
            
            Globals.WriteVerbose(String.Format("[GetDomainSearcher] Search String: {0}", SearchString));

            DirectorySearcher Searcher = new DirectorySearcher(SearchString);
            Searcher.PageSize = 200;
            Searcher.SearchScope = SearchScope.Subtree;
            Searcher.CacheResults = false;
            Searcher.ReferralChasing = ReferralChasingOption.All;

            return Searcher;
        }

        public Domain GetDomain(string Domain = null)
        {
            Domain DomainObject;
            //Check if we've already resolved this domain before. If we have return the cached object
            string key = Domain == null ? "UNIQUENULLOBJECT" : Domain;
            if (DomainResolveCache.ContainsKey(key))
            {
                return DomainResolveCache[key];
            }

            if (Domain == null)
            {
                try
                {
                    DomainObject = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
                }
                catch
                {
                    Console.WriteLine(String.Format("The specified domain {0} does not exist, could not be contacted, or there isn't an existing trust.", Domain));
                    DomainObject = null;
                }
            }
            else
            {
                try
                {
                    DirectoryContext dc = new DirectoryContext(DirectoryContextType.Domain, Domain);
                    DomainObject = System.DirectoryServices.ActiveDirectory.Domain.GetDomain(dc);
                }
                catch
                {
                    Console.WriteLine("Error retrieving current domain");
                    DomainObject = null;
                }
                
            }
            if (Domain == null)
            {
                DomainResolveCache["UNIQUENULLOBJECT"] = DomainObject;
            }else
            {
                DomainResolveCache[Domain] = DomainObject;
            }
            return DomainObject;
        }
    }
}
