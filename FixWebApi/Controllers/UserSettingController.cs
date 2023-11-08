using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using FixWebApi.Models;
using FixWebApi.Models.DTO;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Sports")]
    public class UserSettingController : ApiController
    {
        private FixDbContext db = new FixDbContext();
        ResponseDTO responseDTO = new ResponseDTO();

        //Post : api/PostUserSettings
        [HttpPost]
        [Route("CreateSettings")]
        public async Task<IHttpActionResult> PostSettings(IList<UserSettingModel> userSettingModel)
        {
            try
            {
                db.UserSetting.AddRange(userSettingModel);
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

        [HttpPost]
        [Route("UpdateSettings")]
        public async Task<IHttpActionResult> PutSettings(UserSettingModel userObj)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    var userSetting = await db.UserSetting.Where(x => x.SportsId == userObj.SportsId).FirstOrDefaultAsync();
                    if (userSetting != null)
                    {
                        userSetting.MaxStake = userObj.MaxStake;
                        userSetting.MinStake = userObj.MinStake;
                        userSetting.MaxOdds = userObj.MaxOdds;
                        userSetting.BetDelay = userObj.BetDelay;
                        userSetting.FancyDelay = userObj.FancyDelay;
                        userSetting.MaxProfit = userObj.MaxProfit;
                        userSetting.status = userObj.status;
                    }
                    var eventObj = await db.Event.Where(x => x.SportsId == userObj.SportsId).ToListAsync();
                    if (eventObj.Count > 0)
                    {
                        foreach (var item in eventObj)
                        {
                            item.MaxStake = userObj.MaxStake;
                            item.MinStake = userObj.MinStake;
                            item.Betdelay = userObj.BetDelay;
                            item.Fancydelay = userObj.FancyDelay;
                            item.MaxProfit = userObj.MaxProfit;
                            var mrktObj = await db.Market.Where(x => x.EventId == item.EventId).ToListAsync();
                            if (mrktObj.Count > 0)
                            {
                                foreach (var mrkt in mrktObj)
                                {
                                    mrkt.MaxStake = userObj.MaxStake;
                                    mrkt.MinStake = userObj.MinStake;
                                    mrkt.Betdelay = userObj.BetDelay;
                                    mrkt.Fancydelay = userObj.FancyDelay;
                                }
                            }
                            await db.SaveChangesAsync();
                        }
                    }
                    await db.SaveChangesAsync();
                    dbContextTransaction.Commit();
                    responseDTO.Status = true;
                    responseDTO.Result = "Done";
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
        [Route("GetSettings")]
        public async Task<IHttpActionResult> getSettings()
        {
            try
            {
                var setObj = await db.UserSetting.AsNoTracking().Where(x => !x.deleted).ToListAsync();
                if (setObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = setObj;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = setObj;
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