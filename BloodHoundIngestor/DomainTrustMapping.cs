using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class DomainTrustMapping
    {
        private Helpers Helpers;
        private List<string> SeenDomains;
        private Stack<Domain> Tracker;
        private List<DomainTrust> EnumeratedTrusts;
        private Options options;

        public DomainTrustMapping(Options cli)
        {
            Helpers = Helpers.Instance;
            SeenDomains = new List<string>();
            Tracker = new Stack<Domain>();
            EnumeratedTrusts = new List<DomainTrust>();
            options = cli;
            GetDomainTrusts();

            string Filename = options.CSVPrefix.Equals("") ? "trusts.csv" : options.CSVPrefix + "_trusts.csv";

            using (StreamWriter writer = new StreamWriter(Path.Combine(options.CSVFolder, Filename)))
            {
                foreach (DomainTrust d in EnumeratedTrusts)
                {
                    writer.WriteLine(d.ToCSV());
                }
            }
        }

        public void GetDomainTrusts()
        {
            Domain CurrentDomain;
            
            CurrentDomain = Helpers.GetDomain();
            
            if (CurrentDomain == null)
            {
                Console.WriteLine("Bad Domain for GetDomainTrusts");
                return;
            }
            Tracker.Push(Helpers.GetDomain());

            while (Tracker.Count > 0)
            {
                CurrentDomain = Tracker.Pop();
                if (SeenDomains.Contains(CurrentDomain.Name))
                {
                    return;
                }

                if (CurrentDomain == null)
                {
                    return;
                }
                options.WriteVerbose("Enumerating trusts for " + CurrentDomain.Name);
                SeenDomains.Add(CurrentDomain.Name);
                TrustRelationshipInformationCollection Trusts =  GetNetDomainTrust(CurrentDomain);
                foreach (TrustRelationshipInformation Trust in Trusts)
                {
                    DomainTrust dt = new DomainTrust();
                    dt.SourceDomain = Trust.SourceName;
                    dt.TargetDomain = Trust.TargetName;
                    dt.TrustType = Trust.TrustType;
                    dt.TrustDirection = Trust.TrustDirection;
                    EnumeratedTrusts.Add(dt);
                    Tracker.Push(Helpers.GetDomain(Trust.TargetName));
                }
            }
        }

        private TrustRelationshipInformationCollection GetNetDomainTrust(Domain Domain)
        {
            TrustRelationshipInformationCollection Trusts =  Domain.GetAllTrustRelationships();
            return Trusts;
        }

    }
}
