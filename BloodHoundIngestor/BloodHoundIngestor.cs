using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class Options
    {
        public enum CollectionMethod{
            Group,
            ComputerOnly,
            LocalGroup,
            GPOLocalGroup,
            Session,
            LoggedOn,
            Trusts,
            Stealth,
            Default
        }

        [Option('c', "CollectionMethod", DefaultValue = CollectionMethod.Default, HelpText = "Collection Method (Group, LocalGroup, GPOLocalGroup, Session, LoggedOn, ComputerOnly, Trusts, Stealth, Default")]
        public CollectionMethod CMethod { get; set; }

        [Option('v',"Verbose", DefaultValue=false, HelpText="Enables Verbose Output")]
        public bool Verbose { get; set; }

        [Option('t',"Threads", DefaultValue = 10, HelpText ="Set Number of Enumeration Threads")]
        public int Threads { get; set; }

        [Option('f',"CSVFolder", DefaultValue = ".", HelpText ="Set the directory to output CSV Files")]
        public string CSVFolder { get; set; }

        [Option('p',"CSVPrefix", DefaultValue = "", HelpText ="Set the prefix for the CSV files")]
        public string CSVPrefix { get; set; }

        [Option('d', "Domain", MutuallyExclusiveSet ="domain", DefaultValue = null, HelpText = "Domain to enumerate")]
        public string Domain { get; set; }

        [Option('s',"SearchForest", MutuallyExclusiveSet ="domain", DefaultValue =null, HelpText ="Enumerate entire forest")]
        public bool SearchForest { get; set; }

        [Option("URI", DefaultValue = null, HelpText ="URI for Neo4j Rest API")]
        public string URI { get; set; }

        [Option("UserPass", DefaultValue = null, HelpText ="username:password for the Neo4j Rest API")]
        public string UserPass { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public void WriteVerbose(string Message)
        {
            if (Verbose)
            {
                Console.WriteLine(Message);
            }
        }
    }
    class BloodHoundIngestor
    {
        static void Main(string[] args)
        {
            var options = new Options();

            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
                Helpers.CreateInstance(options);
                if (options.Domain != null)
                {
                    if (Helpers.Instance.GetDomain(options.Domain) == null)
                    {
                        Console.WriteLine("Bad Domain Value Provided");
                        Environment.Exit(0);
                    }
                }
                DomainTrustMapping TrustMapper = new DomainTrustMapping(options);
            }
            
            Environment.Exit(0);
        }
    }
}
