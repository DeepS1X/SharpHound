using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor
{
    class LocalAdminInfo
    {
        public string server { get; set; }
        public string objectname { get; set; }
        public string objecttype { get; set; }

        public LocalAdminInfo()
        {

        }

        public string ToParam()
        {
            return "";
        }

        public string ToCSV()
        {
            return String.Format("{0},{1},{2}", server, objectname, objecttype);
        }
    }
}
