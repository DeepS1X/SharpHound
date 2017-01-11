﻿using System;
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
        private Globals Globals;
        private List<string> SeenDomains;
        private Stack<Domain> Tracker;
        private List<DomainTrust> EnumeratedTrusts;

        public DomainTrustMapping()
        {
            Helpers = Helpers.Instance;
            Globals = Globals.Instance;
            SeenDomains = new List<string>();
            Tracker = new Stack<Domain>();
            EnumeratedTrusts = new List<DomainTrust>();
            GetDomainTrusts();

            using (StreamWriter writer = new StreamWriter("trusts.csv"))
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
                Globals.WriteVerbose("Enumerating trusts for " + CurrentDomain.Name);
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
