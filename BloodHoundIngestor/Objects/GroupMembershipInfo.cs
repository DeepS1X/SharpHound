using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BloodHoundIngestor.Objects
{
    class GroupMembershipInfo
    {
        string GroupName { get; set; }
        string AccountName { get; set; }
        string ObjectType { get; set; }

        public GroupMembershipInfo()
        {

        }

        public string ToCSV()
        {
            return String.Format("{0},{1},{2}", GroupName, AccountName, ObjectType);
        }
    }
}
