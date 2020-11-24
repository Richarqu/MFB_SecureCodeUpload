using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleJobs
{

    public class GoMoneyAcctInqRes
    {
        public string status { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string full_name { get; set; }
        public string sex { get; set; }
        public string title { get; set; }
        public string address { get; set; }
        public string email { get; set; }
        public string branch_code { get; set; }
        public string sterling_cms_id { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string phone_number { get; set; }
        public DateTime birthday { get; set; }
        public string currency_code { get; set; }
    }


}
