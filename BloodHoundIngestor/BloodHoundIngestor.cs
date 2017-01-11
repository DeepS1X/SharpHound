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
        [Option('v',"Verbose", DefaultValue=false, HelpText="Enables Verbose Output")]
        public bool Verbose { get; set; }

        [Option('t',"Threads", DefaultValue = 10, HelpText ="Set Number of Enumeration Threads")]
        public int Threads { get; set; }

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
                DomainTrustMapping TrustMapper = new DomainTrustMapping(options);
            }
            
            Environment.Exit(0);
        }
    }
}
