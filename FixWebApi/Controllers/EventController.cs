using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using AutoMapper;
using FixWebApi.Authentication;
using FixWebApi.Models;
using FixWebApi.Models.DTO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Event")]
    [CustomAuthorization]
    public class EventController : ApiController
    {
        ResponseDTO responseDTO = new ResponseDTO();
        private FixDbContext db = new FixDbContext();
        Runtime run_time = new Runtime();

        [HttpGet]
        [Route("GetCompetitionList")]
        [AllowAnonymous]

        public async Task<IHttpActionResult> getAllCompetitionList(int sportsId)
        {
            try
            {
                var compList = await db.Event.AsNoTracking().Where(e => e.SportsId == sportsId && !e.deleted).GroupBy(x => x.SeriesId).Select(x => x.FirstOrDefault())
                  .Select(e => new
                  {
                      e.SeriesName,
                      e.SeriesId
                  })
                  .ToListAsync();

                if (compList != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = compList;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data Found";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetEventListBySportsId")]
        [AllowAnonymous]

        public async Task<IHttpActionResult> getEventListBySportsId(int sportsId, string seriesId)
        {
            try
            {
                var eventList = await db.Event.AsNoTracking().Where(e => e.SportsId == sportsId && e.SeriesId == seriesId && !e.deleted).ToListAsync();
                if (eventList != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = eventList;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data Found";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetInplayEvents")]
        [AllowAnonymous]

        public async Task<IHttpActionResult> getInplayEvents(int sportsId)
        {
            try
            {
                var eventList = await db.Event.AsNoTracking().Where(e => e.SportsId == sportsId && e.EventTime < DateTime.Now && !e.deleted).ToListAsync();
                if (eventList != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = eventList;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data Found";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        //Get: api/CompetitionList
        [HttpGet]
        [Route("CompetitionList")]
        public async Task<IHttpActionResult> getCompetitionList(int sportsId)
        {
            try
            {
                string compLstUrl = System.Configuration.ConfigurationManager.AppSettings["CompListUrl"].ToString();
                var client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                var responseMessage = await client.GetAsync(requestUri: compLstUrl + "&EventTypeID=" + sportsId);
                if (responseMessage.IsSuccessStatusCode)
                {
                    var data = responseMessage.Content.ReadAsStringAsync().Result;
                    dynamic response = JsonConvert.DeserializeObject<dynamic>(data);
                    responseDTO.Status = true;
                    responseDTO.Result = response;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Response from API";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }



        [HttpGet]
        [Route("EventList")]
        public async Task<IHttpActionResult> getEventList(int sportsId, string seriesId)
        {
            try
            {
                string compLstUrl = System.Configuration.ConfigurationManager.AppSettings["EventListUrl"].ToString();
                var client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                var responseMessage = await client.GetAsync(requestUri: compLstUrl + "&EventTypeID=" + sportsId + "&CompetitionID=" + seriesId);
                if (responseMessage.IsSuccessStatusCode)
                {
                    var data = responseMessage.Content.ReadAsStringAsync().Result;
                    dynamic response = JsonConvert.DeserializeObject<dynamic>(data);
                    responseDTO.Status = true;
                    responseDTO.Result = response;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Response from API";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }


        // POST: api/EventTable
        [HttpPost]
        [Route("Create_Event")]
        public async Task<IHttpActionResult> PostEventModel(EventModel eventModel)
        {
            if (!ModelState.IsValid)
            {
                responseDTO.Status = false;
                responseDTO.Result = ModelState;
            }
            else
            {
                eventModel.EventTime = eventModel.EventTime.AddMinutes(330);
                EventModel checkEventExistance = await eventModelExists(eventModel.EventId);
                if (checkEventExistance != null)
                {
                    checkEventExistance.deleted = false;
                    checkEventExistance.status = false;
                    var markets = await db.Market.Where(x => x.EventId == eventModel.EventId).ToListAsync();
                    var runners = await db.Runner.Where(x => x.EventId == eventModel.EventId).ToListAsync();
                    markets.ForEach(x => { x.deleted = false; x.status = false; });
                    runners.ForEach(x => { x.deleted = false; x.status = false; });
                    int returnValue = await db.SaveChangesAsync();
                    if (returnValue > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Executed Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Execution Failed";
                    }
                }
                else
                {
                    Random random = new Random();
                    double Back1 = Math.Round(random.NextDouble() * (1.5 - 1.0) + 1.0, 2);
                    double Lay1 = Math.Round(random.NextDouble() * (1.5 - 1.0) + 1.0, 2);
                    double Back2 = Math.Round(random.NextDouble() * (2.5 - 2.0) + 2.0, 2);
                    double Lay2 = Math.Round(random.NextDouble() * (2.5 - 2.0) + 2.0, 2);
                    double Back3 = Math.Round(random.NextDouble() * (4.5 - 4.0) + 4.0, 2);
                    double Lay3 = Math.Round(random.NextDouble() * (4.5 - 4.0) + 4.0, 2);
                    string mrktUrl = System.Configuration.ConfigurationManager.AppSettings["MarketListUrl"].ToString();
                    string rnrUrl = System.Configuration.ConfigurationManager.AppSettings["RunnerListUrl"].ToString();
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                    var responseMessage = await client.GetAsync(requestUri: mrktUrl + "&EventID=" + eventModel.EventId + "&sportId=" + eventModel.SportsId);
                    List<MarketModel> marketList = new List<MarketModel>();
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        int userId = Convert.ToInt32(run_time.RunTimeUserId());
                        var marketSetting = await db.UserSetting.AsNoTracking().Where(x => x.SportsId == eventModel.SportsId && x.UserId == userId).FirstOrDefaultAsync();

                        string data = responseMessage.Content.ReadAsStringAsync().Result;
                        List<EventMarketDTO> objList = JsonConvert.DeserializeObject<List<EventMarketDTO>>(data);
                        eventModel.Betdelay = 6;
                        eventModel.Fancydelay = marketSetting.FancyDelay;
                        eventModel.MaxProfit = marketSetting.MaxProfit;
                        eventModel.MaxStake = marketSetting.MaxStake;
                        eventModel.MinStake = marketSetting.MinStake;
                        eventModel.EventFancy = true;
                        eventModel.status = false;
                        eventModel.deleted = false;
                        eventModel.IsLastDigit = false;
                        if (eventModel.SportsId == 1)
                        {
                            eventModel.Back2 = Back2.ToString();
                            eventModel.Lay2 = Lay2.ToString();
                        }
                        else
                        {
                            eventModel.Back2 = "-";
                            eventModel.Lay2 = "-";
                        }
                        eventModel.Back1 = Back1.ToString();
                        eventModel.Lay1 = Lay1.ToString();
                        eventModel.Back3 = Back3.ToString();
                        eventModel.Lay3 = Lay3.ToString();
                        eventModel.createdOn = DateTime.Now;
                        var apiUrlList = db.ThirdPartyApiModel.Where(x => x.SportsId == eventModel.SportsId).Select(x => new { x.DaimondUrl, x.BookMakerUrl, x.BetfairUrl }).FirstOrDefault();
                        MarketModel matchMarket = new MarketModel()
                        {
                            EventId = eventModel.EventId,
                            MarketId = objList.Where(x => x.marketName.Equals("Match Odds")).FirstOrDefault().marketId,
                            marketName = "Match Odds",
                            Betdelay = 6,
                            Fancydelay = marketSetting.FancyDelay,
                            MaxStake = 20000,
                            MinStake = marketSetting.MinStake,
                            status = true,
                            deleted = false,
                            createdOn = DateTime.Now,
                            ApiUrlType = eventModel.SportsId == 4 ? 1 : 2,
                        };

                        marketList.Add(matchMarket);
                        if (eventModel.SportsId == 4)
                        {
                            MarketModel BookMarket = new MarketModel()
                            {
                                EventId = eventModel.EventId,
                                MarketId = objList.Where(x => x.marketName.Equals("Match Odds")).FirstOrDefault().marketId,
                                marketName = "BookMaker",
                                Betdelay = 4,
                                Fancydelay = marketSetting.FancyDelay,
                                MaxStake = 300000,
                                MinStake = marketSetting.MinStake,
                                status = true,
                                deleted = false,
                                createdOn = DateTime.Now,
                                ApiUrlType = 3,
                            };
                            marketList.Add(BookMarket);
                            //var toss = objList.Where(x => x.marketName.Equals("To Win the Toss")).FirstOrDefault();
                            //if (toss != null)
                            //{
                            string tossId = objList.Where(x => x.marketName.Equals("Match Odds")).FirstOrDefault().marketId + "11";
                            MarketModel tossMarket = new MarketModel()
                            {
                                EventId = eventModel.EventId,
                                MarketId = tossId,
                                marketName = "To Win the Toss",
                                Betdelay = 2,
                                Fancydelay = marketSetting.FancyDelay,
                                MaxStake = marketSetting.MaxStake,
                                MinStake = marketSetting.MinStake,
                                status = false,
                                deleted = false,
                                createdOn = DateTime.Now,
                                ApiUrlType = 0,
                            };
                            marketList.Add(tossMarket);
                            //}

                        }

                    }
                    if (marketList.Count > 0)
                    {
                        List<RunnerModel> rnrLst = new List<RunnerModel>();
                        foreach (var item in marketList)
                        {
                            if (item.marketName != "To Win the Toss")
                            {
                                var runnerMessage = await client.GetAsync(requestUri: rnrUrl + "&MarketID=" + item.MarketId);
                                if (runnerMessage.IsSuccessStatusCode)
                                {
                                    string data = runnerMessage.Content.ReadAsStringAsync().Result;
                                    dynamic runObjList = JsonConvert.DeserializeObject<dynamic>(data);
                                    foreach (var rnItem in runObjList)
                                    {
                                        int i = 1;
                                        foreach (var itemRun in rnItem.runners)
                                        {
                                            if (item.marketName == "BookMaker")
                                            {
                                                RunnerModel rnrMdl = new RunnerModel()
                                                {
                                                    EventId = eventModel.EventId,
                                                    MarketId = item.MarketId,
                                                    RunnerId = i,
                                                    RunnerName = itemRun.runnerName,
                                                    MarketName = item.marketName,
                                                    status = false,
                                                    deleted = false,
                                                    createdOn = DateTime.Now,
                                                };
                                                rnrLst.Add(rnrMdl);
                                                i++;
                                            }
                                            else
                                            {
                                                RunnerModel rnrMdl = new RunnerModel()
                                                {
                                                    EventId = eventModel.EventId,
                                                    MarketId = item.MarketId,
                                                    RunnerId = itemRun.selectionId,
                                                    RunnerName = itemRun.runnerName,
                                                    MarketName = item.marketName,
                                                    status = false,
                                                    deleted = false,
                                                    createdOn = DateTime.Now,
                                                };
                                                rnrLst.Add(rnrMdl);
                                            }
                                        }

                                    }
                                    //db.Runner.AddRange(rnrLst);
                                }
                            }
                        }

                        if (eventModel.SportsId == 4)
                        {
                            var runnerList = rnrLst.Where(x => x.MarketName == "Match Odds").ToList();
                            if (runnerList.Count > 0)
                            {
                                foreach (var item in runnerList)
                                {
                                    if (item.RunnerName != "The Draw")
                                    {
                                        int randomNumber = random.Next(10, 100);
                                        RunnerModel rnrMdl = new RunnerModel()
                                        {
                                            EventId = eventModel.EventId,
                                            MarketId = marketList.Where(x => x.marketName == "To Win the Toss").FirstOrDefault().MarketId,
                                            RunnerId = item.RunnerId + randomNumber,
                                            RunnerName = item.RunnerName,
                                            MarketName = "To Win the Toss",
                                            status = false,
                                            deleted = false,
                                            createdOn = DateTime.Now,
                                        };
                                        rnrLst.Add(rnrMdl);
                                    }

                                }
                            }
                        }

                        db.Runner.AddRange(rnrLst);

                        eventModel.Runner1 = rnrLst[0].RunnerName;
                        eventModel.Runner2 = rnrLst[1].RunnerName;
                        eventModel.EventTime = eventModel.EventTime;

                    //    eventModel.EventTime = eventModel.EventTime.AddMinutes(330);
                        db.Event.Add(eventModel);
                        db.Market.AddRange(marketList);
                        int returnValue = await db.SaveChangesAsync();
                        if (returnValue > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = "Executed Successfully";
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Execution Failed";
                        }
                    }

                }

            }
            return Ok(responseDTO);
        }

        [HttpPost]
        [Route("CreateManualEvent")]
        public async Task<IHttpActionResult> CreateManualEvent(ManualEventDTO manualEvent)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = ModelState;
                }
                else
                {
                    int userId = Convert.ToInt32(run_time.RunTimeUserId());
                    var marketSetting = await db.UserSetting.AsNoTracking().Where(x => x.SportsId == manualEvent.SportsId && x.UserId == userId).FirstOrDefaultAsync();

                    Random random = new Random();
                    double Back1 = Math.Round(random.NextDouble() * (1.5 - 1.0) + 1.0, 2);
                    double Lay1 = Math.Round(random.NextDouble() * (1.5 - 1.0) + 1.0, 2);
                    double Back2 = Math.Round(random.NextDouble() * (2.5 - 2.0) + 2.0, 2);
                    double Lay2 = Math.Round(random.NextDouble() * (2.5 - 2.0) + 2.0, 2);
                    double Back3 = Math.Round(random.NextDouble() * (4.5 - 4.0) + 4.0, 2);
                    double Lay3 = Math.Round(random.NextDouble() * (4.5 - 4.0) + 4.0, 2);
                    EventModel eventModel = new EventModel()
                    {
                        SportsId = manualEvent.SportsId,
                        SeriesId = manualEvent.SeriesId,
                        SeriesName = manualEvent.SeriesName,
                        EventId = manualEvent.EventId,
                        EventName = manualEvent.EventName,
                        EventTime = Convert.ToDateTime(manualEvent.EventTime),
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                        Back1 = Back1.ToString(),
                        Lay1 = Lay1.ToString(),
                        Back2 = "-",
                        Lay2 = "-",
                        Back3 = Back3.ToString(),
                        Lay3 = Lay3.ToString(),
                        Betdelay = marketSetting.BetDelay,
                        Fancydelay = marketSetting.FancyDelay,
                        MaxProfit = marketSetting.MaxProfit,
                        MaxStake = marketSetting.MaxStake,
                        MinStake = marketSetting.MinStake,
                        Runner1 = manualEvent.RunnerName1,
                        Runner2 = manualEvent.RunnerName2,
                    };

                    db.Event.Add(eventModel);


                    var apiUrlList = db.ThirdPartyApiModel.Where(x => x.SportsId == eventModel.SportsId).Select(x => new { x.DaimondUrl, x.BookMakerUrl, x.BetfairUrl }).FirstOrDefault();
                    MarketModel matchMarket = new MarketModel()
                    {
                        EventId = eventModel.EventId,
                        MarketId = manualEvent.MarketId,
                        marketName = manualEvent.MarketName,
                        Betdelay = marketSetting.BetDelay,
                        Fancydelay = marketSetting.FancyDelay,
                        MaxStake = marketSetting.MaxStake,
                        MinStake = marketSetting.MinStake,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                        ApiUrlType = 3,
                    };
                    db.Market.Add(matchMarket);
                    MarketModel matchMarket1 = new MarketModel()
                    {
                        EventId = eventModel.EventId,
                        MarketId = manualEvent.MarketId,
                        marketName = "Match Odds",
                        Betdelay = marketSetting.BetDelay,
                        Fancydelay = marketSetting.FancyDelay,
                        MaxStake = marketSetting.MaxStake,
                        MinStake = marketSetting.MinStake,
                        status = true,
                        deleted = false,
                        createdOn = DateTime.Now,
                        ApiUrlType = 1,
                    };
                    db.Market.Add(matchMarket1);

                    RunnerModel runnerModelBook1 = new RunnerModel()
                    {
                        EventId = manualEvent.EventId,
                        MarketId = manualEvent.MarketId,
                        RunnerId = manualEvent.RunnerId1,
                        RunnerName = manualEvent.RunnerName1,
                        Book = 0,
                        MarketName = manualEvent.MarketName,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                    };
                    db.Runner.Add(runnerModelBook1);
                    RunnerModel runnerModelBook2 = new RunnerModel()
                    {
                        EventId = manualEvent.EventId,
                        MarketId = manualEvent.MarketId,
                        RunnerId = manualEvent.RunnerId2,
                        RunnerName = manualEvent.RunnerName2,
                        Book = 0,
                        MarketName = manualEvent.MarketName,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                    };
                    db.Runner.Add(runnerModelBook2);
                    RunnerModel runnerModelMatch1 = new RunnerModel()
                    {
                        EventId = manualEvent.EventId,
                        MarketId = manualEvent.MarketId,
                        RunnerId = manualEvent.RunnerId1,
                        RunnerName = manualEvent.RunnerName1,
                        Book = 0,
                        MarketName = "Match Odds",
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                    };
                    db.Runner.Add(runnerModelMatch1);
                    RunnerModel runnerModelMatch2 = new RunnerModel()
                    {
                        EventId = manualEvent.EventId,
                        MarketId = manualEvent.MarketId,
                        RunnerId = manualEvent.RunnerId2,
                        RunnerName = manualEvent.RunnerName2,
                        Book = 0,
                        MarketName = "Match Odds",
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                    };
                    db.Runner.Add(runnerModelMatch2);
                    int returnValue = await db.SaveChangesAsync();
                    if (returnValue > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Executed Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Execution Failed";
                    }
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }


        // Put: api/EventTable
        [HttpPost]
        [Route("Update_Event")]
        public async Task<IHttpActionResult> PutEventTableModel(string type, int apiType, EventModel evnObj)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                string role = run_time.RunTimeRole();
                var evntObj = await db.Event.Where(x => x.EventId == evnObj.EventId).FirstOrDefaultAsync();
                if (role == "SuperAdmin")
                {
                    switch (type)
                    {
                        case "Fancy":
                            evntObj.EventFancy = evnObj.EventFancy;
                            break;
                        case "Rate":
                            evntObj.status = evnObj.status;
                            break;
                        case "IsFav":
                            evntObj.IsFav = evnObj.IsFav;
                            break;
                        case "Setting":
                            evntObj.ScoreId = evnObj.ScoreId;
                            evntObj.MinStake = evnObj.MinStake;
                            evntObj.MaxStake = evnObj.MaxStake;
                            evntObj.Betdelay = evnObj.Betdelay;
                            evntObj.MaxProfit = evnObj.MaxProfit;
                            var market = await db.Market.Where(x => x.EventId == evnObj.EventId).ToListAsync();
                            foreach (var item in market)
                            {
                                item.MinStake = evnObj.MinStake;
                                item.MaxStake = evnObj.MaxStake;
                                item.Betdelay = evnObj.Betdelay;
                                if (apiType < 3 && apiType > 0)
                                {
                                    if (item.marketName == "Match Odds")
                                    {
                                        item.ApiUrlType = apiType;
                                    }
                                }
                                else if (apiType == 3)
                                {
                                    if (item.marketName == "BookMaker")
                                    {
                                        item.ApiUrlType = apiType;
                                    }
                                }

                            }
                            break;


                    }
                }
                else
                {
                    var signObj = await (from s in db.SignUp
                                         where s.id == id
                                         from e in db.BlockEvent
                                         where e.EventId == evnObj.EventId && (
                 e.UserId == s.ParentId || e.UserId == s.MasterId || e.UserId == s.AdminId || e.UserId == s.SuperAgentId 
                 || e.UserId == s.SuperMasterId || e.UserId == s.SubAdminId)
                                         select new
                                         {
                                             prntRate = e.EventRate,
                                             prntFancy = e.EventFancy,
                                         }).FirstOrDefaultAsync();
                    var blkObj = await db.BlockEvent.Where(x => x.UserId == id && x.EventId == evnObj.EventId).FirstOrDefaultAsync();
                    if (blkObj != null)
                    {
                        switch (type)
                        {
                            case "Fancy":
                                if (signObj != null && signObj.prntFancy)
                                {
                                    if (!evnObj.EventFancy)
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = "Event Fancy Blocked By Parent";
                                    }
                                }
                                else
                                {
                                    blkObj.EventFancy = evnObj.EventFancy;
                                }
                                break;
                            case "Rate":

                                if (signObj != null && signObj.prntRate)
                                {
                                    if (!evnObj.status)
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = "Event Blocked By Parent";
                                    }
                                }
                                else
                                {
                                    blkObj.EventRate = evnObj.status;
                                }
                                break;
                        }
                    }
                    else
                    {
                        BlockEventModel blckObj = new BlockEventModel();
                        blckObj.EventId = evnObj.EventId;
                        blckObj.UserId = id;
                        blckObj.deleted = false;
                        blckObj.createdOn = DateTime.Now;
                        switch (type)
                        {
                            case "Fancy":

                                if (signObj != null && signObj.prntFancy)
                                {
                                    if (!evnObj.EventFancy)
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = "Event Fancy Blocked By Parent";
                                    }
                                }
                                else
                                {
                                    blckObj.EventFancy = evnObj.EventFancy;
                                    blckObj.EventRate = evnObj.status;
                                }
                                break;
                            case "Rate":

                                if (signObj != null && signObj.prntRate)
                                {
                                    if (!evnObj.status)
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = "Event Blocked By Parent";
                                    }
                                }
                                else
                                {
                                    blckObj.EventFancy = evnObj.EventFancy;
                                    blckObj.EventRate = evnObj.status;
                                }
                                break;

                        }

                        db.BlockEvent.Add(blckObj);
                    }
                }
                int returnValue = await db.SaveChangesAsync();
                if (returnValue > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = "Executed Successfully";
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Execution Failed";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }


        // Get: api/GetEvent
        [HttpGet]
        [Route("GetEventList")]
        public async Task<IHttpActionResult> getEventList(int sportsId, string filterType, string value, string marketName)
        {
            try
            {
                if (filterType == "All")
                {
                    if (run_time.RunTimeRole() == "SuperAdmin" || run_time.RunTimeRole() == "Manager")
                    {
                        var eventData = await (from m in db.Market
                                               where m.marketName == marketName
                       && !m.deleted
                                               from e in db.Event
                                               where m.EventId == e.EventId && e.SportsId == sportsId && !e.deleted
                                               select new
                                               {
                                                   e.ScoreId,
                                                   e.EventName,
                                                   e.EventId,
                                                   e.EventTime,
                                                   e.status,
                                                   e.EventFancy,
                                                   e.SportsId,
                                                   e.Betdelay,
                                                   e.MinStake,
                                                   e.MaxStake,
                                                   e.MaxProfit,
                                                   e.IsFav,
                                                   Inplay = e.EventTime < DateTime.Now ? true : false,
                                                   Runners = db.Runner.Where(x => x.EventId == e.EventId && x.MarketName == marketName).ToList(),
                                                   MarketId = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().MarketId,
                                                   MarketName = marketName,
                                                   Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                   ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().ApiUrlType
                                               }).OrderBy(x => x.EventTime).ToListAsync();
                        if (eventData.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = eventData;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                    }
                    else
                    {
                        int userId = Convert.ToInt32(run_time.RunTimeUserId());
                        var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                        if (signObj != null)
                        {
                            var evntBlkObj = await db.BlockEvent.AsNoTracking().Where(x => !x.deleted && (x.UserId == userId || x.UserId == signObj.ParentId || x.UserId == signObj.MasterId || x.UserId == signObj.AdminId || x.UserId == signObj.SuperMasterId || x.UserId == signObj.SuperAgentId || x.UserId == signObj.SubAdminId)).ToListAsync();
                            if (evntBlkObj.Count > 0)
                            {
                                var eventData = await (from m in db.Market
                                                       where m.marketName == "Match Odds"
                               && !m.deleted
                                                       from e in db.Event
                                                       where m.EventId == e.EventId && e.SportsId == sportsId && !e.deleted
                                                       select new
                                                       {
                                                           e.ScoreId,
                                                           e.EventName,
                                                           e.EventId,
                                                           e.EventTime,
                                                           status = db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault() != null ? db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault().EventRate : false,
                                                           EventFancy = db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault() != null ? db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault().EventFancy : false,
                                                           e.SportsId,
                                                           e.Betdelay,
                                                           e.MinStake,
                                                           e.MaxStake,
                                                           e.MaxProfit,
                                                           e.IsFav,
                                                           Inplay = e.EventTime < DateTime.Now ? true : false,
                                                           MarketName = "Match Odds",
                                                           Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                           ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == "Match Odds").FirstOrDefault().ApiUrlType
                                                       }).OrderBy(x => x.EventTime).ToListAsync();
                                if (eventData.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = eventData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "No Data";
                                }
                            }
                            else
                            {
                                var eventData = await (from m in db.Market
                                                       where m.marketName == "Match Odds"
                               && !m.deleted
                                                       from e in db.Event
                                                       where m.EventId == e.EventId && e.SportsId == sportsId && !e.deleted
                                                       select new
                                                       {
                                                           e.ScoreId,
                                                           e.EventName,
                                                           e.EventId,
                                                           e.EventTime,
                                                           e.status,
                                                           e.EventFancy,
                                                           e.SportsId,
                                                           e.Betdelay,
                                                           e.MinStake,
                                                           e.MaxStake,
                                                           e.MaxProfit,
                                                           e.IsFav,
                                                           Inplay = e.EventTime < DateTime.Now ? true : false,
                                                           MarketName = "Match Odds",
                                                           Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                           ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == "Match Odds").FirstOrDefault().ApiUrlType
                                                       }).OrderBy(x => x.EventTime).ToListAsync();
                                if (eventData.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = eventData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "No Data";
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (run_time.RunTimeRole() == "SuperAdmin" || run_time.RunTimeRole() == "Manager")
                    {
                        var eventData = await (from m in db.Market
                                               where m.marketName == marketName
                       && !m.deleted
                                               from e in db.Event
                                               where m.EventId == e.EventId && e.SportsId == sportsId && e.EventName.ToLower().Contains(value.ToLower()) && !e.deleted
                                               select new
                                               {
                                                   e.ScoreId,
                                                   e.EventName,
                                                   e.EventId,
                                                   e.EventTime,
                                                   e.status,
                                                   e.EventFancy,
                                                   e.SportsId,
                                                   e.Betdelay,
                                                   e.MinStake,
                                                   e.MaxStake,
                                                   e.MaxProfit,
                                                   e.IsFav,
                                                   Inplay = e.EventTime < DateTime.Now ? true : false,
                                                   Runners = db.Runner.Where(x => x.EventId == e.EventId && x.MarketName == marketName).ToList(),
                                                   MarketId = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().MarketId,
                                                   MarketName = marketName,
                                                   Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                   ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().ApiUrlType
                                               }).OrderBy(x => x.EventTime).ToListAsync();
                        if (eventData.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = eventData;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                    }
                    else
                    {
                        int userId = Convert.ToInt32(run_time.RunTimeUserId());
                        var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                        if (signObj != null)
                        {
                            var evntBlkObj = await db.BlockEvent.AsNoTracking().Where(x => !x.deleted && (x.UserId == userId || x.UserId == signObj.ParentId || x.UserId == signObj.MasterId || x.UserId == signObj.AdminId || x.UserId == signObj.SuperMasterId || x.UserId == signObj.SuperAgentId || x.UserId == signObj.SubAdminId)).ToListAsync();
                            if (evntBlkObj.Count > 0)
                            {
                                var eventData = await (from m in db.Market
                                                       where m.marketName == "Match Odds"
                               && !m.deleted
                                                       from e in db.Event
                                                       where m.EventId == e.EventId && e.SportsId == sportsId && e.EventName.ToLower().Contains(value.ToLower()) && !e.deleted
                                                       select new
                                                       {
                                                           e.ScoreId,
                                                           e.EventName,
                                                           e.EventId,
                                                           e.EventTime,
                                                           status = db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault() != null ? db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault().EventRate : false,
                                                           EventFancy = db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault() != null ? db.BlockEvent.Where(x => x.EventId == e.EventId).FirstOrDefault().EventFancy : false,
                                                           e.SportsId,
                                                           e.Betdelay,
                                                           e.MinStake,
                                                           e.MaxStake,
                                                           e.MaxProfit,
                                                           e.IsFav,
                                                           Inplay = e.EventTime < DateTime.Now ? true : false,
                                                           MarketName = "Match Odds",
                                                           Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                           ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == "Match Odds").FirstOrDefault().ApiUrlType
                                                       }).OrderBy(x => x.EventTime).ToListAsync();
                                if (eventData.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = eventData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "No Data";
                                }
                            }
                            else
                            {
                                var eventData = await (from m in db.Market
                                                       where m.marketName == "Match Odds"
                               && !m.deleted
                                                       from e in db.Event
                                                       where m.EventId == e.EventId && e.SportsId == sportsId && e.EventName.ToLower().Contains(value.ToLower()) && !e.deleted
                                                       select new
                                                       {
                                                           e.ScoreId,
                                                           e.EventName,
                                                           e.EventId,
                                                           e.EventTime,
                                                           e.status,
                                                           e.EventFancy,
                                                           e.SportsId,
                                                           e.Betdelay,
                                                           e.MinStake,
                                                           e.MaxStake,
                                                           e.MaxProfit,
                                                           e.IsFav,
                                                           Inplay = e.EventTime < DateTime.Now ? true : false,
                                                           MarketName = "Match Odds",
                                                           Markets = db.Market.Where(x => x.EventId == e.EventId).ToList(),
                                                           ApiUrlType = db.Market.Where(x => x.EventId == e.EventId && x.marketName == "Match Odds").FirstOrDefault().ApiUrlType
                                                       }).OrderBy(x => x.EventTime).ToListAsync();
                                if (eventData.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = eventData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "No Data";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        //Get: api/GetEventDetail
        [HttpGet]
        [Route("GetEventDetail")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> getEventDetail(string eventId, string type)
        {
            try
            {
                int id = 1;
                string role = "";
                if (type == "After")
                {
                    id = Convert.ToInt32(run_time.RunTimeUserId());
                    role = run_time.RunTimeRole();
                }
                else
                {
                    id = 0;
                    role = "Client";
                }
                switch (role)
                {
                    case "Client":

                        var eventDetail = await (from e in db.Event
                                                 where e.EventId == eventId
                                                 select new
                                                 {
                                                     e.SeriesName,
                                                     e.ScoreId,
                                                     e.EventName,
                                                     e.EventTime,
                                                     e.EventFancy,
                                                     e.status,
                                                     e.Runner1,
                                                     e.Runner2,
                                                     e.SportsId,
                                                     e.MaxProfit,
                                                     Inplay = e.EventTime < DateTime.Now ? true : false,
                                                     Bets = db.Bet.Where(x => x.EventId == e.EventId && x.UserId == id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                                     {
                                                         b.id,
                                                         b.EventName,
                                                         b.EventId,
                                                         b.MarketId,
                                                         b.RunnerId,
                                                         b.RunnerName,
                                                         b.BetType,
                                                         b.Odds,
                                                         b.Price,
                                                         b.Stake,
                                                         b.MarketName,
                                                         b.createdOn,
                                                         b.Exposure,
                                                         b.Profit,
                                                     }).ToList(),
                                                     markets = db.Market.Where(x => x.EventId == e.EventId).Select(m => new
                                                     {
                                                         m.marketName,
                                                         m.MarketId,
                                                         m.Fancydelay,
                                                         m.MaxStake,
                                                         m.MinStake,
                                                         m.Betdelay,
                                                         m.status,
                                                         m.ApiUrlType,
                                                         runners = db.Runner.Where(x => x.MarketId == m.MarketId && x.MarketName == m.marketName).Select(s => new
                                                         {
                                                             s.RunnerName,
                                                             s.id,
                                                             s.RunnerId,
                                                             s.Book,
                                                             s.MarketId,
                                                             s.MarketName,

                                                         }).ToList(),
                                                     }).ToList(),
                                                     chips = db.Chip.Where(x => !x.deleted && x.UserId == id).Select(c => new
                                                     {
                                                         c.id,
                                                         c.ChipName,
                                                         c.ChipValue,
                                                     }).ToList(),
                                                     apiUrls = db.ThirdPartyApiModel.Where(x => x.SportsId == e.SportsId).Select(x => new
                                                     {
                                                         x.ScoreUrl,
                                                         x.BetfairUrl,
                                                         x.BookMakerUrl,
                                                         x.DaimondUrl,
                                                         x.FancyUrl,
                                                         x.TvUrl
                                                     }).FirstOrDefault()
                                                 }).AsNoTracking().FirstOrDefaultAsync();
                        if (eventDetail != null)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = eventDetail;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    default:

                        switch (role)
                        {
                            case "Agent":
                                var agentBets = await db.Bet.AsNoTracking().Where(x => x.ParentId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = 0,
                                }).ToListAsync();
                                responseDTO.bets = agentBets;
                                break;
                            case "SuperAgent":
                                var superAgentBets = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = 0,
                                }).ToListAsync();
                                responseDTO.bets = superAgentBets;
                                break;
                            case "Master":
                                var masterBets = await db.Bet.AsNoTracking().Where(x => x.MasterId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.ParentId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = s.ParentId == s.MasterId ? 0 : db.SignUp.Where(x => x.id == s.ParentId).Select(x => x.Share).FirstOrDefault(),
                                }).ToListAsync();
                                responseDTO.bets = masterBets;
                                break;
                            case "SuperMaster":
                                var superMasterBets = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.ParentId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = s.ParentId == s.SuperMasterId ? 0 : db.SignUp.Where(x => x.id == s.ParentId).Select(x => x.Share).FirstOrDefault(),
                                }).ToListAsync();
                                responseDTO.bets = superMasterBets;
                                break;
                            case "SubAdmin":
                                var subAdminBets = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.MasterId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = s.SubAdminId == 0 ? 0 : db.SignUp.Where(x => x.id == s.SubAdminId).Select(x => x.Share).FirstOrDefault(),
                                }).ToListAsync();
                                responseDTO.bets = subAdminBets;
                                break;
                            case "Admin":
                                var superBets = await db.Bet.AsNoTracking().Where(x => x.SuperId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.AdminId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = s.AdminId == 0 ? 0 : db.SignUp.Where(x => x.id == s.AdminId).Select(x => x.Share).FirstOrDefault(),
                                }).ToListAsync();
                                responseDTO.bets = superBets;
                                break;
                            case "SuperAdmin":
                                var superAdminBets = await db.Bet.AsNoTracking().Where(x => x.SuperId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
                                {
                                    s.id,
                                    s.UserId,
                                    s.AdminId,
                                    s.UserName,
                                    s.RunnerName,
                                    s.RunnerId,
                                    s.Odds,
                                    s.EventId,
                                    s.MarketId,
                                    s.MarketName,
                                    s.status,
                                    s.Stake,
                                    s.BetStatus,
                                    s.BetType,
                                    s.SportsId,
                                    s.EventName,
                                    s.Price,
                                    s.Profit,
                                    s.Exposure,
                                    s.IpAddress,
                                    s.createdOn,
                                    share = s.SuperId == 0 ? 0 : db.SignUp.Where(x => x.id == s.SuperId).Select(x => x.Share).FirstOrDefault(),
                                }).ToListAsync();
                                responseDTO.bets = superAdminBets;
                                break;
                        }
                        var eventDetails = await (from e in db.Event
                                                  where e.EventId == eventId
                                                  select new
                                                  {
                                                      e.ScoreId,
                                                      e.EventName,
                                                      e.EventTime,
                                                      e.EventFancy,
                                                      e.status,
                                                      e.Runner1,
                                                      e.Runner2,
                                                      Inplay = e.EventTime < DateTime.Now ? true : false,
                                                      e.SportsId,
                                                      share = db.SignUp.Where(x => x.id == id).FirstOrDefault().Share,
                                                      markets = db.Market.Where(x => x.EventId == e.EventId).Select(m => new
                                                      {
                                                          m.marketName,
                                                          m.MarketId,
                                                          m.Fancydelay,
                                                          m.MaxStake,
                                                          m.MinStake,
                                                          m.Betdelay,
                                                          m.status,
                                                          m.ApiUrlType,
                                                          runners = db.Runner.Where(x => x.MarketId == m.MarketId && x.MarketName == m.marketName).Select(s => new
                                                          {
                                                              s.RunnerName,
                                                              s.id,
                                                              s.RunnerId,
                                                              s.Book,
                                                              total = 0,
                                                              s.MarketId,
                                                              s.MarketName,

                                                          }).ToList(),

                                                      }).ToList(),
                                                      apiUrls = db.ThirdPartyApiModel.Where(x => x.SportsId == e.SportsId).Select(x => new
                                                      {
                                                          x.ScoreUrl,
                                                          x.BetfairUrl,
                                                          x.BookMakerUrl,
                                                          x.DaimondUrl,
                                                          x.FancyUrl,
                                                          x.TvUrl
                                                      }).FirstOrDefault(),

                                                  }).AsNoTracking().FirstOrDefaultAsync();
                        if (eventDetails != null)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = eventDetails;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("getCasinoDetail")]
        public async Task<IHttpActionResult> getCasinoDetail(string marketId, string type)
        {
            try
            {
                int id = 1;
                if (type == "After")
                {
                    id = Convert.ToInt32(run_time.RunTimeUserId());
                }
                else
                {
                    id = 0;
                }
                string eventId = System.Configuration.ConfigurationManager.AppSettings["TableEventId"];
                var eventDetail = await (from e in db.Event
                                         where e.EventId == eventId
                                         select new
                                         {
                                             e.EventName,
                                             e.EventTime,
                                             e.EventFancy,
                                             e.status,
                                             Inplay = e.EventTime < DateTime.Now ? true : false,
                                             Bets = db.Bet.Where(x => x.EventId == e.EventId && x.UserId == id && x.MarketId == marketId && !x.deleted && x.BetStatus == "Pending").Select(b => new
                                             {
                                                 b.EventName,
                                                 b.EventId,
                                                 b.MarketId,
                                                 b.RunnerId,
                                                 b.RunnerName,
                                                 b.BetType,
                                                 b.Odds,
                                                 b.Stake,
                                                 b.MarketName,
                                                 b.createdOn,
                                                 b.Exposure,
                                                 b.Profit,
                                             }).ToList(),
                                             markets = db.Market.Where(x => x.EventId == e.EventId && x.MarketId == marketId).Select(m => new
                                             {
                                                 m.marketName,
                                                 m.MarketId,
                                                 m.Fancydelay,
                                                 m.MaxStake,
                                                 m.MinStake,
                                                 m.Betdelay,
                                                 m.status,
                                                 runners = db.Runner.Where(x => x.MarketId == m.MarketId && x.MarketName == m.marketName).Select(s => new
                                                 {
                                                     s.RunnerName,
                                                     s.id,
                                                     s.RunnerId,
                                                     s.Book,
                                                     s.MarketId,
                                                     s.MarketName,

                                                 }).ToList(),
                                             }).ToList(),
                                             apiUrls = db.TableGamesModel.Where(x => x.MarketId == marketId).Select(m => new
                                             {
                                                 m.APIUrl,
                                                 m.ResultAPIUrl,
                                                 m.VedioUrl,

                                             }).FirstOrDefault(),
                                             chips = db.Chip.Where(x => !x.deleted && x.UserId == id).Select(c => new
                                             {
                                                 c.id,
                                                 c.ChipName,
                                                 c.ChipValue,
                                             }).ToList(),
                                         }).AsNoTracking().FirstOrDefaultAsync();
                if (eventDetail != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = eventDetail;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data";
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetDetail")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> getDetail(int sportsId, string reqType, string chckCnd)
        {
            try
            {
                if (chckCnd == "After")
                {
                    string role = run_time.RunTimeRole();
                    int id = Convert.ToInt32(run_time.RunTimeUserId());
                    switch (run_time.RunTimeRole())
                    {
                        case "Client":
                            if (reqType == "All")
                            {
                                var objData = await (from o in db.Offer
                                                     where !o.deleted
                                                     select new
                                                     {
                                                         o.Offer,
                                                         TopEvents = db.Event.Where(x => !x.deleted  && x.EventTime > DateTime.Now).Select(t => new
                                                         {
                                                             t.SportsName,
                                                             t.SeriesName,
                                                             t.EventId,
                                                             t.SportsId,
                                                             t.Runner1,
                                                             t.Runner2,
                                                             t.Back1,
                                                             t.Back3,
                                                             t.EventTime,
                                                         }).OrderBy(x => x.EventTime).ToList(),
                                                         TopInplay = db.Event.Where(x => !x.deleted && x.EventTime < DateTime.Now).Select(t => new
                                                         {
                                                             t.SportsName,
                                                             t.SeriesName,
                                                             t.EventId,
                                                             t.SportsId,
                                                             t.Runner1,
                                                             t.Runner2,
                                                             t.Back1,
                                                             t.Back3,
                                                             t.EventTime,
                                                         }).OrderBy(x => x.EventTime).ToList(),
                                                         Events = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime < DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                         {
                                                             e.SportsId,
                                                             e.SportsName,
                                                             e.EventId,
                                                             e.EventName,
                                                             e.Runner1,
                                                             e.Runner2,
                                                             e.EventTime,
                                                             e.SeriesName,
                                                             e.EventFancy,
                                                             e.IsFav,
                                                             e.status,
                                                             e.Back1,
                                                             e.Back2,
                                                             e.Back3,
                                                             e.Lay1,
                                                             e.Lay2,
                                                             e.Lay3,
                                                         }).OrderBy(x => x.EventTime).ToList(),
                                                         NxtEvnts = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime > DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                         {
                                                             e.SportsId,
                                                             e.SportsName,
                                                             e.EventId,
                                                             e.EventName,
                                                             e.Runner1,
                                                             e.Runner2,
                                                             e.EventTime,
                                                             e.SeriesName,
                                                             e.EventFancy,
                                                             e.IsFav,
                                                             e.status,
                                                             e.Back1,
                                                             e.Back2,
                                                             e.Back3,
                                                             e.Lay1,
                                                             e.Lay2,
                                                             e.Lay3,
                                                         }).OrderBy(x => x.EventTime).ToList(),
                                                         Bets = db.Bet.Where(x => x.UserId == id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                                         {
                                                             b.EventName,
                                                             b.SportsId,
                                                             b.EventId,
                                                             b.MarketId,
                                                             b.RunnerId,
                                                             b.RunnerName,
                                                             b.BetType,
                                                             b.Odds,
                                                             b.Stake,
                                                             b.MarketName,
                                                             b.createdOn,
                                                             b.Exposure,
                                                             b.Profit,
                                                         }).ToList(),
                                                         CricketEventCount = db.Event.Where(x => x.SportsId == 4 && !x.deleted).Count(),
                                                         CricketInplayEventCount = db.Event.Where(x => x.SportsId == 4 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                         TennisEventCount = db.Event.Where(x => x.SportsId == 2 && !x.deleted).Count(),
                                                         TennisInplayEventCount = db.Event.Where(x => x.SportsId == 2 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                         FootballEventCount = db.Event.Where(x => x.SportsId == 1 && !x.deleted).Count(),
                                                         FootbalInplayEventCount = db.Event.Where(x => x.SportsId == 1 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                         News = db.News.Where(x => !x.deleted).Select(n => new
                                                         {
                                                             n.createdOn,
                                                             n.News,
                                                         }).ToList(),
                                                     }).AsNoTracking().FirstOrDefaultAsync();

                                if (objData != null)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = objData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = objData;
                                }
                            }
                            else
                            {
                                var objData = await (from o in db.Offer
                                                     where !o.deleted
                                                     select new
                                                     {
                                                         o.Offer,
                                                         Events = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime < DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                         {
                                                             e.SportsId,
                                                             e.SportsName,
                                                             e.EventId,
                                                             e.EventName,
                                                             e.Runner1,
                                                             e.Runner2,
                                                             e.EventTime,
                                                             e.SeriesName,
                                                             e.EventFancy,
                                                             e.IsFav,
                                                             e.status,
                                                             e.Back1,
                                                             e.Back2,
                                                             e.Back3,
                                                             e.Lay1,
                                                             e.Lay2,
                                                             e.Lay3,

                                                         }).OrderBy(x => x.EventTime).ToList(),
                                                         NxtEvnts = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime > DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                         {
                                                             e.SportsId,
                                                             e.SportsName,
                                                             e.EventId,
                                                             e.EventName,
                                                             e.Runner1,
                                                             e.Runner2,
                                                             e.EventTime,
                                                             e.SeriesName,
                                                             e.EventFancy,
                                                             e.IsFav,
                                                             e.status,
                                                             e.Back1,
                                                             e.Back2,
                                                             e.Back3,
                                                             e.Lay1,
                                                             e.Lay2,
                                                             e.Lay3,
                                                         }).OrderBy(x => x.EventTime).ToList(),


                                                     }).AsNoTracking().FirstOrDefaultAsync();

                                if (objData != null)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = objData;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = objData;
                                }
                            }

                            break;
                        default:

                            switch (run_time.RunTimeRole())
                            {
                                case "Agent":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.ParentId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "SuperAgent":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "Master":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.MasterId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "SuperMaster":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "SubAdmin":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "Admin":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.AdminId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                                case "SuperAdmin":
                                    responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperId == id && x.BetStatus == "Pending" && !x.deleted && x.SportsId == sportsId).ToListAsync();

                                    break;
                            }
                            var agtData = await (from o in db.Offer
                                                 where !o.deleted
                                                 select new
                                                 {
                                                     o.Offer,
                                                     TopEvents = db.Event.Where(x => !x.deleted ).Select(t => new
                                                     {
                                                         t.SportsName,
                                                         t.SeriesName,
                                                         t.SportsId,
                                                         t.EventId,
                                                         t.Runner1,
                                                         t.Runner2,
                                                         t.Back1,
                                                         t.Back3,
                                                         t.EventTime,
                                                         Inplay = t.EventTime < DateTime.Now ? true : false,
                                                     }).OrderBy(x => x.EventTime).ToList(),

                                                     Events = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.SeriesName != "Event").Select(e => new
                                                     {
                                                         e.SportsId,
                                                         e.SportsName,
                                                         e.EventId,
                                                         e.EventName,
                                                         e.EventTime,
                                                         e.SeriesName,
                                                         e.EventFancy,
                                                         e.IsFav,
                                                         e.status,
                                                         e.Back1,
                                                         e.Lay1,
                                                         e.Lay2,
                                                         e.Back2,
                                                         e.Back3,
                                                         e.Lay3,
                                                         Inplay = e.EventTime < DateTime.Now ? true : false,
                                                         bets = role == "Admin" ? db.Bet.FirstOrDefault(x => x.EventId == e.EventId) : role == "SubAdmin" ? db.Bet.FirstOrDefault(x => x.EventId == e.EventId && x.AdminId == id) : role == "Master" ? db.Bet.FirstOrDefault(x => x.EventId == e.EventId && x.MasterId == id) : db.Bet.FirstOrDefault(x => x.EventId == e.EventId && x.ParentId == id),
                                                     }).OrderBy(x => x.EventTime).ToList(),

                                                 }).AsNoTracking().FirstOrDefaultAsync();

                            if (agtData != null)
                            {
                                responseDTO.Status = true;
                                responseDTO.Result = agtData;
                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = agtData;
                            }
                            break;
                    }

                }
                else
                {
                    if (reqType == "All")
                    {
                        var objData = await (from o in db.Offer
                                             where !o.deleted
                                             select new
                                             {
                                                 o.Offer,
                                                 TopEvents = db.Event.Where(x => !x.deleted  && x.EventTime > DateTime.Now).Select(t => new
                                                 {
                                                     t.SportsName,
                                                     t.SeriesName,
                                                     t.EventId,
                                                     t.SportsId,
                                                     t.Runner1,
                                                     t.Runner2,
                                                     t.Back1,
                                                     t.Back3,
                                                     t.EventTime,
                                                 }).OrderBy(x => x.EventTime).ToList(),
                                                 TopInplay = db.Event.Where(x => !x.deleted  && x.EventTime < DateTime.Now).Select(t => new
                                                 {
                                                     t.SportsName,
                                                     t.SeriesName,
                                                     t.EventId,
                                                     t.SportsId,
                                                     t.Runner1,
                                                     t.Runner2,
                                                     t.Back1,
                                                     t.Back3,
                                                     t.EventTime,
                                                 }).OrderBy(x => x.EventTime).ToList(),
                                                 Events = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime < DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                 {
                                                     e.SportsId,
                                                     e.SportsName,
                                                     e.EventId,
                                                     e.EventName,
                                                     e.Runner1,
                                                     e.Runner2,
                                                     e.EventTime,
                                                     e.SeriesName,
                                                     e.EventFancy,
                                                     e.IsFav,
                                                     e.status,
                                                     e.Back1,
                                                     e.Back2,
                                                     e.Back3,
                                                     e.Lay1,
                                                     e.Lay2,
                                                     e.Lay3,
                                                 }).OrderBy(x => x.EventTime).ToList(),
                                                 NxtEvnts = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime > DateTime.Now).Select(e => new
                                                 {
                                                     e.SportsId,
                                                     e.SportsName,
                                                     e.EventId,
                                                     e.EventName,
                                                     e.Runner1,
                                                     e.Runner2,
                                                     e.EventTime,
                                                     e.SeriesName,
                                                     e.EventFancy,
                                                     e.IsFav,
                                                     e.status,
                                                     e.Back1,
                                                     e.Back2,
                                                     e.Back3,
                                                     e.Lay1,
                                                     e.Lay2,
                                                     e.Lay3,
                                                 }).OrderBy(x => x.EventTime).ToList(),
                                                 CricketEventCount = db.Event.Where(x => x.SportsId == 4 && !x.deleted).Count(),
                                                 CricketInplayEventCount = db.Event.Where(x => x.SportsId == 4 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                 TennisEventCount = db.Event.Where(x => x.SportsId == 2 && !x.deleted).Count(),
                                                 TennisInplayEventCount = db.Event.Where(x => x.SportsId == 2 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                 FootballEventCount = db.Event.Where(x => x.SportsId == 1 && !x.deleted).Count(),
                                                 FootbalInplayEventCount = db.Event.Where(x => x.SportsId == 1 && !x.deleted && x.EventTime < DateTime.Now).Count(),
                                                 News = db.News.Where(x => !x.deleted).Select(n => new
                                                 {
                                                     n.createdOn,
                                                     n.News,
                                                 }).ToList(),
                                             }).AsNoTracking().FirstOrDefaultAsync();

                        if (objData != null)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = objData;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = objData;
                        }
                    }
                    else
                    {
                        var objData = await (from o in db.Offer
                                             where !o.deleted
                                             select new
                                             {
                                                 o.Offer,
                                                 Events = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime < DateTime.Now && x.SeriesName != "Event").Select(e => new
                                                 {
                                                     e.SportsId,
                                                     e.SportsName,
                                                     e.EventId,
                                                     e.EventName,
                                                     e.Runner1,
                                                     e.Runner2,
                                                     e.EventTime,
                                                     e.SeriesName,
                                                     e.EventFancy,
                                                     e.IsFav,
                                                     e.status,
                                                     e.Back1,
                                                     e.Back2,
                                                     e.Back3,
                                                     e.Lay1,
                                                     e.Lay2,
                                                     e.Lay3,
                                                 }).OrderBy(x => x.EventTime).ToList(),
                                                 NxtEvnts = db.Event.Where(x => !x.deleted && x.SportsId == sportsId && x.EventTime > DateTime.Now).Select(e => new
                                                 {
                                                     e.SportsId,
                                                     e.SportsName,
                                                     e.EventId,
                                                     e.EventName,
                                                     e.Runner1,
                                                     e.Runner2,
                                                     e.EventTime,
                                                     e.SeriesName,
                                                     e.EventFancy,
                                                     e.IsFav,
                                                     e.status,
                                                     e.Back1,
                                                     e.Back2,
                                                     e.Back3,
                                                     e.Lay1,
                                                     e.Lay2,
                                                     e.Lay3,
                                                 }).OrderBy(x => x.EventTime).ToList(),


                                             }).AsNoTracking().FirstOrDefaultAsync();

                        if (objData != null)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = objData;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = objData;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetCupEvent")]
        public async Task<IHttpActionResult> getCup()
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                var evnt = await (from o in db.Offer
                                  where !o.deleted
                                  select new
                                  {
                                      o.Offer,
                                      Events = db.Event.Where(x => !x.deleted && x.SportsId == 4 && x.SportsName == "CupRate").Select(e => new
                                      {
                                          e.SportsId,
                                          e.SportsName,
                                          e.EventId,
                                          e.EventName,
                                          e.Runner1,
                                          e.Runner2,
                                          e.EventTime,
                                          e.SeriesName,
                                          e.EventFancy,
                                          e.IsFav,
                                          e.status,
                                          e.Back1,
                                          e.Back2,
                                          e.Back3,
                                          e.Lay1,
                                          e.Lay2,
                                          e.Lay3,
                                      }).OrderBy(x => x.EventTime).ToList(),
                                      TopEvents = db.Event.Where(x => !x.deleted ).Select(t => new
                                      {
                                          t.SportsName,
                                          t.SeriesName,
                                          t.SportsId,
                                          t.EventId,
                                          t.Runner1,
                                          t.Runner2,
                                          t.Back1,
                                          t.Back3,
                                          t.EventTime,
                                          Inplay = t.EventTime < DateTime.Now ? true : false,
                                      }).OrderBy(x => x.EventTime).ToList(),
                                  }).AsNoTracking().FirstOrDefaultAsync();

                switch (run_time.RunTimeRole())
                {
                    case "Agent":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.ParentId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "SuperAgent":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "Master":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.MasterId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "SuperMaster":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "SubAdmin":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "Admin":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.AdminId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                    case "SuperAdmin":
                        responseDTO.bets = await db.Bet.AsNoTracking().Where(x => x.SuperId == id && x.BetStatus == "Pending" && !x.deleted && x.MarketName == "Cup-Rate").ToListAsync();
                        break;
                }
                if (evnt != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = evnt;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetFancy")]
        public async Task<IHttpActionResult> GetPendingFancy(string type, int skipRec, int take)
        {
            try
            {
                switch (type)
                {
                    case "Pending":
                        var fancyObj = await (from b in db.Bet
                                              where b.MarketName.Equals("Fancy")
                     && b.BetStatus.Equals("Pending") && !b.deleted
                                              select new
                                              {
                                                  b.EventName,
                                                  b.EventId,
                                                  b.MarketId,
                                                  b.RunnerName,
                                                  b.RunnerId,
                                                  b.Result,
                                              }).GroupBy(l => new { l.EventId, l.MarketId, l.RunnerId }).AsNoTracking().ToListAsync();
                        if (fancyObj.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = fancyObj;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "History":
                        var hisObj = await (from b in db.Bet
                                            where b.MarketName.Equals("Fancy")
                   && b.BetStatus != "Pending" && !b.deleted
                                            select new
                                            {
                                                b.EventName,
                                                b.EventId,
                                                b.MarketId,
                                                b.RunnerName,
                                                b.RunnerId,
                                                b.Result,
                                            }).GroupBy(l => new { l.EventId, l.MarketId, l.RunnerId }).AsNoTracking().Skip(skipRec).Take(take).ToListAsync();
                        if (hisObj.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = hisObj;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetSeriesList")]
        public async Task<IHttpActionResult> GetSrsList(int sportsId)
        {
            try
            {
                var data = await db.Event.AsNoTracking().Where(x => x.SportsId == sportsId && !x.deleted).GroupBy(x => x.SeriesId).Select(x => x.FirstOrDefault()).Select(e => new
                {
                    e.SeriesName,
                    e.SeriesId,
                    Events = db.Event.Where(x => x.SportsId == sportsId && x.SeriesId == e.SeriesId && !x.deleted).Select(ev => new
                    {
                        ev.Runner1,
                        ev.Runner2,
                        ev.EventId,
                        ev.SportsId,
                        ev.Back1,
                        ev.Back3,
                        ev.EventTime,
                    }).ToList(),
                }).ToListAsync();

                if (data.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = data;
                }
                else
                {
                    responseDTO.Status = false;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetEventHist")]
        public async Task<IHttpActionResult> getEvntHistory(int sportsId, string filterType, string value, string marketName, int skipRec, int takeRec)
        {
            try
            {
                if (filterType == "All")
                {
                    var eventData = await (from m in db.Market
                                           where m.marketName == marketName
                   && m.deleted
                                           from e in db.Event
                                           where m.EventId == e.EventId && e.SportsId == sportsId
                                           select new
                                           {
                                               e.EventName,
                                               e.EventId,
                                               e.EventTime,
                                               e.status,
                                               MarketId = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().MarketId,
                                               Result = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().Result,
                                               MarketName = marketName,
                                           }).OrderByDescending(x => x.EventTime).Skip(skipRec).Take(takeRec).ToListAsync();
                    if (eventData.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = eventData;
                        responseDTO.Count = await db.Event.CountAsync(x => x.SportsId == sportsId && x.deleted);
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Data";
                    }
                }
                else
                {
                    var eventData = await (from m in db.Market
                                           where m.marketName == marketName
                   && m.deleted
                                           from e in db.Event
                                           where m.EventId == e.EventId && e.SportsId == sportsId && e.EventName.ToLower().Contains(value.ToLower())
                                           select new
                                           {
                                               e.EventName,
                                               e.EventId,
                                               e.EventTime,
                                               e.status,
                                               MarketId = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().MarketId,
                                               Result = db.Market.Where(x => x.EventId == e.EventId && x.marketName == marketName).FirstOrDefault().Result,
                                               MarketName = marketName,
                                           }).OrderByDescending(x => x.EventTime).Skip(skipRec).Take(takeRec).ToListAsync();
                    if (eventData.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = eventData;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Data";
                    }
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private async Task<EventModel> eventModelExists(string eventId)
        {
            return await db.Event.FirstOrDefaultAsync(e => e.EventId == eventId);
        }


        [HttpGet]
        [Route("AddLastDigitSession")]
        public async Task<IHttpActionResult> addLastDigitSession(string eventId, string marketId, int inning)
        {
            try
            {
                string ing = inning.ToString() == "1" ? "1st" : "2nd";
                int getLastOverValue = await db.LastDigit.Where(x => x.EventId == eventId && x.Inning == inning).MaxAsync(x => x.Over);
                EventLastDigitModel eventLastDigitModel = new EventLastDigitModel()
                {
                    Inning = inning,
                    EventId = eventId,
                    MarketId = marketId,
                    MarketName = "Last Digit Session",
                    Over = getLastOverValue + 1,
                    RunnerName = (getLastOverValue + 1).ToString() + " over run last digit " + ing + " Inng",
                    status = false,
                    deleted = false,
                    createdOn = DateTime.Now,
                    Result = 0,
                    Min = 100,
                    Max = 10000,
                    BackPrice = 1,
                    LayPrice = 1,
                    BackSize = 500,
                    LaySize = 500,
                };
                db.LastDigit.Add(eventLastDigitModel);
                await db.SaveChangesAsync();
                var lastOverList = await db.LastDigit.AsNoTracking().Where(x => x.EventId == eventId && x.Inning == inning).ToListAsync();
                responseDTO.Status = true;
                responseDTO.Result = lastOverList;
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetLastDigitSession")]
        public async Task<IHttpActionResult> getLastDigitSession(string eventId, string type, int ing)
        {
            try
            {
                int id = 1;
                if (type == "After")
                {
                    id = Convert.ToInt32(run_time.RunTimeUserId());
                }
                else
                {
                    id = 0;
                }
                var lastOverList = await db.LastDigit.AsNoTracking().Where(x => x.EventId == eventId && !x.deleted && x.Inning == ing).ToListAsync();
                if (lastOverList.Count > 0)
                {
                    var eventDetail = await (from e in db.Event
                                             where e.EventId == eventId
                                             select new
                                             {
                                                 e.EventName,
                                                 e.EventTime,
                                                 e.EventFancy,
                                                 e.status,
                                                 e.IsLastDigit,
                                                 Inplay = e.EventTime < DateTime.Now ? true : false,
                                                 Bets = db.Bet.Where(x => x.EventId == e.EventId && x.UserId == id && x.BetStatus == "Pending").Select(b => new
                                                 {
                                                     b.EventName,
                                                     b.EventId,
                                                     b.MarketId,
                                                     b.RunnerId,
                                                     b.RunnerName,
                                                     b.BetType,
                                                     b.Odds,
                                                     b.Price,
                                                     b.Stake,
                                                     b.MarketName,
                                                     b.createdOn,
                                                     b.Exposure,
                                                     b.Profit,
                                                 }).ToList(),
                                                 chips = db.Chip.Where(x => !x.deleted && x.UserId == id).Select(c => new
                                                 {
                                                     c.id,
                                                     c.ChipName,
                                                     c.ChipValue,
                                                 }).ToList(),
                                             }).AsNoTracking().FirstOrDefaultAsync();
                    responseDTO.eventDetail = eventDetail;
                    responseDTO.Status = true;
                    responseDTO.Result = lastOverList;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = lastOverList;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetIngLastDigitSession")]
        public async Task<IHttpActionResult> getIngLastDigitSession(string eventId, string type, int ing)
        {
            try
            {
                int id = 1;
                if (type == "After")
                {
                    id = Convert.ToInt32(run_time.RunTimeUserId());
                }
                else
                {
                    id = 0;
                }
                var lastOverList = await db.LastDigit.AsNoTracking().Where(x => x.EventId == eventId && x.Inning == ing && !x.deleted).ToListAsync();
                if (lastOverList.Count > 0)
                {
                    var eventDetail = await (from e in db.Event
                                             where e.EventId == eventId
                                             select new
                                             {
                                                 e.EventName,
                                                 e.EventTime,
                                                 e.EventFancy,
                                                 e.status,
                                                 e.IsLastDigit,
                                                 Inplay = e.EventTime < DateTime.Now ? true : false,
                                             }).AsNoTracking().FirstOrDefaultAsync();
                    responseDTO.eventDetail = eventDetail;
                    responseDTO.Status = true;
                    responseDTO.Result = lastOverList;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = lastOverList;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("UpdateLastDigitSession")]
        public async Task<IHttpActionResult> updateLastDigitSession(string marketId, string type, double backPrice, double layPrice, double backSize, double laySize, string eventId, int sessionId, double min, double max)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                string role = run_time.RunTimeRole();
                var evntObj = await db.Event.Where(x => x.EventId == eventId).FirstOrDefaultAsync();
                if (role == "SuperAdmin")
                {
                    if (type == "Event")
                    {
                        evntObj.IsLastDigit = evntObj.IsLastDigit ? false : true;
                    }
                    else
                    {
                        var sessionObj = await db.LastDigit.Where(x => x.EventId == eventId && x.id == sessionId).FirstOrDefaultAsync();
                        if (sessionObj != null)
                        {
                            sessionObj.status = sessionObj.status ? false : true;
                            sessionObj.Min = min;
                            sessionObj.Max = max;
                            sessionObj.BackSize = backSize;
                            sessionObj.LaySize = laySize;
                            sessionObj.BackPrice = backPrice;
                            sessionObj.LayPrice = layPrice;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Error";
                        }
                    }
                }
                else
                {
                    var blkMrkt = await db.BlockMarket.Where(x => x.MarketId == marketId && x.EventId == eventId && x.UserId == id).FirstOrDefaultAsync();
                    if (blkMrkt != null)
                    {
                        blkMrkt.status = blkMrkt.status ? false : true;
                    }
                    else
                    {
                        BlockMarketModel blckMdl = new BlockMarketModel()
                        {
                            EventId = eventId,
                            MarketId = marketId,
                            UserId = id,
                            status = true,
                        };
                        db.BlockMarket.Add(blckMdl);
                    }
                }
                int returnValue = await db.SaveChangesAsync();
                if (returnValue > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = "Executed Successfully";
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Execution Failed";
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpPost]
        [Route("AddApiUrls")]
        public async Task<IHttpActionResult> AddApiUrls(ThirdPartyApiModel thirdPartyApiModel)
        {
            try
            {
                var Obj = await db.ThirdPartyApiModel.Where(x => x.SportsId == thirdPartyApiModel.SportsId).FirstOrDefaultAsync();
                if (Obj != null)
                {
                    Obj.SportsId = thirdPartyApiModel.SportsId;
                    Obj.BetfairUrl = thirdPartyApiModel.BetfairUrl;
                    Obj.DaimondUrl = thirdPartyApiModel.DaimondUrl;
                    Obj.FancyUrl = thirdPartyApiModel.FancyUrl;
                    Obj.BookMakerUrl = thirdPartyApiModel.BookMakerUrl;
                    Obj.ScoreUrl = thirdPartyApiModel.ScoreUrl;
                    Obj.TvUrl = thirdPartyApiModel.TvUrl;
                }
                else
                {
                    ThirdPartyApiModel thirdPartyApiModel1 = new ThirdPartyApiModel()
                    {
                        SportsId = thirdPartyApiModel.SportsId,
                        BetfairUrl = thirdPartyApiModel.BetfairUrl,
                        DaimondUrl = thirdPartyApiModel.DaimondUrl,
                        FancyUrl = thirdPartyApiModel.FancyUrl,
                        BookMakerUrl = thirdPartyApiModel.BookMakerUrl,
                        ScoreUrl = thirdPartyApiModel.ScoreUrl,
                        TvUrl = thirdPartyApiModel.TvUrl,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now,
                    };
                    db.ThirdPartyApiModel.Add(thirdPartyApiModel1);

                }
                await db.SaveChangesAsync();
                responseDTO.Status = true;
                responseDTO.Result = "Done";
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetApiUrls")]
        public async Task<IHttpActionResult> GetApiUrls()
        {
            try
            {
                var Obj = await db.ThirdPartyApiModel.AsNoTracking().Where(x => x.deleted == false).ToListAsync();
                if (Obj != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = Obj;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = Obj;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("DeleteEvents")]
        public async Task<IHttpActionResult> DeleteEvents()
        {
            try
            {
                DateTime currentDate = DateTime.Now;
                var eventData = await db.Event.Where(x => !x.deleted && x.SportsId != 15 && x.SportsId != 4).ToListAsync();

                if (eventData.Count > 0)
                {
                    foreach (var item in eventData)
                    {
                        var checkBets = await db.Bet.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == item.EventId);
                        if (checkBets == null)
                        {
                            TimeSpan timeDifference = currentDate - item.EventTime;
                            double minutesDifference = timeDifference.TotalMinutes;
                            if (minutesDifference > 90)
                            {
                                item.status = true;
                                item.deleted = true;
                                var markets = await db.Market.Where(x => x.EventId == item.EventId).ToListAsync();
                                var runner = await db.Runner.Where(x => x.EventId == item.EventId).ToListAsync();
                                markets.ForEach(x => { x.deleted = true; x.status = true; });
                                runner.ForEach(x => { x.deleted = true; x.status = true; });

                            }
                        }

                    }
                    await db.SaveChangesAsync();
                }
                responseDTO.Status = true;
                responseDTO.Result = "Success";
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("DeleteEvent")]
        public async Task<IHttpActionResult> DeleteEvent(string eventId)
        {
            try
            {
                var eventData = await db.Event.Where(x => x.EventId == eventId).ToListAsync();
                if (eventData.Count > 0)
                {
                    var marketData = await db.Market.Where(x => x.EventId == eventId).ToListAsync();
                    var runnerData = await db.Runner.Where(x => x.EventId == eventId).ToListAsync();
                    db.Market.RemoveRange(marketData);
                    db.Runner.RemoveRange(runnerData);
                    db.Event.RemoveRange(eventData);
                    await db.SaveChangesAsync();
                    responseDTO.Status = true;
                    responseDTO.Result = "Success";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetScore")]
        public async Task<IHttpActionResult> GetScore(string scoreId)
        {
            try
            {
                string scoreUrl = System.Configuration.ConfigurationManager.AppSettings["Score_Url"].ToString();
                var client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));

                var responseMessage = await client.GetAsync(requestUri: scoreUrl + scoreId);
                if (responseMessage.IsSuccessStatusCode)
                {
                    string data = responseMessage.Content.ReadAsStringAsync().Result;
                    dynamic objList = JsonConvert.DeserializeObject<dynamic>(data);
                    if (objList != null)
                    {
                        JArray eventsArray = (JArray)objList.doc[0].data["events"];
                        var responseData = new
                        {
                            Teams = objList.doc[0].data.match.teams,
                            Status = objList.doc[0].data.match.status.name == "Not started" ? false : true,
                            Toss = objList.doc[0].data.match.coinToss,
                            Resultinfo = objList.doc[0].data.match.resultinfo,
                            Event = eventsArray.Count>0?objList.doc[0].data.events: eventsArray.Count,
                        };

                        responseDTO.Status = true;
                        responseDTO.Result = responseData;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Data";
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "No Data";
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetEventsAsPerTimeSpan")]
        public async Task<IHttpActionResult> GetEventsAsPerTimeSpan(DateTime sDate, DateTime eDate,int sportsId)
        {
            try
            {
                var events = await db.Bet.AsNoTracking().Where(x => x.createdOn >= sDate && x.createdOn <= eDate && x.BetStatus == "Deleted").GroupBy(x => x.EventId).Select(x=>x.FirstOrDefault()).OrderByDescending(x=>x.createdOn).ToListAsync();
                if (events.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = events;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = events;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetEventFancyList")]
        public async Task<IHttpActionResult> GetEventFancyList(string eventId)
        {
            try
            {
                var fancy = await db.Bet.AsNoTracking().Where(x => x.EventId==eventId && x.MarketName=="Fancy" && x.BetStatus == "Deleted").GroupBy(x => x.RunnerName).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.createdOn).ToListAsync();
                if (fancy.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = fancy;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = fancy;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }


    }
}