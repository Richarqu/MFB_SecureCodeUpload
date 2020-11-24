using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleJobs
{
    public class VogueAcctInqResp
    {
        public string code { get; set; }
        public string message { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string customerID { get; set; }
        public string accountType { get; set; }
        public string accountName { get; set; }
        public string emailAddress { get; set; }
        public string phoneNumber { get; set; }
        public string homeAddress { get; set; }
    }

}
