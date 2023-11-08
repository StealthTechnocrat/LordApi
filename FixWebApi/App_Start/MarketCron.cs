using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;

namespace FixWebApi.App_Start
{
    public class MarketCron : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
          //  await client.GetAsync(requestUri: "http://localhost:49657/Market/casinoResult");    
            await client.GetAsync(requestUri: System.Configuration.ConfigurationManager.AppSettings["CasinoResultCronUrl"]); 
        }
    }
}