using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleJobs
{
    public class VoguePayService
    {
        public VogueAcctInqResp Rest_Get(string acct_Num)
        {
            string str1 = string.Empty;
            VogueAcctInqResp _resp = new VogueAcctInqResp();
            try
            {
                string rootUrl = ConfigurationManager.AppSettings["Vogue_Base_Url"];
                string endPoint = ConfigurationManager.AppSettings["Endpoint"];
                string fullUri = $"{rootUrl}{endPoint}{acct_Num}";
                //https://localhost:44319/api/GetVoguePayAcctDetails?input=AB12300001
                using (HttpClient httpClient = new HttpClient())
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                           | SecurityProtocolType.Tls11
                           | SecurityProtocolType.Tls12
                           | SecurityProtocolType.Ssl3;
                    httpClient.BaseAddress = new Uri(rootUrl);
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = httpClient.GetAsync(fullUri).Result.Content.ReadAsStringAsync().Result;

                    var rawResp = JsonConvert.SerializeObject(response);
                    new ErrorLog(rawResp);
                    _resp = JsonConvert.DeserializeObject<VogueAcctInqResp>(response);
                    new ErrorLog(_resp.ToString());
                }
            }
            catch (Exception ex)
            {
                new ErrorLog(ex.Message);
            }
            return _resp;
        }
    }
}
