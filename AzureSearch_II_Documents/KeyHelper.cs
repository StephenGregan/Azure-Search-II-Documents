using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AzureSearch_II_Documents
{
    class KeyHelper
    {
        public static async Task<string> GetAzureFunctionHostKey(HttpClient client)
        {
            string uri = String.Format("https://{0}.scm.azurewebsites.net/api/functions/admin/masterkey", ConfigurationManager.AppSettings["AzureFunctionsSiteName"]);

            byte[] credentials = Encoding.ASCII.GetBytes(String.Format("{0}:{1}", ConfigurationManager.AppSettings["AzureFunctionUsername"], ConfigurationManager.AppSettings["AzureFunctionPassword"]));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));

            HttpResponseMessage response = await client.GetAsync(uri);
            string responseText = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(responseText);
            return json.SelectToken("master key").ToString();
        }
    }
}
