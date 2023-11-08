using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using FixWebApi.Authentication;
using FixWebApi.Models;
using FixWebApi.Models.BetFairClasses;
using FixWebApi.Models.DTO;
using Newtonsoft.Json;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Market")]
    [CustomAuthorization]
    public class MarketController : ApiController
    {
        ResponseDTO responseDTO = new ResponseDTO();
        private FixDbContext db = new FixDbContext();
        Runtime run_time = new Runtime();

        [HttpPost]
        [Route("Update_Market")]
        public async Task<IHttpActionResult> PutMarketModel(MarketModel marketModel)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                string role = run_time.RunTimeRole();
                var mrktObj = await db.Market.Where(x => x.EventId == marketModel.EventId && x.MarketId == marketModel.MarketId && x.marketName == marketModel.marketName).FirstOrDefaultAsync();
                if (role == "SuperAdmin")
                {
                    if (mrktObj != null)
                    {
                        var eventModel = await db.Event.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == marketModel.EventId);
                        var apiUrlObj = await db.ThirdPartyApiModel.AsNoTracking().Select(x => new { x.BetfairUrl, x.DaimondUrl, x.SportsId }).FirstOrDefaultAsync(x => x.SportsId == eventModel.SportsId);
                        mrktObj.ApiUrlType = marketModel.ApiUrlType;
                        mrktObj.Betdelay = marketModel.Betdelay;
                        mrktObj.MaxStake = marketModel.MaxStake;
                        mrktObj.MinStake = marketModel.MinStake;
                        mrktObj.Fancydelay = marketModel.Fancydelay;
                        mrktObj.status = marketModel.status;
                    }
                }
                else
                {
                    var blkMrkt = await db.BlockMarket.Where(x => x.MarketId == marketModel.MarketId && x.EventId == marketModel.EventId && x.UserId == id).FirstOrDefaultAsync();
                    if (blkMrkt != null)
                    {
                        blkMrkt.status = marketModel.status;
                    }
                    else
                    {
                        BlockMarketModel blckMdl = new BlockMarketModel()
                        {
                            EventId = marketModel.EventId,
                            MarketId = marketModel.MarketId,
                            UserId = id,
                            status = marketModel.status,
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

        //[HttpGet]
        //[Route("GetRates")]
        //public async Task<IHttpActionResult> GetRates(string eventId, int sportsId)
        //{
        //    try
        //    {
        //        ApiData betFairObj = new ApiData();

        //        var marketObj = await db.Market.AsNoTracking().Where(x => x.EventId == eventId && !x.status).ToListAsync();
        //        if (marketObj.Count > 0)
        //        {
        //            var client = new HttpClient();
        //            client.DefaultRequestHeaders.Clear();
        //            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
        //            foreach (var item in marketObj)
        //            {
        //                if (item.marketName.Equals("Match Odds") && sportsId == 4)
        //                {
        //                    string Cric_Url = System.Configuration.ConfigurationManager.AppSettings["CR_Rate_Url"].ToString();
        //                    var cricMessage = await client.GetAsync(requestUri: Cric_Url + item.MarketId + "/" + item.EventId);
        //                    if (cricMessage.IsSuccessStatusCode)
        //                    {
        //                        var data = cricMessage.Content.ReadAsStringAsync().Result;
        //                        CricketAPIDTO cricObj = JsonConvert.DeserializeObject<CricketAPIDTO>(data);
        //                        betFairObj.CricFair = cricObj;
        //                    }
        //                    break;
        //                }
        //                else
        //                {
        //                    string Api_Url = System.Configuration.ConfigurationManager.AppSettings["ST_Rate_Url"].ToString();
        //                    var responseMessage = await client.GetAsync(requestUri: Api_Url + "sportsid=" + sportsId + "&marketid=" + item.MarketId);
        //                    if (responseMessage.IsSuccessStatusCode)
        //                    {
        //                        var data = responseMessage.Content.ReadAsStringAsync().Result;
        //                        BetFairAPIDTO betObj = JsonConvert.DeserializeObject<BetFairAPIDTO>(data);
        //                        betFairObj.betFair = betObj;
        //                    }
        //                }
        //            }

        //            responseDTO.Status = false;
        //            responseDTO.Result = betFairObj;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        responseDTO.Status = false;
        //        responseDTO.Result = ex.Message;
        //    }
        //    return Ok(responseDTO);
        //}


        [HttpGet]
        [Route("GetEvntLst")]
        public async Task<IHttpActionResult> GetEvntLst(int type, int sportsId)
        {
            try
            {
                switch (type)
                {
                    case 0:


                        var mrktLst = await (from e in db.Bet
                                             where e.SportsId == sportsId
                                             && e.BetStatus == "Pending" && e.MarketName == "Fancy"
                                             select new
                                             {
                                                 e.id,
                                                 e.EventName,
                                                 e.EventId,
                                             }).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).ToListAsync();
                        if (mrktLst.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = mrktLst;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = mrktLst;
                        }
                        break;
                    case 1:
                        var rolMrktLst = await (from e in db.Bet
                                                where e.SportsId == sportsId
                                                && e.BetStatus != "Pending" && e.MarketName == "Fancy"
                                                select new
                                                {
                                                    e.id,
                                                    e.EventName,
                                                    e.EventId,
                                                    e.createdOn,
                                                }).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).ToListAsync();
                        if (rolMrktLst.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = rolMrktLst.OrderByDescending(x => x.createdOn);
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = rolMrktLst;
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
        [Route("GetFancyList")]
        public async Task<IHttpActionResult> getFancyList(int type, string eventId)
        {
            try
            {
                switch (type)
                {
                    case 0:
                        var fancyObj = await (from f in db.Bet
                                              where f.EventId == eventId
                                              && f.BetStatus == "Pending"
                                              && f.MarketName == "Fancy"
                                              select new
                                              {
                                                  f.EventId,
                                                  f.MarketId,
                                                  f.MarketName,
                                                  f.RunnerId,
                                                  f.RunnerName,
                                                  f.EventName,
                                              }).GroupBy(x => x.RunnerId).Select(x => x.FirstOrDefault()).ToListAsync();
                        if (fancyObj.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = fancyObj;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Pending Fancy";
                        }
                        break;
                    case 1:
                        var rolObj = await (from f in db.Bet
                                            where f.EventId == eventId
                                            && f.BetStatus != "Pending"
                                            && f.MarketName == "Fancy"
                                            select new
                                            {
                                                f.EventId,
                                                f.MarketId,
                                                f.MarketName,
                                                f.RunnerId,
                                                f.RunnerName,
                                                f.EventName,
                                                f.Result,
                                                createdOn=db.Event.FirstOrDefault(x=>x.EventId==f.EventId).EventTime,
                                            }).OrderByDescending(x=>x.createdOn).GroupBy(x => x.RunnerId).Select(x => x.FirstOrDefault()).ToListAsync();
                        if (rolObj.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = rolObj;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No List for RollBack Fancy";
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
        [Route("Ab_Fancy")]
        public async Task<IHttpActionResult> TerminateFancy(string eventId, string marketId, string runnerId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.RunnerId == runnerId && x.MarketId == marketId && x.EventId == eventId && x.BetStatus == "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        betModels.ForEach(x =>
                        {
                            x.BetStatus = "Abandoned";
                            x.Result = "Abandoned";
                            x.deleted = true;
                        });
                        var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.RunnerId == runnerId && !x.deleted).ToListAsync();
                        if (expObj.Count > 0)
                        {
                            expObj.ForEach(x => x.deleted = true);
                        }
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Fancy Terminated Successfully";
                    }
                    else
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Fancy Terminated Successfully";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("FancyResult")]
        public async Task<IHttpActionResult> fancyResult(string eventId, string marketId, string runnerId, int runValue)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.RunnerId == runnerId && x.MarketId == marketId && x.EventId == eventId && x.BetStatus == "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        var winBets = betModels.Where(x => x.Odds <= runValue && x.BetType == "Yes").ToList();
                        winBets.AddRange(betModels.Where(x => x.Odds > runValue && x.BetType == "No").ToList());

                        var lostBets = betModels.Where(x => x.Odds > runValue && x.BetType == "Yes").ToList();
                        lostBets.AddRange(betModels.Where(x => x.Odds <= runValue && x.BetType == "No").ToList());

                        if (winBets.Count > 0)
                        {
                            winBets.ForEach(x =>
                            {
                                x.BetStatus = "Win";
                                x.Result = runValue.ToString();
                            });
                        }
                        if (lostBets.Count > 0)
                        {
                            lostBets.ForEach(x =>
                            {
                                x.BetStatus = "Lost";
                                x.Profit = -x.Exposure;
                                x.Result = runValue.ToString();
                            });
                        }
                        var userBets = betModels.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                        if (userBets.Count > 0)
                        {
                            List<TransactionModel> trnsList = new List<TransactionModel>();
                            foreach (var item in userBets)
                            {
                                double profit = Math.Round(betModels.Where(x => x.UserId == item.UserId).Select(x => x.Profit).DefaultIfEmpty(0).Sum(), 2);
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                bal = Math.Round(bal + profit, 2);
                                var userObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.UserId);
                                userObj.Balance = bal;
                                userObj.ExposureLimit = bal;
                                TransactionModel transModel = new TransactionModel()
                                {
                                    UserId = item.UserId,
                                    UserName = item.UserName,
                                    SportsId = item.SportsId,
                                    EventId = eventId,
                                    MarketId = marketId,
                                    SelectionId = runnerId,
                                    Discription = item.RunnerName + "(" + item.EventName + ")",
                                    MarketName = "Fancy",
                                    Amount = profit,
                                    Balance = bal,
                                    SuperMasterId = item.SuperMasterId,
                                    AgentId = item.AgentId,
                                    SuperAgentId = item.SuperAgentId,
                                    SubAdminId = item.SubAdminId,
                                    ParentId = item.ParentId,
                                    MasterId = item.MasterId,
                                    AdminId = item.AdminId,
                                    SuperId = item.SuperId,
                                    Parent = 0,
                                    createdOn = DateTime.Now,
                                };
                                trnsList.Add(transModel);

                                await db.SaveChangesAsync();
                            }
                            var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.RunnerId == runnerId && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                expObj.ForEach(x => x.deleted = true);
                            }
                            db.Transaction.AddRange(trnsList);
                            await db.SaveChangesAsync();
                            dbContextTransaction.Commit();
                            responseDTO.Status = true;
                            responseDTO.Result = "Fancy Result Declared Successfully";
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Bets On Fancy";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("FancyRollBack")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> fancyRolBk(string eventId, string marketId, string runnerId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.RunnerId == runnerId && x.MarketId == marketId && x.EventId == eventId && x.BetStatus != "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        foreach (var item in betModels)
                        {
                            if (item.BetStatus == "Lost")
                            {
                                if (item.BetType == "Yes")
                                {
                                    item.Profit = item.Stake * item.Price / 100;
                                }
                                else
                                {
                                    item.Profit = item.Stake;
                                }
                            }
                            item.BetStatus = "Pending";
                            item.Result = "";

                        }
                        var tranObj = await db.Transaction.Where(x => x.MarketId == marketId && x.EventId == eventId && x.SelectionId == runnerId).ToListAsync();
                        if (tranObj.Count > 0)
                        {
                            foreach (var item in tranObj)
                            {
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                var userObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.UserId);
                                userObj.Balance = bal - item.Amount;
                                var afterTran = await db.Transaction.Where(x => x.UserId == item.UserId && x.id > item.id).ToListAsync();
                                if (afterTran.Count > 0)
                                {
                                    foreach (var aftr in afterTran)
                                    {
                                        aftr.Balance = aftr.Balance - item.Amount;
                                    }
                                }
                                await db.SaveChangesAsync();
                            }
                            db.Transaction.RemoveRange(tranObj);
                        }
                        var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.RunnerId == runnerId && x.deleted).ToListAsync();
                        if (expObj.Count > 0)
                        {
                            expObj.ForEach(x => x.deleted = false);
                        }
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Fancy Result RollBack Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Bets On Fancy";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("Ab_Market")]
        public async Task<IHttpActionResult> TerminateMarket(string eventId, string marketId, string marketName)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {

                    var betModels = await db.Bet.Where(x => x.deleted == false && x.MarketId == marketId && x.EventId == eventId && x.BetStatus == "Pending" && x.MarketName == marketName).ToListAsync();
                    var eventModel = await db.Event.FirstOrDefaultAsync(x => x.EventId.Equals(eventId));
                    if (betModels.Count > 0)
                    {
                        betModels.ForEach(x =>
                        {
                            x.BetStatus = "Abandoned";
                            x.Result = "Abandoned";
                        });
                        var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.MarketId == marketId && !x.deleted && x.MarketName == marketName).ToListAsync();
                        if (expObj.Count > 0)
                        {
                            expObj.ForEach(x => x.deleted = true);
                        }
                        var marketModel = await db.Market.Where(x => x.MarketId == marketId && x.EventId == eventId && x.marketName == marketName).ToListAsync();
                        marketModel.ForEach(x =>
                        {
                            x.status = true;
                            x.deleted = true;
                            x.Result = "Abandoned";
                        });
                        (await db.Runner.Where(x => x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName).ToListAsync()).ForEach(x =>
                        {
                            x.status = true;
                            x.deleted = true;
                        });

                        await db.SaveChangesAsync();
                        responseDTO.Status = true;
                        responseDTO.Result = "Market Terminated Successfully";
                    }
                    else
                    {
                        var marketModel = await db.Market.Where(x => x.MarketId == marketId && x.EventId == eventId && x.marketName == marketName).ToListAsync();
                        marketModel.ForEach(x =>
                        {
                            x.status = true;
                            x.deleted = true;
                            x.Result = "Abandoned";
                        });
                        (await db.Runner.Where(x => x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName).ToListAsync()).ForEach(x =>
                        {
                            x.status = true;
                            x.deleted = true;
                        });

                        await db.SaveChangesAsync();
                        responseDTO.Status = true;
                        responseDTO.Result = "Market Terminated Successfully";
                    }
                    var checkMarket = await db.Market.FirstOrDefaultAsync(x => x.EventId == eventId && x.deleted == false);
                    if (checkMarket == null)
                    {
                        eventModel.status = true;
                        eventModel.deleted = true;
                        await db.SaveChangesAsync();
                    }
                    dbContextTransaction.Commit();
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("MarketResult")]
        public async Task<IHttpActionResult> marketResult(string eventId, string marketId, string marketName, string runnerId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    int rId = Convert.ToInt32(runnerId);
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.MarketName == marketName && x.MarketId == marketId && x.EventId == eventId && x.BetStatus == "Pending").ToListAsync();
                    string winnerName = (await db.Runner.AsNoTracking().FirstOrDefaultAsync(x => x.id == rId)).RunnerName;
                    if (betModels.Count > 0)
                    {
                       
                        var winBets = betModels.Where(x => x.RunnerId == runnerId && x.BetType == "Back").ToList();
                        winBets.AddRange(betModels.Where(x => x.RunnerId != runnerId && x.BetType == "Lay").ToList());

                        var lostBets = betModels.Where(x => x.RunnerId == runnerId && x.BetType == "Lay").ToList();
                        lostBets.AddRange(betModels.Where(x => x.RunnerId != runnerId && x.BetType == "Back").ToList());

                        if (winBets.Count > 0)
                        {
                            winBets.ForEach(x =>
                            {
                                x.BetStatus = "Win";
                                x.Result = winnerName;
                            });
                        }
                        if (lostBets.Count > 0)
                        {
                            lostBets.ForEach(x =>
                            {
                                x.BetStatus = "Lost";
                                x.Profit = -x.Exposure;
                                x.Result = winnerName;
                            });
                        }
                        var userBets = betModels.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                        if (userBets.Count > 0)
                        {
                            List<TransactionModel> trnsList = new List<TransactionModel>();
                            foreach (var item in userBets)
                            {
                                double com = 0;
                                var userObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.UserId);
                                double profit = Math.Round(betModels.Where(x => x.UserId == item.UserId).Select(x => x.Profit).DefaultIfEmpty(0).Sum(), 2);
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                if (marketName == "Match Odds")
                                {
                                    if (profit > 0)
                                    {
                                        com = Math.Round(userObj.MatchCommission * profit / 100, 2);
                                        profit = Math.Round(profit - com, 2);
                                    }
                                }
                                bal = Math.Round(bal + profit, 2);
                                userObj.Balance = bal;
                                userObj.ExposureLimit = bal;
                                TransactionModel transModel = new TransactionModel()
                                {
                                    UserId = item.UserId,
                                    UserName = item.UserName,
                                    SportsId = item.SportsId,
                                    EventId = eventId,
                                    MarketId = marketId,
                                    SelectionId = runnerId,
                                    Discription = item.EventName + "(" + item.MarketName + ")",
                                    MarketName = marketName,
                                    Amount = profit,
                                    Commission = com,
                                    Balance = bal,
                                    AgentId = item.AgentId,
                                    SuperAgentId = item.SuperAgentId,
                                    SuperMasterId = item.SuperMasterId,
                                    SubAdminId = item.SubAdminId,
                                    ParentId = item.ParentId,
                                    MasterId = item.MasterId,
                                    AdminId = item.AdminId,
                                    SuperId = item.SuperId,
                                    Parent = 0,
                                    createdOn = DateTime.Now,
                                };
                                trnsList.Add(transModel);
                                await db.SaveChangesAsync();
                            }
                            var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.MarketName == marketName && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                expObj.ForEach(x => x.deleted = true);
                            }
                            var marketModel = await db.Market.FirstOrDefaultAsync(x => x.MarketId == marketId && x.EventId == eventId && x.marketName == marketName);
                            marketModel.status = true;
                            marketModel.deleted = true;
                            marketModel.Result = (await db.Runner.FirstOrDefaultAsync(x => x.id == rId)).RunnerName;
                            (await db.Runner.Where(x => x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName).ToListAsync()).ForEach(x =>
                           {
                               x.status = true;
                               x.deleted = true;
                           });
                            db.Transaction.AddRange(trnsList);
                            await db.SaveChangesAsync();

                            var checkMarkets = await db.Market.FirstOrDefaultAsync(x => x.EventId == eventId && x.deleted==false);
                            if (checkMarkets == null)
                            {
                                var eventData = await db.Event.FirstOrDefaultAsync(x => x.EventId == eventId);
                                eventData.deleted = true;
                                eventData.status = true;
                                await db.SaveChangesAsync();
                            }
                            dbContextTransaction.Commit();
                            responseDTO.Status = true;
                            responseDTO.Result = "Market Result Declared Successfully";
                        }
                    }
                    else
                    {                       
                        var marketModel = await db.Market.FirstOrDefaultAsync(x => x.MarketId == marketId && x.EventId == eventId && x.marketName == marketName);
                        marketModel.status = true;
                        marketModel.deleted = true;
                        marketModel.Result = (await db.Runner.FirstOrDefaultAsync(x => x.id == rId)).RunnerName;
                        await db.SaveChangesAsync();
                        var checkMarkets = await db.Market.FirstOrDefaultAsync(x => x.EventId == eventId && x.deleted == false);
                        if (checkMarkets == null)
                        {
                            var eventData = await db.Event.FirstOrDefaultAsync(x => x.EventId == eventId);
                            eventData.deleted = true;
                            eventData.status = true;
                            await db.SaveChangesAsync();
                        }
                        
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Market Result Declared Successfully";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("MrktRollBack")]
        public async Task<IHttpActionResult> mrktRolBk(string eventId, string marketId, string marketName)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.MarketName == marketName && x.MarketId == marketId && x.EventId == eventId && x.BetStatus != "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        foreach (var item in betModels)
                        {
                            if (item.BetStatus == "Lost")
                            {
                                if (item.BetType == "Back")
                                {
                                    item.Profit = (item.Odds - 1) * item.Stake;
                                }
                                else
                                {
                                    item.Profit = item.Stake;
                                }
                            }
                            item.BetStatus = "Pending";
                            item.Result = "";

                        }
                        var tranObj = await db.Transaction.Where(x => x.MarketId == marketId && x.EventId == eventId && x.MarketName == marketName).ToListAsync();
                        if (tranObj.Count > 0)
                        {
                            foreach (var item in tranObj)
                            {
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                var userObj = await db.SignUp.Where(x => x.id == item.UserId).FirstOrDefaultAsync();
                                userObj.Balance = bal - item.Amount;
                                var afterTran = await db.Transaction.Where(x => x.UserId == item.UserId && x.id > item.id).ToListAsync();
                                if (afterTran.Count > 0)
                                {
                                    foreach (var aftr in afterTran)
                                    {
                                        aftr.Balance = aftr.Balance - item.Amount;
                                    }
                                }
                                await db.SaveChangesAsync();
                            }
                            db.Transaction.RemoveRange(tranObj);
                        }
                        var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.MarketName == marketName && x.deleted).ToListAsync();
                        if (expObj.Count > 0)
                        {
                            expObj.ForEach(x => x.deleted = false);
                        }
                        var marketModel = await db.Market.Where(x => x.MarketId == marketId && x.marketName == marketName && x.EventId == eventId).FirstOrDefaultAsync();
                        marketModel.status = false;
                        marketModel.deleted = false;
                        marketModel.Result = "";
                        (await db.Runner.Where(x => x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName).ToListAsync()).ForEach(x =>
                        {
                            x.status = false;
                            x.deleted = false;
                        });
                        var eventData = await db.Event.FirstOrDefaultAsync(x => x.EventId == eventId);
                        eventData.deleted = false;
                        eventData.status = false;
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Market Result RollBack Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Bets On Market";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("CasinoResult")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> casinoResult()
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    string casinourl = "";
                    List<TransactionModel> trnsList = new List<TransactionModel>();
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                    int sportsId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]);
                    var bets = await db.Bet.Where(x => x.SportsId == sportsId && !x.deleted && x.BetStatus == "Pending").ToListAsync();
                    var betsModel = bets.GroupBy(x => x.MarketId).Select(x => x.FirstOrDefault()).ToList();
                    if (betsModel.Count > 0)
                    {
                        foreach (var item in betsModel)
                        {
                            casinourl = System.Configuration.ConfigurationManager.AppSettings[item.MarketName].ToString();

                            var responseMessage = await client.GetAsync(requestUri: casinourl);
                            if (responseMessage.IsSuccessStatusCode)
                            {
                                var data = responseMessage.Content.ReadAsStringAsync().Result;
                                CasinoResult casinoResult = JsonConvert.DeserializeObject<CasinoResult>(data);
                                if (casinoResult != null)
                                {
                                    var result = casinoResult.data.FirstOrDefault(x => x.mid == item.MarketId);
                                    if (result != null)
                                    {
                                        switch (item.MarketName)
                                        {
                                            case "20-20 TeenPatti":
                                                result.result = result.result == "1" || result.result == "3" ? result.result : "0";
                                                break;
                                            case "Lucky7-A":                                            
                                                result.result = result.result == "1" || result.result == "2" ? result.result : "0";
                                                break;
                                            case "DragonTiger20-20":
                                                result.result = result.result == "1" || result.result == "2" ? result.result : "3";
                                                break;
                                            case "AndarBahar":
                                                result.result = result.result == "2" ? "4" : result.result;
                                                break;
                                        }
                                        if (result.result == "0")
                                        {
                                            var deletedBets = await db.Bet.Where(x => x.MarketId == item.MarketId).ToListAsync();
                                            if (deletedBets.Count > 0)
                                            {
                                                deletedBets.ForEach(x =>
                                                {
                                                    x.BetStatus = "Tied";
                                                    x.Profit = 0;
                                                    x.Result = "Tied";
                                                });
                                                await db.SaveChangesAsync();
                                                var betData = deletedBets.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                                                foreach (var items in betData)
                                                {
                                                    var expObj = await db.Exposure.Where(x => x.MarketId == item.MarketId && x.UserId == items.UserId && !x.deleted).ToListAsync();
                                                    if (expObj.Count > 0)
                                                    {
                                                        expObj.ForEach(x => x.deleted = true);
                                                    }
                                                    await db.SaveChangesAsync();
                                                }

                                            }
                                        }
                                        else
                                        {

                                            var winBets = bets.Where(x => x.RunnerId == result.result && x.MarketId == item.MarketId && x.BetType == "Back").ToList();
                                            winBets.AddRange(bets.Where(x => x.RunnerId != result.result && x.BetType == "Lay" && x.MarketId == item.MarketId).ToList());

                                            var lostBets = bets.Where(x => x.RunnerId == result.result && x.BetType == "Lay" && x.MarketId == item.MarketId).ToList();
                                            lostBets.AddRange(bets.Where(x => x.RunnerId != result.result && x.BetType == "Back" && x.MarketId == item.MarketId).ToList());

                                            if (winBets.Count > 0)
                                            {
                                                winBets.ForEach(x =>
                                                {
                                                    x.BetStatus = "Win";
                                                    switch (x.MarketName)
                                                    {
                                                        case "20-20 TeenPatti":
                                                            x.Result = result.result == "1" ? "PlayerA" : result.result == "3" ? "PlayerB" : "Tied";
                                                            break;
                                                        case "Lucky7-A":
                                                            x.Result = result.result == "1" ? "Low Card" : result.result == "2" ? "High Card" : "Tied";
                                                            break;
                                                        case "Amar Akbar Anthony":
                                                            x.Result = result.result == "1" ? "Amar" : result.result == "2" ? "Akbar" : "Anthony";
                                                            break;
                                                        case "DragonTiger20-20":
                                                            x.Result = result.result == "1" ? "Dragon" : result.result == "2" ? "Tiger" : "Tied";
                                                            break;
                                                        case "AndarBahar":
                                                            x.Result = result.result == "1" ? "SA" : "SB";
                                                            break;
                                                    }

                                                });
                                            }
                                            if (lostBets.Count > 0)
                                            {
                                                lostBets.ForEach(x =>
                                                {
                                                    x.BetStatus = "Lost";
                                                    x.Profit = -x.Exposure;
                                                    switch (x.MarketName)
                                                    {
                                                        case "20-20 TeenPatti":
                                                            x.Result = result.result == "1" ? "PlayerA" : result.result == "3" ? "PlayerB" : "Tied";
                                                            break;
                                                        case "Lucky7-A":
                                                            x.Result = result.result == "1" ? "Low Card" : result.result == "2" ? "High Card" : "Tied";
                                                            break;
                                                        case "Amar Akbar Anthony":
                                                            x.Result = result.result == "1" ? "Amar" : result.result == "2" ? "Akbar" : "Anthony";
                                                            break;
                                                        case "DragonTiger20-20":
                                                            x.Result = result.result == "1" ? "Dragon" : result.result == "2" ? "Tiger" : "Tied";
                                                            break;
                                                        case "AndarBahar":
                                                            x.Result = result.result == "1" ? "SA" : "SB";
                                                            break;
                                                    }

                                                });
                                            }
                                            await db.SaveChangesAsync();

                                            var userBets = bets.Where(x => x.SportsId == sportsId && !x.deleted && x.MarketId == item.MarketId).GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                                            if (bets.Count > 0)
                                            {
                                                foreach (var bts in userBets)
                                                {
                                                    double profit = Math.Round(bets.Where(x => x.UserId == bts.UserId && x.MarketId == item.MarketId).Select(x => x.Profit).DefaultIfEmpty(0).Sum(), 2);
                                                    double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == bts.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    bal = Math.Round(bal + profit, 2);
                                                    var userObj = await db.SignUp.Where(x => x.id == bts.UserId).FirstOrDefaultAsync();
                                                    userObj.Balance = bal;
                                                    userObj.ExposureLimit = bal;
                                                    TransactionModel transModel = new TransactionModel()
                                                    {
                                                        UserId = bts.UserId,
                                                        UserName = bts.UserName,
                                                        SportsId = bts.SportsId,
                                                        EventId = bts.EventId,
                                                        MarketId = bts.MarketId,
                                                        SelectionId = bts.RunnerId,
                                                        Discription = bts.EventName + " " + bts.MarketId + "(" + bts.MarketName + ")",
                                                        MarketName = bts.MarketName,
                                                        Amount = profit,
                                                        Balance = bal,
                                                        SuperMasterId = item.SuperMasterId,
                                                        AgentId = item.AgentId,
                                                        SuperAgentId = item.SuperAgentId,
                                                        SubAdminId = item.SubAdminId,
                                                        ParentId = bts.ParentId,
                                                        MasterId = bts.MasterId,
                                                        AdminId = bts.AdminId,
                                                        SuperId = bts.SuperId,
                                                        Parent = 0,
                                                        createdOn = DateTime.Now,
                                                    };
                                                    trnsList.Add(transModel);
                                                    var expObj = await db.Exposure.Where(x => x.MarketId == item.MarketId && x.UserId == bts.UserId && !x.deleted).ToListAsync();
                                                    if (expObj.Count > 0)
                                                    {
                                                        expObj.ForEach(x => x.deleted = true);
                                                    }
                                                    await db.SaveChangesAsync();
                                                }
                                            }
                                            else
                                            {
                                                var expObj = await db.Exposure.Where(x => x.MarketId == item.MarketId && x.EventId == item.EventId).ToListAsync();
                                                if (expObj.Count > 0)
                                                {
                                                    expObj.ForEach(x => x.deleted = true);
                                                    await db.SaveChangesAsync();
                                                }
                                            }
                                        }

                                    }
                                }
                            }
                        }
                        db.Transaction.AddRange(trnsList);
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Market Result Declared Successfully";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }


        [HttpGet]
        [Route("CasinoMarketResult")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> casinoMarketResult(string eventId,string marketId, string marketName, string runnerId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    int rId = Convert.ToInt32(runnerId);
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.MarketName == marketName && x.MarketId == marketId && x.BetStatus == "Pending").ToListAsync();
                    string winnerName = (await db.Runner.AsNoTracking().FirstOrDefaultAsync(x => x.RunnerId == rId && x.MarketName==marketName)).RunnerName;
                    if (betModels.Count > 0)
                    {
                       
                        var winBets = betModels.Where(x => x.RunnerId == runnerId && x.BetType == "Back").ToList();
                        winBets.AddRange(betModels.Where(x => x.RunnerId != runnerId && x.BetType == "Lay").ToList());

                        var lostBets = betModels.Where(x => x.RunnerId == runnerId && x.BetType == "Lay").ToList();
                        lostBets.AddRange(betModels.Where(x => x.RunnerId != runnerId && x.BetType == "Back").ToList());

                        if (winBets.Count > 0)
                        {
                            winBets.ForEach(x =>
                            {
                                x.BetStatus = "Win";
                                x.Result = winnerName;
                            });
                        }
                        if (lostBets.Count > 0)
                        {
                            lostBets.ForEach(x =>
                            {
                                x.BetStatus = "Lost";
                                x.Profit = -x.Exposure;
                                x.Result = winnerName;
                            });
                        }
                        var userBets = betModels.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                        if (userBets.Count > 0)
                        {
                            List<TransactionModel> trnsList = new List<TransactionModel>();
                            foreach (var item in userBets)
                            {
                                double com = 0;
                                var userObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.UserId);
                                double profit = Math.Round(betModels.Where(x => x.UserId == item.UserId).Select(x => x.Profit).DefaultIfEmpty(0).Sum(), 2);
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                              
                                bal = Math.Round(bal + profit, 2);
                                userObj.Balance = bal;
                                userObj.ExposureLimit = bal;
                                TransactionModel transModel = new TransactionModel()
                                {
                                    UserId = item.UserId,
                                    UserName = item.UserName,
                                    SportsId = item.SportsId,
                                    EventId = eventId,
                                    MarketId = marketId,
                                    SelectionId = runnerId,
                                    Discription = item.EventName + "(" + item.MarketName + ")",
                                    MarketName = marketName,
                                    Amount = profit,
                                    Commission = com,
                                    Balance = bal,
                                    SuperMasterId = item.SuperMasterId,
                                    AgentId = item.AgentId,
                                    SuperAgentId = item.SuperAgentId,
                                    SubAdminId = item.SubAdminId,
                                    ParentId = item.ParentId,
                                    MasterId = item.MasterId,
                                    AdminId = item.AdminId,
                                    SuperId = item.SuperId,
                                    Parent = 0,
                                    createdOn = DateTime.Now,
                                };
                                trnsList.Add(transModel);
                                await db.SaveChangesAsync();
                            }
                            var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.MarketName == marketName && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                expObj.ForEach(x => x.deleted = true);
                            }
                           
                            db.Transaction.AddRange(trnsList);
                            await db.SaveChangesAsync();
                            dbContextTransaction.Commit();
                            responseDTO.Status = true;
                            responseDTO.Result = "Market Result Declared Successfully";
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpPost]
        [Route("saveGameCards")]
        public async Task<IHttpActionResult> saveGameCards(CardDetails obj)
        {
            try
            {
                if (obj.MarketId != null && obj.MarketName != null && obj.CardNames != null)
                {
                    var marketCheck = await db.Cards.AsNoTracking().FirstOrDefaultAsync(x => x.MarketId == obj.MarketId);
                    if (marketCheck == null)
                    {
                        if (obj.MarketId != "0" && obj.MarketId != null && obj.CardNames != "" && obj.CardNames != null && !obj.CardNames.Contains("null"))
                        {
                            obj.createdOn = DateTime.Now;
                            db.Cards.Add(obj);
                            await db.SaveChangesAsync();
                            responseDTO.Status = true;
                            responseDTO.Result = "Done";
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
        [Route("getResult")]
        public async Task<IHttpActionResult> getResult(string roundId)
        {
            try
            {
                var resultdata = await db.Cards.Where(x => x.MarketId == roundId).FirstOrDefaultAsync();
                if (resultdata != null)
                {
                    if (resultdata.MarketName.Contains("Lucky7") || resultdata.MarketName.Contains("Amar-Akbar"))
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = resultdata;
                    }
                    else
                    {

                        var cardArray = resultdata.CardNames.Split(',');
                        responseDTO.Status = true;
                        responseDTO.Result = cardArray;
                    }
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message; ;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("CloseEvent")]
        public async Task<IHttpActionResult> closeEvent(string eventId)
        {
            try
            {
                var evntObj = await db.Event.Where(x => x.EventId == eventId && !x.deleted).FirstOrDefaultAsync();
                if (evntObj != null)
                {
                    var marktObj = await db.Market.Where(x => x.EventId == eventId).ToListAsync();
                    if (marktObj.Count > 0)
                    {
                        var runObj = await db.Runner.Where(x => x.EventId == eventId).ToListAsync();
                        if (runObj.Count > 0)
                        {
                            evntObj.status = true;
                            evntObj.deleted = true;
                            marktObj.ForEach(x =>
                            {
                                x.deleted = true;
                                x.status = true;
                                x.Result = "Deleted";
                            });
                            runObj.ForEach(x =>
                            {
                                x.deleted = true;
                                x.status = true;
                            });
                            await db.SaveChangesAsync();
                            responseDTO.Status = true;
                            responseDTO.Result = "Success";
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Error";
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
        [Route("LastDigitFancyResult")]
        public async Task<IHttpActionResult> lastDigitFancyResult(string eventId, int sessionId, int runValue, string marketId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.RunnerId == sessionId.ToString() && x.MarketId == marketId && x.EventId == eventId && x.BetStatus == "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        var winBets = betModels.Where(x => x.Odds == runValue && x.BetType == "Yes").ToList();
                        winBets.AddRange(betModels.Where(x => x.Odds != runValue && x.BetType == "No").ToList());

                        var lostBets = betModels.Where(x => x.Odds != runValue && x.BetType == "Yes").ToList();
                        lostBets.AddRange(betModels.Where(x => x.Odds == runValue && x.BetType == "No").ToList());

                        if (winBets.Count > 0)
                        {
                            winBets.ForEach(x =>
                            {
                                x.BetStatus = "Win";
                                x.Result = runValue.ToString();
                            });
                        }
                        if (lostBets.Count > 0)
                        {
                            lostBets.ForEach(x =>
                            {
                                x.BetStatus = "Lost";
                                x.Profit = -x.Exposure;
                                x.Result = runValue.ToString();
                            });
                        }
                        var userBets = betModels.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                        if (userBets.Count > 0)
                        {
                            List<TransactionModel> trnsList = new List<TransactionModel>();
                            foreach (var item in userBets)
                            {
                                double profit = Math.Round(betModels.Where(x => x.UserId == item.UserId).Select(x => x.Profit).DefaultIfEmpty(0).Sum(), 2);
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                bal = Math.Round(bal + profit, 2);
                                var userObj = await db.SignUp.Where(x => x.id == item.UserId).FirstOrDefaultAsync();
                                userObj.Balance = bal;
                                userObj.ExposureLimit = bal;
                                TransactionModel transModel = new TransactionModel()
                                {
                                    UserId = item.UserId,
                                    UserName = item.UserName,
                                    SportsId = item.SportsId,
                                    EventId = eventId,
                                    MarketId = marketId,
                                    SelectionId = sessionId.ToString(),
                                    Discription = item.RunnerName + "(" + item.EventName + ")",
                                    MarketName = "Last Digit Session",
                                    Amount = profit,
                                    Balance = bal,
                                    SuperMasterId = item.SuperMasterId,
                                    AgentId = item.AgentId,
                                    SuperAgentId = item.SuperAgentId,
                                    SubAdminId = item.SubAdminId,
                                    ParentId = item.ParentId,
                                    MasterId = item.MasterId,
                                    AdminId = item.AdminId,
                                    SuperId = item.SuperId,
                                    Parent = 0,
                                    createdOn = DateTime.Now,
                                };
                                trnsList.Add(transModel);

                                await db.SaveChangesAsync();
                            }
                            var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.RunnerId == sessionId.ToString() && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                expObj.ForEach(x => x.deleted = true);
                            }
                            db.Transaction.AddRange(trnsList);
                            await db.SaveChangesAsync();
                            dbContextTransaction.Commit();
                            responseDTO.Status = true;
                            responseDTO.Result = "Fancy Result Declared Successfully";
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Bets On Fancy";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("LastDigitFancyRollBack")]
        public async Task<IHttpActionResult> lastDigitFancyRolBk(string eventId, string marketId, int sessionId)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var betModels = await db.Bet.Where(x => x.deleted == false && x.RunnerId == sessionId.ToString() && x.MarketId == marketId && x.EventId == eventId && x.BetStatus != "Pending").ToListAsync();
                    if (betModels.Count > 0)
                    {
                        foreach (var item in betModels)
                        {
                            if (item.BetStatus == "Lost")
                            {
                                if (item.BetType == "Yes")
                                {
                                    item.Profit = item.Stake * item.Price / 100;
                                }
                                else
                                {
                                    item.Profit = item.Stake;
                                }
                            }
                            item.BetStatus = "Pending";
                            item.Result = "";

                        }
                        var tranObj = await db.Transaction.Where(x => x.MarketId == marketId && x.EventId == eventId && x.SelectionId == sessionId.ToString()).ToListAsync();
                        if (tranObj.Count > 0)
                        {
                            foreach (var item in tranObj)
                            {
                                double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                var userObj = await db.SignUp.Where(x => x.id == item.UserId).FirstOrDefaultAsync();
                                userObj.Balance = bal - item.Amount;
                                var afterTran = await db.Transaction.Where(x => x.UserId == item.UserId && x.id > item.id).ToListAsync();
                                if (afterTran.Count > 0)
                                {
                                    foreach (var aftr in afterTran)
                                    {
                                        aftr.Balance = aftr.Balance - item.Amount;
                                    }
                                }
                                await db.SaveChangesAsync();
                            }
                            db.Transaction.RemoveRange(tranObj);
                        }
                        var expObj = await db.Exposure.Where(x => x.MarketId == marketId && x.EventId == eventId && x.RunnerId == sessionId.ToString() && x.deleted).ToListAsync();
                        if (expObj.Count > 0)
                        {
                            expObj.ForEach(x => x.deleted = false);
                        }
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();
                        responseDTO.Status = true;
                        responseDTO.Result = "Fancy Result RollBack Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Bets On Fancy";
                    }
                }
                catch (Exception ex)
                {
                    dbContextTransaction.Rollback();
                    responseDTO.Status = false;
                    responseDTO.Result = ex.Message;
                }
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


    }
}