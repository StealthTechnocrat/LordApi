using System;
using System.Collections;
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
using FixWebApi.Models.DTO;
using Newtonsoft.Json;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Bets")]
    [CustomAuthorization]
    public class BetModelsController : ApiController
    {
        private FixDbContext db = new FixDbContext();
        ResponseDTO responseDTO = new ResponseDTO();
        BetResponseDTO betResponseDTO = new BetResponseDTO();
        Runtime _runtime = new Runtime();


        // POST: api/BetModels

        [HttpPost]
        [Route("CreateBet")]
        public async Task<IHttpActionResult> PostBetModel(BetModel betModel)
        {
            try
            {
                string runTimeToken = _runtime.RunTimeToken();

               
                int id = Convert.ToInt32(_runtime.RunTimeUserId());
                var userObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == id && !x.deleted);
                //if (userObj != null && userObj.jwtToken == runTimeToken)
                //{
                if (!userObj.status && !userObj.BetStatus)
                {
                    BetSettingDTO parentObj = new BetSettingDTO()
                    {
                        ParentId = userObj.ParentId,
                        SuperId = userObj.SuperId,
                        MasterId = userObj.MasterId,
                        AdminId = userObj.AdminId,
                        Stake = betModel.Stake
                    };
                    responseDTO = await checkSettings(betModel.MarketName, betModel.EventId, betModel.MarketId, parentObj);
                    if (responseDTO.Status)
                    {
                        if (betModel.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]))
                        {
                            if (betModel.MarketName != "To Win the Toss")
                            {
                                responseDTO = await CheckRates(betModel);
                                if (!responseDTO.Status)
                                {
                                    return Ok(responseDTO);
                                }
                            }
                        }
                        string odds = betModel.MarketName == "BookMaker" ? "1." + betModel.Odds.ToString() : betModel.Odds.ToString();
                        var sportsSetting = await db.UserSetting.FirstOrDefaultAsync(x => x.SportsId == betModel.SportsId);
                        if (sportsSetting.MaxOdds < Convert.ToDouble(odds))
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Max Odds limit is only " + sportsSetting.MaxOdds;
                            return Ok(responseDTO);
                        }
                        if (betModel.BetType == "Back")
                        {

                            if (betModel.MarketName == "BookMaker")
                            {
                                betModel.Profit = Math.Round(betModel.Stake * (betModel.Odds / 100), 2);
                            }
                            else
                            {
                                betModel.Profit = Math.Round(betModel.Stake * (betModel.Odds - 1), 2);
                            }
                            betModel.Exposure = betModel.Stake;
                        }
                        else
                        {
                            if (betModel.MarketName == "BookMaker")
                            {
                                betModel.Exposure = Math.Round(betModel.Stake * (betModel.Odds / 100), 2);
                            }
                            else
                            {
                                betModel.Exposure = Math.Round(betModel.Stake * (betModel.Odds - 1), 2);
                            }
                            betModel.Profit = betModel.Stake;
                        }
                        var rnrMrkt = await db.Runner.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.MarketName == betModel.MarketName).ToListAsync();
                        List<BookDTO> bookObj = new List<BookDTO>();
                        foreach (var rnr in rnrMrkt)
                        {
                            BookDTO bookDto = new BookDTO()
                            {
                                RunnerId = rnr.id,
                                ProfitLoss = 0,
                            };
                            bookObj.Add(bookDto);
                        }
                        var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.MarketId == betModel.MarketId && x.MarketName == betModel.MarketName && x.UserId == id && !x.deleted).ToListAsync();
                        userBetModel.Add(betModel);
                        if (userBetModel.Count > 0)
                        {
                            foreach (var item in userBetModel)
                            {
                                foreach (var itemBk in bookObj)
                                {
                                    if (itemBk.RunnerId == Convert.ToInt32(item.RunnerId))
                                    {
                                        if (item.BetType == "Back")
                                        {
                                            itemBk.ProfitLoss += item.Profit;
                                        }
                                        else
                                        {
                                            itemBk.ProfitLoss += -item.Exposure;
                                        }
                                    }
                                    else
                                    {
                                        if (item.BetType == "Back")
                                        {
                                            itemBk.ProfitLoss += -item.Exposure;
                                        }
                                        else
                                        {
                                            itemBk.ProfitLoss += item.Profit;
                                        }

                                    }
                                }
                            }
                            double marketProfitLoss = bookObj.Max(x => x.ProfitLoss) < 0 ? 0 : bookObj.Max(x => x.ProfitLoss);
                            if (sportsSetting.MaxProfit < marketProfitLoss)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Max profit limit is only " + sportsSetting.MaxProfit;
                                return Ok(responseDTO);
                            }
                            var expObj = await db.Exposure.Where(x => x.UserId == id && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                var mrktExp = expObj.Where(x => x.MarketId == betModel.MarketId && x.MarketName == betModel.MarketName && x.EventId == betModel.EventId).FirstOrDefault();
                                if (mrktExp != null)
                                {
                                    mrktExp.Exposure = bookObj.Min(x => x.ProfitLoss) > 0 ? 0 : bookObj.Min(x => x.ProfitLoss);
                                }
                                else
                                {
                                    ExposureModel expMdl = new ExposureModel()
                                    {
                                        UserId = id,
                                        MarketId = betModel.MarketId,
                                        EventId = betModel.EventId,
                                        MarketName = betModel.MarketName,
                                        Exposure = bookObj.Min(x => x.ProfitLoss),
                                        createdOn = DateTime.Now,
                                        ParentId = userObj.ParentId,
                                        MasterId = userObj.MasterId,
                                        AdminId = userObj.AdminId,
                                        SuperId = userObj.SuperId,
                                    };
                                    db.Exposure.Add(expMdl);
                                    expObj.Add(expMdl);
                                }
                            }
                            else
                            {
                                ExposureModel expMdl = new ExposureModel()
                                {
                                    UserId = id,
                                    MarketId = betModel.MarketId,
                                    EventId = betModel.EventId,
                                    MarketName = betModel.MarketName,
                                    Exposure = bookObj.Min(x => x.ProfitLoss),
                                    createdOn = DateTime.Now,
                                    ParentId = userObj.ParentId,
                                    MasterId = userObj.MasterId,
                                    AdminId = userObj.AdminId,
                                    SuperId = userObj.SuperId,
                                };
                                db.Exposure.Add(expMdl);
                                expObj.Add(expMdl);
                            }
                            double totalExp = expObj.Select(l => l.Exposure).DefaultIfEmpty(0).Sum();
                            if (userObj.ExposureLimit >= -totalExp)
                            {
                                double balance = Math.Round(await db.Transaction.Where(x => x.UserId == id).Select(s => s.Amount).DefaultIfEmpty(0).SumAsync(), 2);
                                double freeChips = Math.Round(balance + totalExp, 2);
                                if (freeChips >= 0)
                                {
                                    // userObj.Exposure = totalExp;
                                    betModel.UserId = id;
                                    betModel.ParentId = userObj.ParentId;
                                    betModel.MasterId = userObj.MasterId;
                                    betModel.AdminId = userObj.AdminId;
                                    betModel.SuperId = userObj.SuperId;
                                    betModel.UserName = userObj.UserId;
                                    betModel.SuperId = userObj.SuperId;
                                    betModel.createdOn = DateTime.Now;
                                    betModel.IpAddress = userObj.IpAddress;
                                    db.Bet.Add(betModel);
                                    await db.SaveChangesAsync();
                                    betResponseDTO.Status = true;
                                    betResponseDTO.FreeChips = freeChips;
                                    betResponseDTO.Exp = totalExp;
                                    if (betModel.EventName == "Table Casino")
                                    {
                                        betResponseDTO.Bets = await db.Bet.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.UserId == id && !x.deleted && x.BetStatus == "Pending" && x.MarketId == betModel.MarketId).ToListAsync();

                                    }
                                    else
                                    {
                                        betResponseDTO.Bets = await db.Bet.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.UserId == id && !x.deleted && x.BetStatus == "Pending").ToListAsync();

                                    }


                                }
                                else
                                {
                                    betResponseDTO.Status = false;
                                    betResponseDTO.Result = "Low Balance.";
                                }
                            }
                            else
                            {
                                betResponseDTO.Status = false;
                                betResponseDTO.Result = "Low Balance.";
                            }
                        }
                    }
                    else
                    {
                        betResponseDTO.Status = false;
                        betResponseDTO.Result = "Market Bet Blocked.";
                    }
                }
                else
                {
                    betResponseDTO.Status = false;
                    betResponseDTO.Result = "Bet Locked..Please contact to upperline.";
                }

                //}
                //else
                //{
                //    betResponseDTO.Status = false;
                //    betResponseDTO.Result = "UnAuthorized Request.";
                //}

            }
            catch (Exception ex)
            {
                betResponseDTO.Status = false;
                betResponseDTO.Result = ex.Message;
            }
            return Ok(betResponseDTO);
        }

        [HttpPost]
        [Route("CreateFancyBet")]
        public async Task<IHttpActionResult> PostFancyBetModel(BetModel betModel)
        {
            try
            {
                string runTimeToken = _runtime.RunTimeToken();
                int id = Convert.ToInt32(_runtime.RunTimeUserId());
                var userObj = await db.SignUp.AsNoTracking().FirstOrDefaultAsync(x => x.id == id && !x.deleted);
                //if (userObj != null && userObj.jwtToken == runTimeToken)
                //{
                if (!userObj.status && !userObj.BetStatus)
                {
                    BetSettingDTO parentObj = new BetSettingDTO()
                    {
                        ParentId = userObj.ParentId,
                        SuperId = userObj.SuperId,
                        MasterId = userObj.MasterId,
                        AdminId = userObj.AdminId,
                        Stake = betModel.Stake
                    };
                    responseDTO = await checkSettings(betModel.MarketName, betModel.EventId, betModel.MarketId, parentObj);
                    if (responseDTO.Status)
                    {
                        responseDTO = await CheckRates(betModel);
                        if (betModel.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]))
                        {
                            responseDTO = await CheckRates(betModel);
                            if (!responseDTO.Status)
                            {
                                return Ok(responseDTO);
                            }
                        }
                        if (betModel.MarketName == "Fancy")
                        {
                            if (betModel.BetType == "Yes")
                            {
                                betModel.Profit = Math.Round(betModel.Stake * betModel.Price / 100, 2);
                                betModel.Exposure = betModel.Stake;
                            }
                            else
                            {
                                betModel.Exposure = Math.Round(betModel.Stake * betModel.Price / 100, 2);
                                betModel.Profit = betModel.Stake;
                            }
                        }
                        else
                        {
                            betModel.Profit = Math.Round(betModel.Stake * betModel.Price / 100, 2);
                            betModel.Exposure = betModel.Stake;
                        }

                        double price = betModel.Price;
                        double stack = betModel.Stake;
                        var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.MarketId == betModel.MarketId && x.RunnerId == betModel.RunnerId && x.UserId == id && !x.deleted).ToListAsync();
                        userBetModel.Add(betModel);
                        if (userBetModel.Count > 0)
                        {
                            double exposure = 0;
                            double yesSum = 0;
                            double noSum = 0;
                            for (int i = 0; i < userBetModel.Count; i++)
                            {
                                if (userBetModel[i].BetType == "Yes")
                                {
                                    for (int j = 0; j < userBetModel.Count; j++)
                                    {
                                        if (userBetModel[j].BetType == "No")
                                        {
                                            if (userBetModel[i].Odds <= userBetModel[j].Odds)
                                            {
                                                double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                                noSum = noSum + backVal;
                                                userBetModel[j].Stake = 0;
                                                userBetModel[j].Price = 0;
                                            }
                                            else
                                            {
                                                double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                yesSum = yesSum + backVal;
                                                userBetModel[j].Price = 0;
                                                userBetModel[j].Stake = 0;
                                            }
                                        }
                                        else
                                        {
                                            double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                            yesSum = yesSum + backVal;
                                            userBetModel[j].Stake = 0;
                                            userBetModel[j].Price = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    for (int j = 0; j < userBetModel.Count; j++)
                                    {
                                        if (userBetModel[j].BetType == "Yes")
                                        {
                                            if (userBetModel[i].Odds >= userBetModel[j].Odds)
                                            {
                                                double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                yesSum = yesSum + backVal;
                                                userBetModel[i].Price = 0;
                                                userBetModel[j].Stake = 0;

                                            }
                                            else
                                            {
                                                double backVal = userBetModel[j].Stake;//userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                noSum = noSum + backVal;
                                                userBetModel[j].Price = 0;
                                                userBetModel[j].Stake = 0;

                                            }

                                        }
                                        else
                                        {
                                            double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                            noSum = noSum + backVal;
                                            userBetModel[j].Price = 0;
                                            userBetModel[j].Stake = 0;
                                        }
                                    }
                                }
                            }

                            exposure = yesSum - noSum;
                            if (exposure > 0)
                            {
                                exposure = -exposure;
                            }
                            betModel.Stake = stack;
                            betModel.Price = price;
                            var expObj = await db.Exposure.Where(x => x.UserId == id && !x.deleted).ToListAsync();
                            if (expObj.Count > 0)
                            {
                                var mrktExp = expObj.Where(x => x.MarketId == betModel.MarketId && x.RunnerId == betModel.RunnerId && x.EventId == betModel.EventId).FirstOrDefault();
                                if (mrktExp != null)
                                {
                                    mrktExp.Exposure = exposure;
                                }
                                else
                                {
                                    ExposureModel expMdl = new ExposureModel()
                                    {
                                        UserId = id,
                                        MarketId = betModel.MarketId,
                                        EventId = betModel.EventId,
                                        MarketName = betModel.MarketName,
                                        Exposure = exposure,
                                        RunnerId = betModel.RunnerId,
                                        createdOn = DateTime.Now,
                                        ParentId = userObj.ParentId,
                                        MasterId = userObj.MasterId,
                                        AdminId = userObj.AdminId,
                                        SuperId = userObj.SuperId,
                                    };
                                    db.Exposure.Add(expMdl);
                                    expObj.Add(expMdl);
                                }
                            }
                            else
                            {
                                ExposureModel expMdl = new ExposureModel()
                                {
                                    UserId = id,
                                    MarketId = betModel.MarketId,
                                    EventId = betModel.EventId,
                                    MarketName = betModel.MarketName,
                                    Exposure = exposure,
                                    RunnerId = betModel.RunnerId,
                                    createdOn = DateTime.Now,
                                    ParentId = userObj.ParentId,
                                    MasterId = userObj.MasterId,
                                    AdminId = userObj.AdminId,
                                    SuperId = userObj.SuperId,
                                };
                                db.Exposure.Add(expMdl);
                                expObj.Add(expMdl);
                            }
                            double totalExp = expObj.Select(l => l.Exposure).DefaultIfEmpty(0).Sum();
                            if (userObj.ExposureLimit >= -totalExp)
                            {
                                double balance = Math.Round(await db.Transaction.Where(x => x.UserId == id).Select(s => s.Amount).DefaultIfEmpty(0).SumAsync(), 2);
                                double freeChips = Math.Round(balance + totalExp, 2);
                                if (freeChips >= 0)
                                {
                                    //userObj.Exposure = totalExp;
                                    betModel.UserId = id;
                                    betModel.status = false;
                                    betModel.ParentId = userObj.ParentId;
                                    betModel.MasterId = userObj.MasterId;
                                    betModel.AdminId = userObj.AdminId;
                                    betModel.SuperId = userObj.SuperId;
                                    betModel.UserName = userObj.UserId;
                                    betModel.SuperId = userObj.SuperId;
                                    betModel.createdOn = DateTime.Now;
                                    betModel.IpAddress = userObj.IpAddress;
                                    db.Bet.Add(betModel);
                                    await db.SaveChangesAsync();
                                    betResponseDTO.Status = true;
                                    betResponseDTO.FreeChips = freeChips;
                                    betResponseDTO.Exp = totalExp;
                                    betResponseDTO.Bets = await db.Bet.AsNoTracking().Where(x => x.EventId == betModel.EventId && x.UserId == id && !x.deleted && x.BetStatus == "Pending").ToListAsync();

                                }
                                else
                                {
                                    betResponseDTO.Status = false;
                                    betResponseDTO.Result = "Low Balance.";
                                }
                            }
                            else
                            {
                                betResponseDTO.Status = false;
                                betResponseDTO.Result = "Exposure Limit Exceeded.";
                            }


                        }
                    }
                    else
                    {
                        betResponseDTO.Status = false;
                        betResponseDTO.Result = "Market Bet Locked";
                    }
                }
                else
                {
                    betResponseDTO.Status = false;
                    betResponseDTO.Result = "Bet Locked..Please contact to upperline.";
                }
                //}
                //else
                //{
                //    betResponseDTO.Status = false;
                //    betResponseDTO.Result = "UnAuthorized Request.";
                //}


            }
            catch (Exception ex)
            {
                betResponseDTO.Status = false;
                betResponseDTO.Result = ex.Message;
            }
            return Ok(betResponseDTO);
        }



        [HttpGet]
        [Route("CheckSettings")]
        public async Task<ResponseDTO> checkSettings(string marketName, string eventId, string marketId, BetSettingDTO obj)
        {
            string mrktname = "";
            if (marketName == "Fancy" || marketName == "Last Digit Session")
            {
                mrktname = "Match Odds";
            }
            else
            {
                mrktname = marketName;
            }

            var checkData = await (from m in db.Market
                                   where m.EventId == eventId &&
                                   m.marketName == mrktname
                                   from e in db.Event
                                   where e.EventId == eventId
                                   select new
                                   {
                                       e.EventTime,
                                       eSts = e.status,
                                       fSta = e.EventFancy,
                                       mSts = m.status,
                                       m.MaxStake,
                                       m.MinStake,
                                       lDigit = e.IsLastDigit,
                                   }).FirstOrDefaultAsync();
            if (checkData != null)
            {
                if (checkData.eSts)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Event Bet Is Blocked By UpperLine";
                    return responseDTO;
                }
                else if (marketName == "Fancy")
                {
                    if (checkData.fSta)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Fancy Bet Is Blocked By UpperLine";
                        return responseDTO;
                    }
                }
                else if (marketName == "Last Digit Session")
                {
                    if (checkData.lDigit)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Fancy Bet Is Blocked By UpperLine";
                        return responseDTO;
                    }
                }

                else
                {
                    if (mrktname == "To Win the Toss")
                    {
                        TimeSpan diff = checkData.EventTime - DateTime.Now;
                        if (diff.TotalHours < 1)
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Market Bet Is Locked";
                            return responseDTO;
                        }
                    }
                }
                if (checkData.mSts)
                {
                    if (marketName != "Fancy")
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Market Bet Is Blocked By UpperLine";
                        return responseDTO;
                    }
                    else
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Condition Pass";
                        return responseDTO;
                    }
                }
                else
                {
                    var checkPrntEvt = await (from e in db.BlockEvent
                                              where e.EventId == eventId &&
                                              (e.UserId == obj.ParentId || e.UserId == obj.MasterId || e.UserId == obj.AdminId || e.UserId == obj.SuperId)
                                              select new
                                              {
                                                  e.EventRate,
                                                  e.EventFancy
                                              }).FirstOrDefaultAsync();
                    if (checkPrntEvt != null)
                    {
                        if (checkPrntEvt.EventRate)
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Event Bet Is Blocked By UpperLine";
                            return responseDTO;
                        }
                        else if (marketName == "Fancy")
                        {
                            if (checkPrntEvt.EventFancy)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Fancy Bet Is Blocked By UpperLine";
                                return responseDTO;
                            }
                        }
                    }
                    var checkPrntMrkt = await (from e in db.BlockMarket
                                               where e.EventId == eventId && e.MarketId == marketId &&
                                               (e.UserId == obj.ParentId || e.UserId == obj.MasterId || e.UserId == obj.AdminId || e.UserId == obj.SuperId)
                                               select new
                                               {
                                                   e.status,
                                               }).FirstOrDefaultAsync();
                    if (checkPrntMrkt != null)
                    {
                        if (checkPrntMrkt.status)
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Market Bet Is Blocked By UpperLine";
                            return responseDTO;
                        }
                        else
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = "Condition Pass";
                            return responseDTO;
                        }
                    }
                    else
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Condition Pass";
                        return responseDTO;
                    }
                }

            }
            return responseDTO;
        }

        [HttpGet]
        [Route("GetBetsHistory")]
        public async Task<IHttpActionResult> getHistory(int skipRec, int takeRecord, int sportsId, string marketName, string betStatus, string role, int userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                double count = 0;
                dynamic betModel;
                switch (role)
                {
                    case "SuperAdmin":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 3:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "Admin":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 3:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.AdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "SubAdmin":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();

                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();

                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();

                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SubAdminId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "SuperMaster":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperMasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "Master":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.MasterId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "SuperAgent":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.SuperAgentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "Agent":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.ParentId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
                        }
                        break;
                    case "Client":
                        switch (sportsId)
                        {
                            //All
                            case 0:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;

                                }
                                break;
                            // Casino
                            case 5:
                                break;
                            // Exchange Sports(Cricket,Football,Tennis)
                            default:
                                switch (marketName)
                                {
                                    case "All":
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();
                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                    default:
                                        if (betStatus == "All")
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();

                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        else
                                        {
                                            betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted).Select(b => new
                                            {
                                                b.id,
                                                b.UserName,
                                                b.EventName,
                                                b.MarketName,
                                                b.RunnerName,
                                                b.Odds,
                                                b.Stake,
                                                b.Profit,
                                                b.createdOn,
                                                b.Result,
                                                b.SportsId,
                                                b.BetType,
                                            }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRecord).ToListAsync();

                                            count = await db.Bet.CountAsync(x => x.UserId == userId && x.BetType == betStatus && x.BetStatus != "Pending" && x.MarketName == marketName && x.SportsId == sportsId && x.createdOn >= startDate && x.createdOn <= endDate && !x.deleted);
                                        }
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = count;
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                        break;
                                }
                                break;
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
        [Route("GetEventBets")]
        public async Task<IHttpActionResult> getEventBets(string userName, string role, int userId, string marketName, string eventId, int skipRec, int take)
        {
            try
            {
                dynamic betModel;
                switch (role)
                {
                    case "SuperAdmin":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.EventId == eventId && !x.deleted & x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;
                            default:

                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;

                        }
                        break;
                    case "Admin":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.EventId == eventId && !x.deleted & x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;
                            default:

                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;

                        }
                        break;
                    case "SubAdmin":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;
                            default:
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;

                        }
                        break;
                    case "SuperMaster":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;

                        }
                        break;
                    case "Master":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;

                        }
                        break;
                    case "SuperAgent":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;

                        }
                        break;
                    case "Agent":
                        switch (marketName)
                        {
                            case "All":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.BetType,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;

                        }
                        break;
                    case "Client":
                        switch (marketName)
                        {
                            case "All":
                                betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.EventId == eventId && !x.deleted).Select(b => new
                                {
                                    b.UserName,
                                    b.EventName,
                                    b.MarketName,
                                    b.RunnerName,
                                    b.Odds,
                                    b.Stake,
                                    b.BetType,
                                    b.Profit,
                                    b.createdOn,
                                    b.Result,
                                }).ToListAsync();
                                if (betModel.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = betModel;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = betModel;
                                }
                                break;
                            default:
                                betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                {
                                    b.UserName,
                                    b.EventName,
                                    b.MarketName,
                                    b.RunnerName,
                                    b.Odds,
                                    b.BetType,
                                    b.Stake,
                                    b.Profit,
                                    b.createdOn,
                                    b.Result,
                                }).ToListAsync();
                                if (betModel.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = betModel;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = betModel;
                                }
                                break;
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
        [Route("GetMarketBets")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> getMarketBets(string userName, string role, int userId, string marketName, string eventId, string runId, string marketId)
        {
            try
            {
                dynamic betModel;
                switch (role)
                {
                    case "SuperAdmin":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;

                        }
                        break;
                    case "Admin":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;

                        }
                        break;
                    case "SubAdmin":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;

                        }
                        break;
                    case "SuperMaster":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {

                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;

                        }
                        break;
                    case "Master":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.Odds,
                                        b.BetType,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {

                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;

                        }
                        break;
                    case "Agent":
                        switch (marketName)
                        {
                            case "Fancy":
                                if (userName == "Null")
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted);
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                    {
                                        b.id,
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {

                                        betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                else
                                {
                                    if (userName == "Null")
                                    {
                                        betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted);
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                    else
                                    {

                                        betModel = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower())).Select(b => new
                                        {
                                            b.id,
                                            b.UserName,
                                            b.EventName,
                                            b.MarketName,
                                            b.RunnerName,
                                            b.Odds,
                                            b.BetType,
                                            b.Stake,
                                            b.Profit,
                                            b.createdOn,
                                            b.Result,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).ToListAsync();
                                        if (betModel.Count > 0)
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = betModel;
                                            responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted && x.UserName.ToLower().Contains(userName.ToLower()));
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = betModel;
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "Client":
                        switch (marketName)
                        {
                            case "Fancy":
                                betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.EventId == eventId && x.RunnerId == runId && !x.deleted).Select(b => new
                                {
                                    b.UserName,
                                    b.EventName,
                                    b.MarketName,
                                    b.BetType,
                                    b.RunnerName,
                                    b.Odds,
                                    b.Stake,
                                    b.Profit,
                                    b.createdOn,
                                    b.Result,
                                    b.IpAddress,
                                }).ToListAsync();
                                if (betModel.Count > 0)
                                {
                                    responseDTO.Status = true;
                                    responseDTO.Result = betModel;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = betModel;
                                }
                                break;
                            case "Casino":

                                break;
                            default:
                                if (eventId == System.Configuration.ConfigurationManager.AppSettings["TableEventId"])
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.MarketId == marketId && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.BetType,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }
                                else
                                {
                                    betModel = await db.Bet.AsNoTracking().Where(x => x.UserId == userId && x.MarketName == marketName && x.EventId == eventId && !x.deleted).Select(b => new
                                    {
                                        b.UserName,
                                        b.EventName,
                                        b.MarketName,
                                        b.BetType,
                                        b.RunnerName,
                                        b.Odds,
                                        b.Stake,
                                        b.Profit,
                                        b.createdOn,
                                        b.Result,
                                        b.IpAddress,
                                    }).ToListAsync();
                                    if (betModel.Count > 0)
                                    {
                                        responseDTO.Status = true;
                                        responseDTO.Result = betModel;
                                    }
                                    else
                                    {
                                        responseDTO.Status = false;
                                        responseDTO.Result = betModel;
                                    }
                                }

                                break;

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
        [Route("GetPendingBets")]
        public async Task<IHttpActionResult> getPendingBets(string usrName, int sportsId, string mrktName, string betType, int skipRec, int takeRec)
        {
            try
            {
                dynamic betModels;
                int _id = Convert.ToInt32(_runtime.RunTimeUserId());
                switch (_runtime.RunTimeRole())
                {
                    case "SuperAdmin":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "Admin":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.AdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "SubAdmin":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SubAdminId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "SuperMaster":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperMasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "Master":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.MasterId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "SuperAgent":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.SuperAgentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;
                    case "Agent":
                        if (usrName == "Null")
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        //change
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (sportsId == 0)
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                            else
                            {
                                if (mrktName == "All")
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                                else
                                {
                                    if (betType == "All")
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted);
                                    }
                                    else
                                    {
                                        betModels = await db.Bet.AsNoTracking().Where(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                        {
                                            b.id,
                                            b.EventName,
                                            b.MarketName,
                                            b.UserName,
                                            b.RunnerName,
                                            b.BetType,
                                            b.Odds,
                                            b.Stake,
                                            b.Exposure,
                                            b.Profit,
                                            b.createdOn,
                                            b.IpAddress,
                                        }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                        responseDTO.Count = await db.Bet.AsNoTracking().CountAsync(x => x.ParentId == _id && x.UserName.ToLower().Contains(usrName.ToLower()) && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending");
                                    }
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
                        }
                        break;

                    case "Client":
                        if (sportsId == 0)
                        {
                            if (mrktName == "All")
                            {
                                if (betType == "All")
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.UserName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                                else
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        b.UserName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                            }
                            else
                            {
                                if (betType == "All")
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        b.UserName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).OrderByDescending(x => x.id).Skip(skipRec).Take(takeRec).ToListAsync();
                                }
                                else
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        b.UserName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Stake,
                                        b.Price,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                            }
                        }
                        else
                        {
                            if (mrktName == "All")
                            {
                                if (betType == "All")
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.SportsId == sportsId && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.UserName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                                else
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.SportsId == sportsId && !x.deleted && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        b.UserName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                            }
                            else
                            {
                                if (betType == "All")
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.SportsId == sportsId && x.MarketName == mrktName && x.BetStatus == "Pending" && !x.deleted).Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        b.UserName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                                else
                                {
                                    betModels = await db.Bet.AsNoTracking().Where(x => x.UserId == _id && x.SportsId == sportsId && !x.deleted && x.MarketName == mrktName && x.BetType == betType && x.BetStatus == "Pending").Select(b => new
                                    {
                                        b.id,
                                        b.EventName,
                                        b.MarketName,
                                        Sports = b.SportsId == 4 ? "Cricket" : b.SportsId == 1 ? "Soccer" : b.SportsId == 2 ? "Tennis" : "TableGames",
                                        b.UserName,
                                        b.RunnerName,
                                        b.BetType,
                                        b.Odds,
                                        b.Price,
                                        b.Stake,
                                        b.Exposure,
                                        b.Profit,
                                        b.createdOn,
                                        b.IpAddress,
                                    }).ToListAsync();
                                }
                            }
                        }
                        if (betModels.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = betModels;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = betModels;
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
        [Route("getFancyBook")]
        public async Task<IHttpActionResult> getFancyBook(string RunnerId, string marketId)
        {
            try
            {
                SignUpModel signUpModel = new SignUpModel();
                List<FancyCurrentPositionDTO> fancyCurrentPositions = new List<FancyCurrentPositionDTO>();
                int userId = Convert.ToInt32(_runtime.RunTimeUserId());
                double getMax = 0;
                double getMin = 0;
                signUpModel = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (_runtime.RunTimeRole())
                {
                    case "Client":
                        getMin = await db.Bet.Where(x => x.deleted == false && x.UserId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.UserId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);
                        var userBetModel = await db.Bet.Where(x => x.deleted == false && x.UserId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModel.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }

                            foreach (var item in userBetModel)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Exposure;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Profit;
                                                j++;
                                            }

                                        }

                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Profit;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Exposure;
                                                j++;
                                            }

                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "Agent":
                        getMin = await db.Bet.Where(x => x.deleted == false && x.ParentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.ParentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);
                        var userBetModelAgent = await db.Bet.Where(x => x.deleted == false && x.ParentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelAgent.Count > 0)
                        {

                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }

                            foreach (var item in userBetModelAgent)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Exposure;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Profit;
                                                j++;
                                            }

                                        }

                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Profit;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Exposure;
                                                j++;
                                            }

                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "SuperAgent":
                        getMin = await db.Bet.Where(x => x.deleted == false && x.SuperAgentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.SuperAgentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);
                        var userBetModelSuperAgent = await db.Bet.Where(x => x.deleted == false && x.ParentId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelSuperAgent.Count > 0)
                        {

                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }

                            foreach (var item in userBetModelSuperAgent)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Exposure;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Profit;
                                                j++;
                                            }

                                        }

                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - item.Profit;
                                                j++;
                                            }
                                            else
                                            {
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + item.Exposure;
                                                j++;
                                            }

                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "Master":
                        getMin = await db.Bet.Where(x => x.deleted == false && x.MasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.MasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);

                        var userBetModelMaster = await db.Bet.Where(x => x.deleted == false && x.MasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelMaster.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }
                            foreach (var item in userBetModelMaster)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }

                                        }
                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin; i <= getMax; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "SuperMaster":
                        getMin = await db.Bet.Where(x => x.deleted == false && x.SuperMasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.SuperMasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);

                        var userBetModelSuperMaster = await db.Bet.Where(x => x.deleted == false && x.MasterId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelSuperMaster.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }
                            foreach (var item in userBetModelSuperMaster)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }

                                        }
                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin; i <= getMax; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "SubAdmin":
                        var signUpModelSubAdmin = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                        getMin = await db.Bet.Where(x => x.deleted == false && x.SubAdminId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.SubAdminId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);

                        var BetubAdmin = await db.Bet.Where(x => x.deleted == false && x.AdminId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (BetubAdmin.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }
                            foreach (var item in BetubAdmin)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }

                                        }
                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin; i <= getMax; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = signUpModel.Share * item.Profit / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = signUpModel.Share * item.Exposure / 100;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "Admin":
                        var signUpModelAdmin = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                        getMin = await db.Bet.Where(x => x.deleted == false && x.AdminId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.AdminId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);
                        var userBetModelAdmin = await db.Bet.Where(x => x.deleted == false && x.SuperId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelAdmin.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }

                            foreach (var item in userBetModelAdmin)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = item.Exposure;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = item.Profit;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }

                                        }
                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin; i <= getMax; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = item.Profit;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = item.Exposure;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "SuperAdmin":
                        var signUpModelSuperAdmin = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                        getMin = await db.Bet.Where(x => x.deleted == false && x.SuperId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MinAsync(x => x.Odds);
                        getMax = await db.Bet.Where(x => x.deleted == false && x.SuperId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).MaxAsync(x => x.Odds);
                        var userBetModelSuperAdmin = await db.Bet.Where(x => x.deleted == false && x.SuperId == userId && x.MarketId == marketId && x.RunnerId == RunnerId).ToListAsync();
                        if (userBetModelSuperAdmin.Count > 0)
                        {
                            for (double i = getMin - 3; i <= getMax + 5; i++)
                            {
                                FancyCurrentPositionDTO fancyCurrentPosition = new FancyCurrentPositionDTO()
                                {
                                    RunValue = 0,
                                    Pl = 0,
                                };
                                fancyCurrentPositions.Add(fancyCurrentPosition);
                            }

                            foreach (var item in userBetModelSuperAdmin)
                            {
                                int j = 0;
                                if (item.BetType == "Yes")
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin - 3; i <= getMax + 5; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = item.Exposure;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = item.Profit;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }

                                        }
                                    }

                                }
                                else
                                {
                                    if (item.Odds >= getMin)
                                    {
                                        for (double i = getMin; i <= getMax; i++)
                                        {
                                            if (i < item.Odds)
                                            {
                                                double pl = item.Profit;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl - pl;
                                                j++;
                                            }
                                            else
                                            {
                                                double pl = item.Exposure;
                                                fancyCurrentPositions[j].RunValue = i;
                                                fancyCurrentPositions[j].Pl = fancyCurrentPositions[j].Pl + pl;
                                                j++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                }
                if (fancyCurrentPositions.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = fancyCurrentPositions;
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
        [Route("DeleteBets")]
        public async Task<IHttpActionResult> deleteBets(int id, string Pwd)
        {
            try
            {
                int _id = Convert.ToInt32(_runtime.RunTimeUserId());
                string role = _runtime.RunTimeRole();
                if (role == "SuperAdmin")
                {

                    var signObj = await db.SignUp.AsNoTracking().Select(x => new { x.id, x.Password }).FirstOrDefaultAsync(x => x.id == _id);
                    if (signObj != null)
                    {
                        if (signObj.Password == Pwd)
                        {
                            var bet = await db.Bet.FirstOrDefaultAsync(x => x.id == id && !x.deleted);
                            if (bet != null)
                            {
                                bet.deleted = true;
                                bet.BetStatus = "Deleted";
                                await db.SaveChangesAsync();
                                if (bet.MarketName == "Fancy")
                                {
                                    var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketId == bet.MarketId && x.RunnerId == bet.RunnerId && x.UserId == bet.UserId && !x.deleted).ToListAsync();

                                    if (userBetModel.Count > 0)
                                    {
                                        double exposure = 0;
                                        double yesSum = 0;
                                        double noSum = 0;
                                        for (int i = 0; i < userBetModel.Count; i++)
                                        {
                                            if (userBetModel[i].BetType == "Yes")
                                            {
                                                for (int j = 0; j < userBetModel.Count; j++)
                                                {
                                                    if (userBetModel[j].BetType == "No")
                                                    {
                                                        if (userBetModel[i].Odds <= userBetModel[j].Odds)
                                                        {
                                                            double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                                            noSum = noSum + backVal;
                                                            userBetModel[j].Stake = 0;
                                                            userBetModel[j].Price = 0;
                                                        }
                                                        else
                                                        {
                                                            double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                            yesSum = yesSum + backVal;
                                                            userBetModel[j].Price = 0;
                                                            userBetModel[j].Stake = 0;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                                        yesSum = yesSum + backVal;
                                                        userBetModel[j].Stake = 0;
                                                        userBetModel[j].Price = 0;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                for (int j = 0; j < userBetModel.Count; j++)
                                                {
                                                    if (userBetModel[j].BetType == "Yes")
                                                    {
                                                        if (userBetModel[i].Odds >= userBetModel[j].Odds)
                                                        {
                                                            double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                            yesSum = yesSum + backVal;
                                                            userBetModel[i].Price = 0;
                                                            userBetModel[j].Stake = 0;

                                                        }
                                                        else
                                                        {
                                                            double backVal = userBetModel[j].Stake;//userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                            noSum = noSum + backVal;
                                                            userBetModel[j].Price = 0;
                                                            userBetModel[j].Stake = 0;

                                                        }

                                                    }
                                                    else
                                                    {
                                                        double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                        noSum = noSum + backVal;
                                                        userBetModel[j].Price = 0;
                                                        userBetModel[j].Stake = 0;
                                                    }
                                                }
                                            }
                                        }

                                        exposure = yesSum - noSum;
                                        if (exposure > 0)
                                        {
                                            exposure = -exposure;
                                        }
                                        var expObj = await db.Exposure.FirstOrDefaultAsync(x => x.UserId == bet.UserId && !x.deleted && x.EventId == bet.EventId && x.RunnerId == bet.RunnerId);
                                        if (expObj != null)
                                        {
                                            expObj.Exposure = exposure;
                                            await db.SaveChangesAsync();
                                        }
                                        responseDTO.Status = true;
                                        responseDTO.Result = "Done";
                                    }
                                    else
                                    {
                                        var expObj = await db.Exposure.FirstOrDefaultAsync(x => x.UserId == bet.UserId && !x.deleted && x.EventId == bet.EventId && x.RunnerId == bet.RunnerId);
                                        if (expObj != null)
                                        {
                                            expObj.Exposure = 0;
                                            expObj.deleted = true;
                                            await db.SaveChangesAsync();
                                        }
                                        responseDTO.Status = true;
                                        responseDTO.Result = "Done";
                                    }
                                }
                                else
                                {
                                    var rnrMrkt = await db.Runner.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketName == bet.MarketName).ToListAsync();
                                    List<BookDTO> bookObj = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            RunnerId = rnr.id,
                                            ProfitLoss = 0,
                                        };
                                        bookObj.Add(bookDto);
                                    }
                                    var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.UserId == bet.UserId && !x.deleted).ToListAsync();
                                    if (userBetModel.Count > 0)
                                    {
                                        foreach (var item in userBetModel)
                                        {
                                            foreach (var itemBk in bookObj)
                                            {
                                                if (itemBk.RunnerId == Convert.ToInt32(item.RunnerId))
                                                {
                                                    if (item.BetType == "Back")
                                                    {
                                                        itemBk.ProfitLoss += item.Profit;
                                                    }
                                                    else
                                                    {
                                                        itemBk.ProfitLoss += -item.Exposure;
                                                    }
                                                }
                                                else
                                                {
                                                    if (item.BetType == "Back")
                                                    {
                                                        itemBk.ProfitLoss += -item.Exposure;
                                                    }
                                                    else
                                                    {
                                                        itemBk.ProfitLoss += item.Profit;
                                                    }

                                                }
                                            }
                                        }

                                        var expObj = await db.Exposure.FirstOrDefaultAsync(x => x.UserId == bet.UserId && !x.deleted && x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.EventId == bet.EventId);
                                        if (expObj != null)
                                        {
                                            expObj.Exposure = bookObj.Min(x => x.ProfitLoss);
                                            await db.SaveChangesAsync();
                                            responseDTO.Status = true;
                                            responseDTO.Result = "Done";

                                        }
                                    }
                                    else
                                    {
                                        var mrktExp = await db.Exposure.FirstOrDefaultAsync(x => x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.EventId == bet.EventId && x.UserId == bet.UserId && !x.deleted);
                                        if (mrktExp != null)
                                        {
                                            mrktExp.Exposure = 0;
                                            mrktExp.deleted = true;
                                            await db.SaveChangesAsync();
                                            responseDTO.Status = true;
                                            responseDTO.Result = "Done";
                                        }
                                        else
                                        {
                                            responseDTO.Status = true;
                                            responseDTO.Result = "Done";
                                        }
                                    }

                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Wrong Password";
                        }
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "You have no authority to delete any kind of bets";
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
        [Route("GetDeletedBets")]
        public async Task<IHttpActionResult> getDeletedBets(int sportsId, string eventId, string marketName,string runnerId)
        {
            try
            {
                int _id = Convert.ToInt32(_runtime.RunTimeUserId());
                string role = _runtime.RunTimeRole();
                if (role == "SuperAdmin")
                {
                    var getDeletBetsObj = await db.Bet.AsNoTracking().Where(x => x.SportsId == sportsId && x.BetStatus == "Deleted" && x.EventId == eventId && x.MarketName == marketName).OrderByDescending(x => x.id).ToListAsync();
                    if (getDeletBetsObj.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = marketName=="Fancy"?getDeletBetsObj.Where(x=>x.RunnerId==runnerId): getDeletBetsObj;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = getDeletBetsObj;
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "You have no authority";
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
        [Route("RecoverDeleteBets")]
        public async Task<IHttpActionResult> recoverDeleteBets(List<int> id, string eventId, string marketName, string runnerId, string Pwd)
        {
            int _id = Convert.ToInt32(_runtime.RunTimeUserId());
            string role = _runtime.RunTimeRole();
            if (role == "SuperAdmin")
            {
                if (marketName == "Fancy")
                {
                    var checkFancy = await db.Bet.FirstOrDefaultAsync(x => x.EventId == eventId && x.RunnerId == runnerId && !x.deleted && (x.BetStatus == "Win" || x.BetStatus == "Lost"));
                    if (checkFancy != null)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Please rollback the fancy " + checkFancy.RunnerName;
                        return Ok(responseDTO);
                    }
                }
                else
                {
                    var checkEvent = await db.Event.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == eventId && x.deleted);
                    if (checkEvent != null)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Please rollback the market first";
                        return Ok(responseDTO);
                    }
                }
                using (var dbContextTransaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var signObj = await db.SignUp.AsNoTracking().Select(x => new { x.id, x.Password }).Where(x => x.id == _id).FirstOrDefaultAsync();
                        if (signObj != null)
                        {
                            if (signObj.Password == Pwd)
                            {
                                if (id.Count > 0)
                                {
                                    foreach (var itemId in id)
                                    {
                                        var bet = await db.Bet.Where(x => x.id == itemId && x.deleted).FirstOrDefaultAsync();
                                        if (bet != null)
                                        {
                                            if (bet.MarketName == "Fancy")
                                            {

                                                bet.deleted = false;
                                                bet.BetStatus = "Pending";
                                                await db.SaveChangesAsync();
                                                var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketId == bet.MarketId && x.RunnerId == bet.RunnerId && x.UserId == bet.UserId && !x.deleted).ToListAsync();

                                                if (userBetModel.Count > 0)
                                                {
                                                    double exposure = 0;
                                                    double yesSum = 0;
                                                    double noSum = 0;
                                                    for (int i = 0; i < userBetModel.Count; i++)
                                                    {
                                                        if (userBetModel[i].BetType == "Yes")
                                                        {
                                                            for (int j = 0; j < userBetModel.Count; j++)
                                                            {
                                                                if (userBetModel[j].BetType == "No")
                                                                {
                                                                    if (userBetModel[i].Odds <= userBetModel[j].Odds)
                                                                    {
                                                                        double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                                                        noSum = noSum + backVal;
                                                                        userBetModel[j].Stake = 0;
                                                                        userBetModel[j].Price = 0;
                                                                    }
                                                                    else
                                                                    {
                                                                        double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                                        yesSum = yesSum + backVal;
                                                                        userBetModel[j].Price = 0;
                                                                        userBetModel[j].Stake = 0;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    double backVal = userBetModel[j].Stake; //userBetModel[j].Stake * userBetModel[j].Price / 100;
                                                                    yesSum = yesSum + backVal;
                                                                    userBetModel[j].Stake = 0;
                                                                    userBetModel[j].Price = 0;
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            for (int j = 0; j < userBetModel.Count; j++)
                                                            {
                                                                if (userBetModel[j].BetType == "Yes")
                                                                {
                                                                    if (userBetModel[i].Odds >= userBetModel[j].Odds)
                                                                    {
                                                                        double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                                        yesSum = yesSum + backVal;
                                                                        userBetModel[i].Price = 0;
                                                                        userBetModel[j].Stake = 0;

                                                                    }
                                                                    else
                                                                    {
                                                                        double backVal = userBetModel[j].Stake;//userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                                        noSum = noSum + backVal;
                                                                        userBetModel[j].Price = 0;
                                                                        userBetModel[j].Stake = 0;
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    double backVal = userBetModel[j].Price * userBetModel[j].Stake / 100;
                                                                    noSum = noSum + backVal;
                                                                    userBetModel[j].Price = 0;
                                                                    userBetModel[j].Stake = 0;
                                                                }
                                                            }
                                                        }
                                                    }

                                                    exposure = yesSum - noSum;
                                                    if (exposure > 0)
                                                    {
                                                        exposure = -exposure;
                                                    }
                                                    var expObj = await db.Exposure.FirstOrDefaultAsync(x => x.UserId == bet.UserId && !x.deleted && x.EventId == bet.EventId && x.RunnerId == bet.RunnerId);
                                                    if (expObj != null)
                                                    {
                                                        expObj.Exposure = exposure;
                                                        await db.SaveChangesAsync();
                                                    }
                                                }
                                                else
                                                {
                                                    var expObj = await db.Exposure.FirstOrDefaultAsync(x => x.UserId == bet.UserId && !x.deleted && x.EventId == bet.EventId && x.RunnerId == bet.RunnerId);
                                                    if (expObj != null)
                                                    {
                                                        expObj.Exposure = 0;
                                                        expObj.deleted = true;
                                                        await db.SaveChangesAsync();
                                                    }

                                                }
                                            }
                                            else
                                            {

                                                bet.BetStatus = "Pending";
                                                bet.deleted = false;
                                                await db.SaveChangesAsync();
                                                var rnrMrkt = await db.Runner.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketName == bet.MarketName).ToListAsync();
                                                List<BookDTO> bookObj = new List<BookDTO>();
                                                foreach (var rnr in rnrMrkt)
                                                {
                                                    BookDTO bookDto = new BookDTO()
                                                    {
                                                        RunnerId = rnr.id,
                                                        ProfitLoss = 0,
                                                    };
                                                    bookObj.Add(bookDto);
                                                }
                                                var userBetModel = await db.Bet.AsNoTracking().Where(x => x.EventId == bet.EventId && x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.UserId == bet.UserId && !x.deleted).ToListAsync();
                                                if (userBetModel.Count > 0)
                                                {
                                                    foreach (var item in userBetModel)
                                                    {
                                                        foreach (var itemBk in bookObj)
                                                        {
                                                            if (itemBk.RunnerId == Convert.ToInt32(item.RunnerId))
                                                            {
                                                                if (item.BetType == "Back")
                                                                {
                                                                    itemBk.ProfitLoss += item.Profit;
                                                                }
                                                                else
                                                                {
                                                                    itemBk.ProfitLoss += -item.Exposure;
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (item.BetType == "Back")
                                                                {
                                                                    itemBk.ProfitLoss += -item.Exposure;
                                                                }
                                                                else
                                                                {
                                                                    itemBk.ProfitLoss += item.Profit;
                                                                }

                                                            }
                                                        }
                                                    }

                                                    var expObj = await db.Exposure.Where(x => x.UserId == bet.UserId && !x.deleted).ToListAsync();
                                                    if (expObj.Count > 0)
                                                    {
                                                        var mrktExp = expObj.Where(x => x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.EventId == bet.EventId).FirstOrDefault();
                                                        if (mrktExp != null)
                                                        {
                                                            mrktExp.Exposure = bookObj.Min(x => x.ProfitLoss);
                                                            await db.SaveChangesAsync();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    var mrktExp = await db.Exposure.FirstOrDefaultAsync(x => x.MarketId == bet.MarketId && x.MarketName == bet.MarketName && x.EventId == bet.EventId && x.UserId == bet.UserId && !x.deleted);
                                                    if (mrktExp != null)
                                                    {
                                                        mrktExp.Exposure = 0;
                                                        mrktExp.deleted = true;
                                                        await db.SaveChangesAsync();
                                                    }
                                                }

                                            }
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = "No bet found";
                                        }
                                    }
                                    dbContextTransaction.Commit();
                                    responseDTO.Status = true;
                                    responseDTO.Result = "Done";
                                }
                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Wrong Password";
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
            }
            else
            {
                responseDTO.Status = false;
                responseDTO.Result = "You have no authority to recover any kind of bets";
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetPosition")]
        public async Task<IHttpActionResult> getPosition(string role, int userId, string marketId, string eventId, string marketName)
        {
            try
            {
                ArrayList bookDTOList = new ArrayList();
                List<BookDTO> bookDTOs = new List<BookDTO>();
                var rnrMrkt = await db.Runner.AsNoTracking().Where(x => x.EventId == eventId && x.MarketName == marketName && x.MarketId == marketId).ToListAsync();
                var requestSignObj = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                List<BetModel> allBets = new List<BetModel>();
                List<BetModel> betModel = new List<BetModel>();
                switch (role)
                {
                    case "Admin":
                        allBets = await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName && !x.deleted).ToListAsync();
                        if (allBets.Count > 0)
                        {
                            betModel = allBets.GroupBy(x => x.AdminId).Select(x => x.FirstOrDefault()).ToList();
                            if (betModel.Count > 0)
                            {
                                foreach (var betObj in betModel)
                                {
                                    if (betObj.AdminId == 0)
                                    {
                                        var bets = allBets.Where(x => x.AdminId == 0).GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                                        foreach (var item in bets)
                                        {
                                            var groupBets = allBets.Where(x => x.UserId == item.UserId).ToList();
                                            if (groupBets.Count > 0)
                                            {
                                                var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == item.UserId).FirstOrDefaultAsync();
                                                List<BookDTO> bookObj = new List<BookDTO>();
                                                foreach (var rnr in rnrMrkt)
                                                {
                                                    BookDTO bookDto = new BookDTO()
                                                    {
                                                        RunnerId = rnr.id,
                                                        ProfitLoss = 0,
                                                        totalSum = 0,

                                                    };
                                                    bookObj.Add(bookDto);
                                                }
                                                foreach (var grpObj in groupBets)
                                                {
                                                    foreach (var itemBk in bookObj)
                                                    {
                                                        itemBk.Role = "Client";
                                                        itemBk.UserId = grpObj.id;
                                                        itemBk.UserName = signObj.UserId;
                                                        if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                        {
                                                            if (grpObj.BetType == "Back")
                                                            {
                                                                itemBk.totalSum += -grpObj.Profit;
                                                                itemBk.ProfitLoss += -grpObj.Profit;
                                                            }
                                                            else
                                                            {
                                                                itemBk.totalSum += grpObj.Exposure;
                                                                itemBk.ProfitLoss += grpObj.Exposure;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (grpObj.BetType == "Back")
                                                            {
                                                                itemBk.totalSum += grpObj.Exposure;
                                                                itemBk.ProfitLoss += grpObj.Exposure;
                                                            }
                                                            else
                                                            {
                                                                itemBk.totalSum += -grpObj.Profit;
                                                                itemBk.ProfitLoss += -grpObj.Profit;
                                                            }

                                                        }
                                                    }
                                                }
                                                bookDTOs.AddRange(bookObj);
                                                bookDTOList.Add(bookObj);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        var groupBets = allBets.Where(x => x.AdminId == betObj.AdminId).ToList();
                                        if (groupBets.Count > 0)
                                        {
                                            var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == betObj.AdminId).FirstOrDefaultAsync();
                                            var parentSignObj = await db.SignUp.AsNoTracking().Where(x => x.id == signObj.ParentId).FirstOrDefaultAsync();
                                            List<BookDTO> bookObj = new List<BookDTO>();
                                            foreach (var rnr in rnrMrkt)
                                            {
                                                BookDTO bookDto = new BookDTO()
                                                {
                                                    RunnerId = rnr.id,
                                                    ProfitLoss = 0,
                                                    totalSum = 0,

                                                };
                                                bookObj.Add(bookDto);
                                            }
                                            foreach (var grpObj in groupBets)
                                            {
                                                foreach (var itemBk in bookObj)
                                                {
                                                    itemBk.Role = "SubAdmin";
                                                    itemBk.UserId = grpObj.AdminId;
                                                    itemBk.UserName = signObj.UserName;
                                                    if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                    {
                                                        if (grpObj.BetType == "Back")
                                                        {
                                                            itemBk.totalSum += -grpObj.Profit;

                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }
                                                        else
                                                        {
                                                            itemBk.totalSum += grpObj.Exposure;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (grpObj.BetType == "Back")
                                                        {
                                                            itemBk.totalSum += grpObj.Exposure;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                        else
                                                        {
                                                            itemBk.totalSum += -grpObj.Profit;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }

                                                    }
                                                }
                                            }
                                            bookDTOs.AddRange(bookObj);
                                            bookDTOList.Add(bookObj);
                                        }
                                    }

                                }
                                if (bookDTOList.Count > 0)
                                {
                                    List<BookDTO> bookObj1 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Own(" + requestSignObj.UserId + " | " + requestSignObj.Role + ")",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj1.Add(bookDto);
                                    }
                                    bookDTOList.Add(bookObj1);
                                    List<BookDTO> bookObj2 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Total",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj2.Add(bookDto);
                                    }
                                    bookDTOList.Add(bookObj2);
                                    responseDTO.Status = true;
                                    responseDTO.Result = bookDTOList;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "No Data";
                                }
                            }

                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SubAdmin":
                        allBets = await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName && !x.deleted).ToListAsync();
                        if (allBets.Count > 0)
                        {
                            betModel = allBets.GroupBy(x => x.MasterId).Select(x => x.FirstOrDefault()).ToList();
                            if (betModel.Count > 0)
                            {
                                foreach (var betObj in betModel)
                                {
                                    if (betObj.MasterId == 0)
                                    {
                                        var bets = allBets.Where(x => x.MasterId == 0).GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                                        foreach (var item in bets)
                                        {
                                            var groupBets = allBets.Where(x => x.UserId == item.UserId).ToList();
                                            if (groupBets.Count > 0)
                                            {
                                                var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == item.UserId).FirstOrDefaultAsync();
                                                var parentObj = await db.SignUp.AsNoTracking().Where(x => x.id == item.ParentId).FirstOrDefaultAsync();
                                                List<BookDTO> bookObj = new List<BookDTO>();
                                                foreach (var rnr in rnrMrkt)
                                                {
                                                    BookDTO bookDto = new BookDTO()
                                                    {
                                                        RunnerId = rnr.id,
                                                        ProfitLoss = 0,
                                                        totalSum = 0,
                                                    };
                                                    bookObj.Add(bookDto);
                                                }
                                                foreach (var grpObj in groupBets)
                                                {
                                                    foreach (var itemBk in bookObj)
                                                    {
                                                        itemBk.Role = "Client";
                                                        itemBk.UserId = grpObj.id;
                                                        itemBk.UserName = signObj.UserId;
                                                        if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                        {
                                                            if (grpObj.BetType == "Back")
                                                            {
                                                                itemBk.totalSum += -grpObj.Profit;
                                                                itemBk.ProfitLoss += parentObj.Share / 100 * -grpObj.Profit;
                                                            }
                                                            else
                                                            {
                                                                itemBk.totalSum += grpObj.Exposure;
                                                                itemBk.ProfitLoss += parentObj.Share / 100 * grpObj.Exposure;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (grpObj.BetType == "Back")
                                                            {
                                                                itemBk.totalSum += grpObj.Exposure;
                                                                itemBk.ProfitLoss += parentObj.Share / 100 * grpObj.Exposure;
                                                            }
                                                            else
                                                            {
                                                                itemBk.totalSum += -grpObj.Profit;
                                                                itemBk.ProfitLoss += parentObj.Share / 100 * -grpObj.Profit;
                                                            }

                                                        }
                                                    }
                                                }
                                                bookDTOs.AddRange(bookObj);
                                                bookDTOList.Add(bookObj);
                                            }
                                        }
                                    }

                                    else
                                    {
                                        var groupBets = allBets.Where(x => x.MasterId == betObj.MasterId && x.MasterId > 0).ToList();
                                        if (groupBets.Count > 0)
                                        {
                                            var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == betObj.MasterId).FirstOrDefaultAsync();
                                            var parentSignObj = await db.SignUp.AsNoTracking().Where(x => x.id == signObj.ParentId).FirstOrDefaultAsync();

                                            List<BookDTO> bookObj = new List<BookDTO>();
                                            foreach (var rnr in rnrMrkt)
                                            {
                                                BookDTO bookDto = new BookDTO()
                                                {
                                                    RunnerId = rnr.id,
                                                    ProfitLoss = 0,
                                                    totalSum = 0,
                                                };
                                                bookObj.Add(bookDto);
                                            }
                                            foreach (var grpObj in groupBets)
                                            {
                                                foreach (var itemBk in bookObj)
                                                {
                                                    itemBk.Role = "Master";
                                                    itemBk.UserId = grpObj.MasterId;
                                                    itemBk.UserName = signObj.UserName;
                                                    if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                    {
                                                        if (grpObj.BetType == "Back")
                                                        {
                                                            itemBk.totalSum += -grpObj.Profit;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }
                                                        else
                                                        {
                                                            itemBk.totalSum += grpObj.Exposure;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (grpObj.BetType == "Back")
                                                        {
                                                            itemBk.totalSum += grpObj.Exposure;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                        else
                                                        {
                                                            itemBk.totalSum += -grpObj.Profit;
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }

                                                    }
                                                }
                                            }
                                            bookDTOs.AddRange(bookObj);
                                            bookDTOList.Add(bookObj);
                                        }
                                    }

                                }
                                if (bookDTOList.Count > 0)
                                {
                                    List<BookDTO> bookObj1 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Own(" + requestSignObj.UserId + " | " + requestSignObj.Role + ")",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj1.Add(bookDto);
                                    }
                                    List<BookDTO> bookObj2 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Total",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj2.Add(bookDto);
                                    }

                                    bookDTOList.Add(bookObj1);
                                    bookDTOList.Add(bookObj2);
                                    responseDTO.Status = true;
                                    responseDTO.Result = bookDTOList;
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
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Master":
                        allBets = await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName && !x.deleted).ToListAsync();
                        if (allBets.Count > 0)
                        {
                            betModel = allBets.GroupBy(x => x.ParentId).Select(x => x.FirstOrDefault()).ToList();
                            if (betModel.Count > 0)
                            {
                                foreach (var betObj in betModel)
                                {
                                    var groupBets = allBets.Where(x => x.ParentId == betObj.ParentId).ToList();
                                    if (groupBets.Count > 0)
                                    {
                                        var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == betObj.ParentId).FirstOrDefaultAsync();
                                        var parentSignObj = await db.SignUp.AsNoTracking().Where(x => x.id == signObj.ParentId).FirstOrDefaultAsync();


                                        List<BookDTO> bookObj = new List<BookDTO>();
                                        foreach (var rnr in rnrMrkt)
                                        {
                                            BookDTO bookDto = new BookDTO()
                                            {
                                                RunnerId = rnr.id,
                                                ProfitLoss = 0,
                                                totalSum = 0,

                                            };
                                            bookObj.Add(bookDto);
                                        }
                                        foreach (var grpObj in groupBets)
                                        {
                                            foreach (var itemBk in bookObj)
                                            {
                                                itemBk.Role = signObj.Role == "Master" ? "Client" : signObj.Role;
                                                itemBk.UserId = signObj.Role == "Master" ? 0 : grpObj.ParentId;
                                                itemBk.UserName = signObj.Role == "Master" ? grpObj.UserName : signObj.UserName;
                                                if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                {
                                                    if (grpObj.BetType == "Back")
                                                    {
                                                        itemBk.totalSum += -grpObj.Profit;
                                                        if (signObj.Role == "Agent")
                                                        {
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }
                                                        else
                                                        {
                                                            itemBk.ProfitLoss += -grpObj.Profit;
                                                        }

                                                    }
                                                    else
                                                    {
                                                        itemBk.totalSum += grpObj.Exposure;
                                                        if (signObj.Role == "Agent")
                                                        {
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                        else
                                                        {
                                                            itemBk.ProfitLoss += grpObj.Exposure;
                                                        }

                                                    }
                                                }
                                                else
                                                {
                                                    if (grpObj.BetType == "Back")
                                                    {
                                                        itemBk.totalSum += grpObj.Exposure;
                                                        if (signObj.Role == "Agent")
                                                        {
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * grpObj.Exposure;
                                                        }
                                                        else
                                                        {
                                                            itemBk.ProfitLoss += grpObj.Exposure;
                                                        }

                                                    }
                                                    else
                                                    {
                                                        if (signObj.Role == "Agent")
                                                        {
                                                            itemBk.ProfitLoss += (parentSignObj.Share - signObj.Share) / 100 * -grpObj.Profit;
                                                        }
                                                        else
                                                        {
                                                            itemBk.ProfitLoss += -grpObj.Profit;
                                                        }
                                                        itemBk.totalSum += -grpObj.Profit;
                                                    }

                                                }
                                            }
                                        }
                                        bookDTOList.Add(bookObj);
                                        bookDTOs.AddRange(bookObj);
                                    }
                                }
                                if (bookDTOList.Count > 0)
                                {
                                    List<BookDTO> bookObj1 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Own(" + requestSignObj.UserId + " | " + requestSignObj.Role + ")",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj1.Add(bookDto);
                                    }
                                    List<BookDTO> bookObj2 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Total",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj2.Add(bookDto);
                                    }

                                    bookDTOList.Add(bookObj1);
                                    bookDTOList.Add(bookObj2);
                                    responseDTO.Status = true;
                                    responseDTO.Result = bookDTOList;
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
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Agent":
                        allBets = await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId && x.MarketId == marketId && x.MarketName == marketName && !x.deleted).ToListAsync();
                        if (allBets.Count > 0)
                        {
                            betModel = allBets.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).ToList();
                            if (betModel.Count > 0)
                            {
                                foreach (var betObj in betModel)
                                {
                                    var groupBets = allBets.Where(x => x.UserId == betObj.UserId).ToList();
                                    if (groupBets.Count > 0)
                                    {
                                        var signObj = await db.SignUp.AsNoTracking().Where(x => x.id == betObj.UserId).FirstOrDefaultAsync();
                                        var parentSignObj = await db.SignUp.AsNoTracking().Where(x => x.id == signObj.ParentId).FirstOrDefaultAsync();
                                        List<BookDTO> bookObj = new List<BookDTO>();
                                        foreach (var rnr in rnrMrkt)
                                        {
                                            BookDTO bookDto = new BookDTO()
                                            {
                                                RunnerId = rnr.id,
                                                ProfitLoss = 0,
                                                totalSum = 0,

                                            };
                                            bookObj.Add(bookDto);
                                        }
                                        foreach (var grpObj in groupBets)
                                        {
                                            foreach (var itemBk in bookObj)
                                            {
                                                itemBk.Role = "Client";
                                                itemBk.UserId = grpObj.UserId;
                                                itemBk.UserName = signObj.UserName;
                                                if (itemBk.RunnerId == Convert.ToInt32(grpObj.RunnerId))
                                                {
                                                    if (grpObj.BetType == "Back")
                                                    {
                                                        itemBk.totalSum += -grpObj.Profit;
                                                        itemBk.ProfitLoss += parentSignObj.Share / 100 * -grpObj.Profit;
                                                    }
                                                    else
                                                    {
                                                        itemBk.totalSum += grpObj.Exposure;
                                                        itemBk.ProfitLoss += parentSignObj.Share / 100 * grpObj.Exposure;
                                                    }
                                                }
                                                else
                                                {
                                                    if (grpObj.BetType == "Back")
                                                    {
                                                        itemBk.totalSum += grpObj.Exposure;
                                                        itemBk.ProfitLoss += parentSignObj.Share / 100 * grpObj.Exposure;
                                                    }
                                                    else
                                                    {
                                                        itemBk.totalSum += -grpObj.Profit;
                                                        itemBk.ProfitLoss += parentSignObj.Share / 100 * -grpObj.Profit;
                                                    }

                                                }
                                            }
                                        }
                                        bookDTOs.AddRange(bookObj);
                                        bookDTOList.Add(bookObj);
                                    }
                                }
                                if (bookDTOList.Count > 0)
                                {
                                    List<BookDTO> bookObj1 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Own(" + requestSignObj.UserId + " | " + requestSignObj.Role + ")",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj1.Add(bookDto);
                                    }
                                    List<BookDTO> bookObj2 = new List<BookDTO>();
                                    foreach (var rnr in rnrMrkt)
                                    {
                                        BookDTO bookDto = new BookDTO()
                                        {
                                            UserName = "Total",
                                            ProfitLoss = bookDTOs.Where(x => x.RunnerId == rnr.id).Select(x => x.ProfitLoss).DefaultIfEmpty(0).Sum(),
                                        };

                                        bookObj2.Add(bookDto);
                                    }

                                    bookDTOList.Add(bookObj1);
                                    bookDTOList.Add(bookObj2);
                                    responseDTO.Status = true;
                                    responseDTO.Result = bookDTOList;
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
        [Route("GetEvntBets")]
        public async Task<IHttpActionResult> getPendingEvntBets(string eventId)
        {
            try
            {
                int _id = Convert.ToInt32(_runtime.RunTimeUserId());
                List<BetModel> betObj = new List<BetModel>();
                switch (_runtime.RunTimeRole())
                {
                    case "SuperAdmin":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.SuperId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Admin":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.AdminId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SubAdmin":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.SubAdminId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SuperMaster":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.SuperMasterId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Master":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.MasterId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SuperAgent":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.SuperAgentId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Agent":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.ParentId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Client":
                        betObj = await db.Bet.AsNoTracking().Where(x => x.EventId == eventId && x.UserId == _id && x.BetStatus == "Pending").ToListAsync();
                        break;
                }
                if (betObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = betObj;
                }
                else
                {
                    responseDTO.Status = false; ;
                    responseDTO.Result = betObj;
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
        [Route("GetAllPendingBetsOfEvent")]
        public async Task<IHttpActionResult> getAllPendingBetsOfEvent(string eventId)
        {
            try
            {
                int id = Convert.ToInt32(_runtime.RunTimeUserId());
                switch (_runtime.RunTimeRole())
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
                        var adminBets = await db.Bet.AsNoTracking().Where(x => x.AdminId == id && x.BetStatus == "Pending" && !x.deleted && x.EventId == eventId).Select(s => new
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
                        responseDTO.bets = adminBets;
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
                            share = s.AdminId == 0 ? 0 : db.SignUp.Where(x => x.id == s.AdminId).Select(x => x.Share).FirstOrDefault(),
                        }).ToListAsync();
                        responseDTO.bets = superAdminBets;
                        break;
                }
                responseDTO.Status = true;
                responseDTO.Result = "";
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        public async Task<ResponseDTO> CheckRates(BetModel betModel)
        {
            try
            {
                var checkEvent = await db.Event.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == betModel.EventId);
                if (checkEvent != null)
                {
                    var checkMarket = await db.Market.AsNoTracking().FirstOrDefaultAsync(x => x.MarketId == betModel.MarketId && x.EventId == betModel.EventId);
                    if (checkMarket != null)
                    {
                        var marketUrl = await db.ThirdPartyApiModel.AsNoTracking().Where(x => x.SportsId == betModel.SportsId).FirstOrDefaultAsync();
                        var client = new HttpClient();
                        client.DefaultRequestHeaders.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));

                        switch (betModel.SportsId)
                        {
                            case 1:
                            case 2:
                                var betFairResponse = await client.GetAsync(requestUri: marketUrl.BetfairUrl + betModel.SportsId + "&marketid=" + betModel.MarketId);
                                if (betFairResponse.IsSuccessStatusCode)
                                {
                                    var data = betFairResponse.Content.ReadAsStringAsync().Result;
                                    BetFairAPIDTO betFairAPIDTO = JsonConvert.DeserializeObject<BetFairAPIDTO>(data);
                                    if (betFairAPIDTO != null && (betFairAPIDTO.status == "OPEN" || betFairAPIDTO.status == "ACTIVE"))
                                    {
                                        switch (betModel.BetType)
                                        {
                                            case "Back":
                                                if (Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToBack[0].price) >= betModel.Odds)
                                                {
                                                    betModel.Odds = Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToBack[0].price);
                                                    if (betModel.Odds > 0)
                                                    {
                                                        responseDTO.Status = true;
                                                        responseDTO.Result = "Bet rates are matched";
                                                    }
                                                    else
                                                    {
                                                        responseDTO.Status = false;
                                                        responseDTO.Result = "Bet rates are not valid";
                                                    }
                                                }
                                                else
                                                {
                                                    responseDTO.Status = true;
                                                    responseDTO.Result = "Bet rates are not matched";
                                                }
                                                break;
                                            case "Lay":
                                                if (Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToLay[0].price) <= betModel.Odds)
                                                {
                                                    betModel.Odds = Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToLay[0].price);
                                                    if (betModel.Odds > 0)
                                                    {
                                                        responseDTO.Status = true;
                                                        responseDTO.Result = "Bet rates are matched";
                                                    }
                                                    else
                                                    {
                                                        responseDTO.Status = false;
                                                        responseDTO.Result = "Bet rates are not valid";
                                                    }
                                                }
                                                else
                                                {
                                                    responseDTO.Status = true;
                                                    responseDTO.Result = "Bet rates are not matched";
                                                }
                                                break;
                                        }
                                    }
                                }
                                break;
                            case 4:
                                if (checkMarket.ApiUrlType == 1 || checkMarket.ApiUrlType == 3)
                                {
                                    var responseMessage = await client.GetAsync(requestUri: betModel.MarketName == "BookMaker" ? marketUrl.BookMakerUrl + betModel.MarketId + "/" + betModel.EventId : marketUrl.DaimondUrl + betModel.MarketId + "/" + betModel.EventId);
                                    if (responseMessage.IsSuccessStatusCode)
                                    {
                                        var data = responseMessage.Content.ReadAsStringAsync().Result;
                                        CricketAPIDTO cricketAPIDTO = JsonConvert.DeserializeObject<CricketAPIDTO>(data);
                                        if (cricketAPIDTO != null)
                                        {
                                            if (cricketAPIDTO.market[0].priceStatus == "OPEN" || cricketAPIDTO.market[0].priceStatus == "ACTIVE")
                                            {
                                                if (betModel.MarketName == "Match Odds" || betModel.MarketName == "Fancy")
                                                {
                                                    var checkRunner = cricketAPIDTO.session.FirstOrDefault(x => x.SelectionId == betModel.RunnerId);
                                                    switch (betModel.BetType)
                                                    {
                                                        case "Back":
                                                            if (double.TryParse(cricketAPIDTO.market[0].events[betModel.ParentId].BackPrice1, out double backPrice1) && backPrice1 >= betModel.Odds)
                                                            {
                                                                betModel.Odds = Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].BackPrice1);
                                                                if (betModel.Odds > 0)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Bet rates are not valid";
                                                            }
                                                            break;
                                                        case "Lay":
                                                            if (double.TryParse(cricketAPIDTO.market[0].events[betModel.ParentId].LayPrice1, out double layPrice1Lay) && layPrice1Lay <= betModel.Odds)
                                                            {
                                                                betModel.Odds = Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].LayPrice1);
                                                                if (betModel.Odds > 0)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Bet rates are not valid";
                                                            }
                                                            break;
                                                        case "Yes":
                                                            if (checkRunner != null && checkRunner.GameStatus != "SUSPENDED" && checkRunner.GameStatus != "Ball Running")
                                                            {
                                                                if (int.TryParse(checkRunner.BackPrice1, out int backPrice1Yes) && backPrice1Yes == betModel.Odds)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Market suspended";
                                                            }
                                                            break;
                                                        case "No":
                                                            if (checkRunner != null && checkRunner.GameStatus != "SUSPENDED" && checkRunner.GameStatus != "Ball Running")
                                                            {
                                                                if (int.TryParse(checkRunner.LayPrice1, out int layPrice1No) && layPrice1No == betModel.Odds)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Market suspended";
                                                            }
                                                            break;
                                                    }
                                                }
                                                else
                                                {
                                                    switch (betModel.BetType)
                                                    {
                                                        case "Back":
                                                            if (Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].BackPrice1) >= betModel.Odds)
                                                            {
                                                                betModel.Odds = Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].BackPrice1);
                                                                if (betModel.Odds > 0)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Bet rates are not valid";
                                                            }
                                                            break;
                                                        case "Lay":
                                                            if (Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].LayPrice1) <= betModel.Odds)
                                                            {
                                                                betModel.Odds = Convert.ToDouble(cricketAPIDTO.market[0].events[betModel.ParentId].LayPrice1);
                                                                if (betModel.Odds > 0)
                                                                {
                                                                    responseDTO.Status = true;
                                                                    responseDTO.Result = "Bet rates are matched";
                                                                }
                                                                else
                                                                {
                                                                    responseDTO.Status = false;
                                                                    responseDTO.Result = "Bet rates are not valid";
                                                                }
                                                            }
                                                            else
                                                            {
                                                                responseDTO.Status = false;
                                                                responseDTO.Result = "Bet rates are not valid";
                                                            }
                                                            break;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                responseDTO.Status = false;
                                                responseDTO.Result = "Market suspended";
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var betFairResponseCric = await client.GetAsync(requestUri: marketUrl.BetfairUrl + betModel.SportsId + "&marketid=" + betModel.MarketId);
                                    if (betFairResponseCric.IsSuccessStatusCode)
                                    {
                                        var data = betFairResponseCric.Content.ReadAsStringAsync().Result;

                                        BetFairAPIDTO betFairAPIDTO = JsonConvert.DeserializeObject<BetFairAPIDTO>(data);
                                        if (betFairAPIDTO != null && (betFairAPIDTO.status == "OPEN" || betFairAPIDTO.status == "ACTIVE"))
                                        {
                                            switch (betModel.BetType)
                                            {
                                                case "Back":
                                                    if (Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToBack[0].price) >= betModel.Odds)
                                                    {
                                                        betModel.Odds = Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToBack[0].price);
                                                        if (betModel.Odds > 0)
                                                        {
                                                            responseDTO.Status = true;
                                                            responseDTO.Result = "Bet rates are matched";
                                                        }
                                                        else
                                                        {
                                                            responseDTO.Status = false;
                                                            responseDTO.Result = "Bet rates are not valid";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        responseDTO.Status = true;
                                                        responseDTO.Result = "Bet rates are not matched";
                                                    }
                                                    break;
                                                case "Lay":
                                                    if (Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToLay[0].price) <= betModel.Odds)
                                                    {
                                                        betModel.Odds = Convert.ToDouble(betFairAPIDTO.runners[betModel.ParentId].availableToLay[0].price);
                                                        if (betModel.Odds > 0)
                                                        {
                                                            responseDTO.Status = true;
                                                            responseDTO.Result = "Bet rates are matched";
                                                        }
                                                        else
                                                        {
                                                            responseDTO.Status = false;
                                                            responseDTO.Result = "Bet rates are not valid";
                                                        }
                                                    }
                                                    else
                                                    {
                                                        responseDTO.Status = true;
                                                        responseDTO.Result = "Bet rates are not matched";
                                                    }
                                                    break;

                                            }
                                        }
                                    }
                                }

                                break;
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Wrong bet";
                    }

                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Wrong bet";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = "Wrong bet";
            }
            return responseDTO;
        }

        [HttpGet]
        [Route("GetClientPendingBets")]
        public async Task<IHttpActionResult> getClientPendingBets(int userId, string role)
        {
            try
            {
                List<BetModel> bets = new List<BetModel>();
                switch (role)
                {
                     case "Admin":
                        bets = await db.Bet.AsNoTracking().Where(x => x.AdminId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SubAdmin":
                        bets = await db.Bet.AsNoTracking().Where(x => x.SubAdminId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SuperMaster":
                        bets = await db.Bet.AsNoTracking().Where(x => x.SuperMasterId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Master":
                        bets = await db.Bet.AsNoTracking().Where(x => x.MasterId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "SuperAgent":
                        bets = await db.Bet.AsNoTracking().Where(x => x.SuperAgentId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Agent":
                        bets = await db.Bet.AsNoTracking().Where(x => x.ParentId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                    case "Client":
                        bets = await db.Bet.AsNoTracking().Where(x => x.UserId.Equals(userId) && x.BetStatus == "Pending").ToListAsync();
                        break;
                }

                if (bets.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = bets;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = bets;
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
        [Route("GetCasinoBets")]
        public async Task<IHttpActionResult> GetCasinoBets(string role, int userId, string marketId, string systemId, DateTime sDate, DateTime eDate)
        {
            try
            {
                List<TransactionModel> casinoBets = new List<TransactionModel>();
                switch (role)
                {
                    case "SuperAdmin":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "Admin":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "SubAdmin":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "SuperMaster":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "Master":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "SuperAgent":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "Agent":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.MarketId == marketId && x.EventId == systemId && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).ToListAsync();

                        break;
                    case "Client":
                        casinoBets = await db.Transaction.AsNoTracking().Where(x => x.id == userId).OrderByDescending(x => x.id).ToListAsync();

                        break;
                }
                if (casinoBets.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = casinoBets;
                }
                else
                {
                    responseDTO.Status = true;
                    responseDTO.Result = casinoBets;
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


    }
}