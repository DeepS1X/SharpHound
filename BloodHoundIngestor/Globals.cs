using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class Globals
    {
        public Boolean Verbose { get; set; }
        private static Globals instance;

        public static Globals Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Globals();
                }
                return instance;
            }
        }

        public Globals()
        {
            Verbose = false;
        }

        public void WriteVerbose(string Message)
        {
            if (Verbose){
                Console.WriteLine(Message);
            }
        }

    }
}
