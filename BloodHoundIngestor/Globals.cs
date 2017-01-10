using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class Globals
    {
        private Boolean Verbose;
        private static Globals instance;

        public static Globals Instance()
        {
            if (instance == null)
            {
                instance = new Globals();
            }
            return instance;
        }

        public Globals()
        {
            Verbose = false;
        }

    }
}
