using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
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
    [RoutePrefix("SignUp")]
    [CustomAuthorization]
    public class SignUpController : ApiController
    {
        ResponseDTO responseDTO = new ResponseDTO();
        private FixDbContext db = new FixDbContext();
        Runtime run_time = new Runtime();
        CheckTokenExpire _check = new CheckTokenExpire();

        // GET: api/SignUpModels/CheckToken
        [HttpGet]
        [Route("Token_Refresh")]
        public async Task<IHttpActionResult> tokenRefresh(int userId)
        {
            try
            {
                var signUpModel = await db.SignUp.Where(x => x.id == userId && !x.deleted).FirstOrDefaultAsync();
                if (signUpModel != null)
                {
                    string token = _check.refreshToken(run_time.RunTimeToken(), signUpModel);
                    if (!string.IsNullOrEmpty(token))
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = token;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = HttpStatusCode.Forbidden;
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = HttpStatusCode.Forbidden;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }


        // Post: api/SignUpModels/Login
        [HttpPost]
        [Route("Valid_Login")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> ValidLogin(LoginDTO loginDto)
                {
            try
            {
                string ip = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrEmpty(ip))
                {
                    ip = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
                }
                SignUpModel signUpModel = new SignUpModel();
                signUpModel = db.SignUp.Where(x => x.UserId == loginDto.LoginID).FirstOrDefault();
                if (signUpModel != null)
                {
                    if (signUpModel.Password == loginDto.Password)
                    {
                        if (signUpModel.deleted == true)
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Account Closed";
                        }
                        else
                        {
                            if (signUpModel.status == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Account Blocked";
                            }
                            else
                            {
                                string token = TokenManager.generateToken(signUpModel);
                                if (!string.IsNullOrEmpty(token))
                                {
                                    signUpModel.jwtToken = token;
                                    signUpModel.IpAddress = ip;
                                    signUpModel.LoginToken = token;
                                    await db.SaveChangesAsync();
                                    responseDTO.Status = true;
                                    responseDTO.Result = token;
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
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "InValid UserName";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        // GET: api/SignUpModels
        [HttpGet]
        [Route("GetBy_ParentId")]
        public async Task<IHttpActionResult> GetSignUp(string type, int take, int referById, int skipRec, string value, string role)
        {
            try
            {
                var userObj = await db.SignUp.Where(x => x.id == referById && !x.deleted).FirstOrDefaultAsync();
                BalanceDetailDTO balObj = new BalanceDetailDTO();
                switch (role)
                {
                    case "SuperAdmin":
                        balObj.DownLineBal = await db.SignUp.Where(x => x.SuperId == referById).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineExp = await db.Exposure.Where(x => x.SuperId == referById && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineAvailBal = Math.Round(balObj.DownLineBal + balObj.DownLineExp, 2);
                        balObj.OwnBal = userObj.Balance;
                        balObj.TotalBal = Math.Round(balObj.OwnBal + balObj.DownLineBal, 2);
                        balObj.CreditLimit = userObj.CreditLimit;
                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        balObj.Parent = userObj.ParentId;
                        balObj.Role = userObj.Role;
                        switch (type)
                        {
                            case "All":
                            case "Inner":

                                var signObj = await (from s in db.SignUp
                                                     where
                                 s.ParentId.Equals(referById) && !s.deleted
                                                     select new
                                                     {
                                                         s.id,
                                                         s.UserId,
                                                         s.ParentId,
                                                         s.MasterId,
                                                         s.AdminId,
                                                         s.SuperId,
                                                         s.Role,
                                                         s.Balance,
                                                         s.CreditLimit,
                                                         profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                         DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                         s.createdOn,
                                                         Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                         s.BetStatus,
                                                         s.FancyBetStatus,
                                                         s.status,
                                                         s.deleted,
                                                         s.CasinoStatus,
                                                         s.TableStatus,
                                                         s.Password,
                                                         s.MobileNumber,
                                                         s.Share,
                                                         Amount = 0,
                                                         Trantype = 0,
                                                         Full = 0,
                                                         Remarks = "",
                                                     }).AsNoTracking().OrderBy(x => x.Role == "SubAdmin" ? 0 : 1).ThenBy(x => x.UserId).Skip(skipRec).Take(take).ToListAsync();
                                if (signObj.Count > 0)
                                {
                                    balObj.usrObj = signObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                    responseDTO.Count = await db.SignUp.CountAsync(x => x.ParentId == referById && !x.deleted);
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                            case "Search":
                                var searchObj = await (from s in db.SignUp
                                                       where s.UserId.ToLower().Contains(value.ToLower()) && !s.deleted
                                                       select new
                                                       {
                                                           s.id,
                                                           s.UserId,
                                                           s.ParentId,
                                                           s.MasterId,
                                                           s.AdminId,
                                                           s.SuperId,
                                                           s.Role,
                                                           s.Balance,
                                                           s.CreditLimit,
                                                           profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                           DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                           s.createdOn,
                                                           Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                           s.BetStatus,
                                                           s.FancyBetStatus,
                                                           s.status,
                                                           s.deleted,
                                                           s.CasinoStatus,
                                                           s.TableStatus,
                                                           s.Password,
                                                           s.MobileNumber,
                                                           s.Share,
                                                           Amount = 0,
                                                           Trantype = 0,
                                                           Full = 0,
                                                           Remarks = "",
                                                       }).AsNoTracking().OrderBy(x => x.Role == "SubAdmin" ? 0 : 1).ThenBy(x => x.UserId).ToListAsync();
                                if (searchObj.Count > 0)
                                {
                                    balObj.usrObj = searchObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                        }
                        break;
                    case "Admin":
                        balObj.DownLineBal = await db.SignUp.Where(x => x.SuperId == referById).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineExp = await db.Exposure.Where(x => x.SuperId == referById && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineAvailBal = Math.Round(balObj.DownLineBal + balObj.DownLineExp, 2);
                        balObj.OwnBal = userObj.Balance;
                        balObj.TotalBal = Math.Round(balObj.OwnBal + balObj.DownLineBal, 2);
                        balObj.CreditLimit = userObj.CreditLimit;
                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        balObj.Parent = userObj.ParentId;
                        balObj.Role = userObj.Role;
                        switch (type)
                        {
                            case "All":
                            case "Inner":

                                var signObj = await (from s in db.SignUp
                                                     where
                                 s.ParentId.Equals(referById) && !s.deleted
                                                     select new
                                                     {
                                                         s.id,
                                                         s.UserId,
                                                         s.ParentId,
                                                         s.MasterId,
                                                         s.AdminId,
                                                         s.SuperId,
                                                         s.Role,
                                                         s.Balance,
                                                         s.CreditLimit,
                                                         profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                         DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                         s.createdOn,
                                                         Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                         s.BetStatus,
                                                         s.FancyBetStatus,
                                                         s.status,
                                                         s.deleted,
                                                         s.CasinoStatus,
                                                         s.TableStatus,
                                                         s.Password,
                                                         s.MobileNumber,
                                                         s.Share,
                                                         Amount=0,
                                                         Trantype=0,
                                                         Full=0,
                                                         Remarks="",
                                                     }).AsNoTracking().OrderBy(x => x.Role == "SubAdmin" ? 0 : 1).ThenBy(x => x.UserId).Skip(skipRec).Take(take).ToListAsync();
                                if (signObj.Count > 0)
                                {
                                    balObj.usrObj = signObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                    responseDTO.Count = await db.SignUp.CountAsync(x => x.ParentId == referById && !x.deleted);
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                            case "Search":
                                var searchObj = await (from s in db.SignUp
                                                       where s.UserId.ToLower().Contains(value.ToLower()) && !s.deleted
                                                       select new
                                                       {
                                                           s.id,
                                                           s.UserId,
                                                           s.ParentId,
                                                           s.MasterId,
                                                           s.AdminId,
                                                           s.SuperId,
                                                           s.Role,
                                                           s.Balance,
                                                           s.CreditLimit,
                                                           profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                           DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.AdminId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.AdminId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                           s.createdOn,
                                                           Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                           s.BetStatus,
                                                           s.FancyBetStatus,
                                                           s.status,
                                                           s.deleted,
                                                           s.CasinoStatus,
                                                           s.TableStatus,
                                                           s.Password,
                                                           s.MobileNumber,
                                                           s.Share,
                                                           Amount = 0,
                                                           Trantype = 0,
                                                           Full = 0,
                                                           Remarks = "",
                                                       }).AsNoTracking().OrderBy(x => x.Role == "SubAdmin" ? 0 : 1).ThenBy(x => x.UserId).ToListAsync();
                                if (searchObj.Count > 0)
                                {
                                    balObj.usrObj = searchObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                        }
                        break;
                    case "SubAdmin":

                        balObj.DownLineBal = await db.SignUp.Where(x => x.AdminId == referById).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineExp = await db.Exposure.Where(x => x.AdminId == referById && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineAvailBal = Math.Round(balObj.DownLineBal + balObj.DownLineExp, 2);
                        balObj.OwnBal = userObj.Balance;
                        balObj.TotalBal = Math.Round(balObj.OwnBal + balObj.DownLineBal, 2);
                        balObj.CreditLimit = userObj.CreditLimit;
                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        balObj.Parent = userObj.ParentId;
                        balObj.Role = userObj.Role;

                        switch (type)
                        {
                            case "All":
                            case "Inner":

                                var signObj = await (from s in db.SignUp
                                                     where
                                 s.ParentId.Equals(referById) && !s.deleted
                                                     select new
                                                     {
                                                         s.id,
                                                         s.UserId,
                                                         s.ParentId,
                                                         s.MasterId,
                                                         s.AdminId,
                                                         s.SuperId,
                                                         s.Role,
                                                         s.Balance,
                                                         s.CreditLimit,
                                                         profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                         DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.MasterId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.MasterId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                         s.createdOn,
                                                         Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                         s.BetStatus,
                                                         s.FancyBetStatus,
                                                         s.status,
                                                         s.deleted,
                                                         s.CasinoStatus,
                                                         s.TableStatus,
                                                         s.Password,
                                                         s.MobileNumber,
                                                         s.Share,
                                                         Amount = 0,
                                                         Trantype = 0,
                                                         Full = 0,
                                                         Remarks = "",
                                                     }).AsNoTracking().OrderBy(x => x.Role == "Master" ? 0 : 1).ThenBy(x => x.UserId).Skip(skipRec).Take(take).ToListAsync();
                                if (signObj.Count > 0)
                                {
                                    balObj.usrObj = signObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                    responseDTO.Count = await db.SignUp.CountAsync(x => x.ParentId == referById && !x.deleted);
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                            case "Search":
                                var searchObj = await (from s in db.SignUp
                                                       where
                                   s.AdminId.Equals(referById) && s.UserId.ToLower().Contains(value.ToLower()) && !s.deleted
                                                       select new
                                                       {
                                                           s.id,
                                                           s.UserId,
                                                           s.ParentId,
                                                           s.MasterId,
                                                           s.AdminId,
                                                           s.SuperId,
                                                           s.Role,
                                                           s.Balance,
                                                           s.CreditLimit,
                                                           profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                           DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.MasterId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.MasterId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.MasterId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                           s.createdOn,
                                                           Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                           s.BetStatus,
                                                           s.FancyBetStatus,
                                                           s.status,
                                                           s.deleted,
                                                           s.CasinoStatus,
                                                           s.TableStatus,
                                                           s.Password,
                                                           s.MobileNumber,
                                                           s.Share,
                                                           Amount = 0,
                                                           Trantype = 0,
                                                           Full = 0,
                                                           Remarks = "",
                                                       }).AsNoTracking().OrderBy(x => x.Role == "Master" ? 0 : 1).ThenBy(x => x.UserId).ToListAsync();
                                if (searchObj.Count > 0)
                                {
                                    balObj.usrObj = searchObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                        }
                        break;
                    case "Master":
                        balObj.DownLineBal = await db.SignUp.Where(x => x.MasterId == referById).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineExp = await db.Exposure.Where(x => x.MasterId == referById && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineAvailBal = Math.Round(balObj.DownLineBal + balObj.DownLineExp, 2);
                        balObj.OwnBal = userObj.Balance;
                        balObj.TotalBal = Math.Round(balObj.OwnBal + balObj.DownLineBal, 2);
                        balObj.CreditLimit = userObj.CreditLimit;
                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        balObj.Parent = userObj.ParentId;
                        balObj.Role = userObj.Role;
                        switch (type)
                        {
                            case "All":
                            case "Inner":

                                var signObj = await (from s in db.SignUp
                                                     where
                                 s.ParentId.Equals(referById) && !s.deleted
                                                     select new
                                                     {
                                                         s.id,
                                                         s.UserId,
                                                         s.ParentId,
                                                         s.MasterId,
                                                         s.AdminId,
                                                         s.SuperId,
                                                         s.Role,
                                                         s.Balance,
                                                         s.CreditLimit,
                                                         profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                         DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.ParentId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.ParentId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                         ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                         s.createdOn,
                                                         Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                         s.BetStatus,
                                                         s.FancyBetStatus,
                                                         s.status,
                                                         s.deleted,
                                                         s.CasinoStatus,
                                                         s.TableStatus,
                                                         s.Password,
                                                         s.MobileNumber,
                                                         s.Share,
                                                         Amount = 0,
                                                         Trantype = 0,
                                                         Full = 0,
                                                         Remarks = "",
                                                     }).AsNoTracking().OrderBy(x => x.Role == "Agent" ? 0 : 1).ThenBy(x => x.UserId).Skip(skipRec).Take(take).ToListAsync();
                                if (signObj.Count > 0)
                                {
                                    balObj.usrObj = signObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                    responseDTO.Count = await db.SignUp.CountAsync(x => x.ParentId == referById && !x.deleted);
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                            case "Search":
                                var searchObj = await (from s in db.SignUp
                                                       where
                                   s.MasterId.Equals(referById) && s.UserId.ToLower().Contains(value.ToLower()) && !s.deleted
                                                       select new
                                                       {
                                                           s.id,
                                                           s.UserId,
                                                           s.ParentId,
                                                           s.MasterId,
                                                           s.AdminId,
                                                           s.SuperId,
                                                           s.Role,
                                                           s.Balance,
                                                           s.CreditLimit,
                                                           profitLoss = s.Role == "Client" ? db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum() : (s.Balance + db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() - s.CreditLimit),
                                                           DownBal = s.Role == "Client" ? 0 : db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           DownExp = s.Role == "Client" ? db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum() : db.Exposure.Where(x => x.ParentId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           DownAvailBal = s.Role == "Client" ? (s.Balance + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum()) : db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.ParentId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           TotalBal = s.Role == "Client" ? s.Balance : s.Balance + db.SignUp.Where(x => x.ParentId == s.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum(),
                                                           ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                           s.createdOn,
                                                           Count = s.Role == "Client" ? 0 : db.SignUp.Count(x => x.ParentId == s.id && !x.deleted),
                                                           s.BetStatus,
                                                           s.FancyBetStatus,
                                                           s.status,
                                                           s.deleted,
                                                           s.CasinoStatus,
                                                           s.TableStatus,
                                                           s.Password,
                                                           s.MobileNumber,
                                                           s.Share,
                                                           Amount = 0,
                                                           Trantype = 0,
                                                           Full = 0,
                                                           Remarks = "",
                                                       }).AsNoTracking().OrderBy(x => x.Role == "Agent" ? 0 : 1).ThenBy(x => x.UserId).ToListAsync();
                                if (searchObj.Count > 0)
                                {
                                    balObj.usrObj = searchObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                        }
                        break;
                    case "Agent":
                        balObj.DownLineBal = await db.SignUp.Where(x => x.ParentId == referById).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineExp = await db.Exposure.Where(x => x.ParentId == referById && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                        balObj.DownLineAvailBal = Math.Round(balObj.DownLineBal + balObj.DownLineExp, 2);
                        balObj.OwnBal = userObj.Balance;
                        balObj.TotalBal = Math.Round(balObj.OwnBal + balObj.DownLineBal, 2);
                        balObj.CreditLimit = userObj.CreditLimit;
                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        balObj.Parent = userObj.ParentId;
                        balObj.Role = userObj.Role;

                        switch (type)
                        {
                            case "All":
                            case "Inner":

                                var signObj = await (from s in db.SignUp
                                                     where
                                 s.ParentId.Equals(referById) && !s.deleted
                                                     select new
                                                     {
                                                         s.id,
                                                         s.UserId,
                                                         s.ParentId,
                                                         s.MasterId,
                                                         s.AdminId,
                                                         s.SuperId,
                                                         s.Role,
                                                         Balance = db.Transaction.Where(x => x.UserId == s.id).Select(l => l.Amount).DefaultIfEmpty(0).Sum(),
                                                         s.CreditLimit,
                                                         profitLoss = db.Transaction.Where(x => x.UserId == s.id && x.SportsId!=0).Select(l => l.Amount).DefaultIfEmpty(0).Sum(),// - s.CreditLimit,
                                                         DownExp = db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                         DownAvailBal = db.Transaction.Where(x => x.UserId == s.id).Select(l => l.Amount).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),

                                                         ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                         s.createdOn,
                                                         s.BetStatus,
                                                         s.FancyBetStatus,
                                                         s.status,
                                                         s.deleted,
                                                         s.CasinoStatus,
                                                         s.TableStatus,
                                                         s.Password,
                                                         s.MobileNumber,
                                                         s.Share,
                                                         Amount = 0,
                                                         Trantype = 0,
                                                         Full = 0,
                                                         Remarks = "",
                                                     }).AsNoTracking().OrderBy(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                                if (signObj.Count > 0)
                                {
                                    balObj.usrObj = signObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                    responseDTO.Count = await db.SignUp.CountAsync(x => x.ParentId == referById && !x.deleted);
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
                                }
                                break;
                            case "Search":
                                var searchObj = await (from s in db.SignUp
                                                       where
                                   s.ParentId.Equals(referById) && s.UserId.ToLower().Contains(value.ToLower()) && !s.deleted
                                                       select new
                                                       {
                                                           s.id,
                                                           s.UserId,
                                                           s.ParentId,
                                                           s.MasterId,
                                                           s.AdminId,
                                                           s.SuperId,
                                                           s.Role,
                                                           s.Balance,
                                                           s.CreditLimit,
                                                           profitLoss = db.Transaction.Where(x => x.UserId == s.id && x.SportsId != 0).Select(l => l.Amount).DefaultIfEmpty(0).Sum(),// - s.CreditLimit,

                                                           //profitLoss = db.Transaction.Where(x => x.UserId == s.id).Select(l => l.Amount).DefaultIfEmpty(0).Sum() - s.CreditLimit,
                                                           DownExp = db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),
                                                           DownAvailBal = db.Transaction.Where(x => x.UserId == s.id).Select(l => l.Amount).DefaultIfEmpty(0).Sum() + db.Exposure.Where(x => x.UserId == s.id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).Sum(),

                                                           ParentName = db.SignUp.Where(x => x.id == s.ParentId).FirstOrDefault().UserName,
                                                           s.createdOn,
                                                           s.BetStatus,
                                                           s.FancyBetStatus,
                                                           s.status,
                                                           s.deleted,
                                                           s.CasinoStatus,
                                                           s.TableStatus,
                                                           s.Password,
                                                           s.MobileNumber,
                                                           s.Share,
                                                           Amount = 0,
                                                           Trantype = 0,
                                                           Full = 0,
                                                           Remarks = "",
                                                       }).AsNoTracking().OrderBy(x => x.UserId).ToListAsync();
                                if (searchObj.Count > 0)
                                {
                                    balObj.usrObj = searchObj;
                                    responseDTO.Status = true;
                                    responseDTO.Result = balObj;
                                }
                                else
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = balObj;
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
        [Route("GetClsBy_ParntId")]
        public async Task<IHttpActionResult> getClsUSerList(int take, int skipRec)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                var userObj = await db.SignUp.AsNoTracking().Where(x => x.ParentId == id && x.deleted).OrderBy(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                if (userObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = userObj;
                    responseDTO.Count = await db.SignUp.AsNoTracking().CountAsync(x => x.ParentId == id && x.deleted);
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = userObj;
                }

            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        // POST: api/SignUpModels
        [HttpPost]
        [Route("Create_User")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> PostSignUpModel(SignUpModel SignObj)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {

                int user_Id = SignObj.ParentId == 0 ? Convert.ToInt32(run_time.RunTimeUserId()) : SignObj.ParentId;
                bool userName = ExistsUserName(SignObj.UserId);

                if (!userName)
                {
                    var ParentData = await db.SignUp.Where(x => x.id == user_Id && x.deleted == false).FirstOrDefaultAsync();
                    if (ParentData != null)
                    {
                        if (SignObj.Role != "Client")
                        {
                            if (ParentData.Share < SignObj.Share)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Share must be equal or less than parent share";
                                return Ok(responseDTO);
                            }
                        }
                        switch (ParentData.Role)
                        {
                            case "SuperAdmin":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.id;
                                break;
                            case "Admin":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.id;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            case "SubAdmin":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.id;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            case "SuperMaster":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.id;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            case "Master":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.id;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            case "SuperAgent":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.AgentId;
                                SignObj.SuperAgentId = ParentData.id;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            case "Agent":
                                SignObj.ParentId = ParentData.id;
                                SignObj.AgentId = ParentData.id;
                                SignObj.SuperAgentId = ParentData.SuperAgentId;
                                SignObj.MasterId = ParentData.MasterId;
                                SignObj.SuperMasterId = ParentData.SuperMasterId;
                                SignObj.SubAdminId = ParentData.SubAdminId;
                                SignObj.AdminId = ParentData.AdminId;
                                SignObj.SuperId = ParentData.SuperId;
                                break;
                            default:
                                SignObj.Share = SignObj.MatchCommission;
                                break;

                        }

                        SignObj.Role = SignObj.Role;
                        SignObj.BetStatus = ParentData.BetStatus;
                        SignObj.FancyBetStatus = ParentData.FancyBetStatus;
                        SignObj.CasinoStatus = ParentData.CasinoStatus;
                        SignObj.TableStatus = ParentData.TableStatus;
                        SignObj.ExposureLimit = 0;
                        SignObj.IpAddress = "";
                        SignObj.Exposure = 0;
                        SignObj.ProfitLoss = 0;
                        SignObj.CreditLimit = 0;
                        SignObj.createdOn = DateTime.Now;
                        SignObj.deleted = false;
                        SignObj.status = ParentData.status;
                        db.SignUp.Add(SignObj);
                        int returnValue = await db.SaveChangesAsync();
                        if (returnValue > 0)
                        {
                            TakeRecord takeRec = new TakeRecord()
                            {
                                UserId = SignObj.id,
                                Records = 10,
                                createdOn = DateTime.Now,
                            };
                            db.TakeRecord.Add(takeRec);
                            var chipModel = await db.Chip.Where(x => x.UserId == 1 && !x.deleted).ToListAsync();
                            if (chipModel.Count > 0)
                            {
                                foreach (var item in chipModel)
                                {
                                    ChipModel obj = new ChipModel()
                                    {
                                        UserId = SignObj.id,
                                        ChipName = item.ChipName,
                                        ChipValue = item.ChipValue,
                                        status = item.status,
                                        deleted = item.deleted,
                                        createdOn = DateTime.Now,
                                    };
                                    db.Chip.Add(obj);
                                }
                            }
                            await db.SaveChangesAsync();
                            responseDTO.Status = true;
                            responseDTO.Result = "Successfully Created!";
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "DataBase Exception At the time of Create data";
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No Parent Found";
                    }

                }
                else
                {
                    if (userName)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Username All ready Exist";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Mobile No All ready Exist";
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

        [HttpPost]
        [Route("Create_Master")]
        public async Task<IHttpActionResult> CreateMaster(string type, SignUpModel signObj)
        {
            try
            {
                if (type == "SecurityForPost")
                {
                    signObj.BetStatus = false;
                    signObj.status = false;
                    signObj.deleted = false;
                    signObj.FancyBetStatus = false;
                    signObj.CasinoStatus = false;
                    signObj.TableStatus = false;
                    signObj.ExposureLimit = 0;
                    signObj.IpAddress = "";
                    signObj.Exposure = 0;
                    signObj.ProfitLoss = 0;
                    signObj.CreditLimit = 0;
                    signObj.createdOn = DateTime.Now;
                    db.SignUp.Add(signObj);
                    await db.SaveChangesAsync();
                    responseDTO.Status = true;
                    responseDTO.Result = "Done";
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Type Error";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        // PUT : api/SignUpModels
        [HttpGet]
        [Route("Update_User")]
        public async Task<IHttpActionResult> PutSignUpModel(string Type, string Value, int userId)
        {
            try
            {
                SignUpModel SignObj = new SignUpModel();
                List<SignUpModel> parentObj = new List<SignUpModel>();
                SignObj = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                var checkParent = await db.SignUp.Where(x => x.id == SignObj.ParentId).FirstOrDefaultAsync();

                if (SignObj != null)
                {
                    switch (SignObj.Role)
                    {
                        case "SuperAdmin":
                            parentObj = await db.SignUp.Where(x => x.SuperId == userId).ToListAsync();
                            break;
                        case "Admin":
                            parentObj = await db.SignUp.Where(x => x.AdminId == userId).ToListAsync();
                            break;
                        case "SubAdmin":
                            parentObj = await db.SignUp.Where(x => x.SubAdminId == userId).ToListAsync();
                            break;
                        case "SuperMaster":
                            parentObj = await db.SignUp.Where(x => x.SuperMasterId == userId).ToListAsync();
                            break;
                        case "Master":
                            parentObj = await db.SignUp.Where(x => x.MasterId == userId).ToListAsync();
                            break;
                        case "SuperAgent":
                            parentObj = await db.SignUp.Where(x => x.SuperAgentId == userId).ToListAsync();
                            break;
                        case "Agent":
                            parentObj = await db.SignUp.Where(x => x.ParentId == userId).ToListAsync();
                            break;
                    }
                    switch (Type)
                    {
                        case "BetStatus":
                            if (checkParent.BetStatus == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent BetStatus Is Block So You Can't Do Any Thing Without your UpperLine Permission";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.BetStatus = true;
                                }
                                else
                                {
                                    SignObj.BetStatus = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.BetStatus = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.BetStatus = false);
                                    }
                                }
                            }
                            break;
                        case "FancyBetStatus":
                            if (checkParent.FancyBetStatus == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent FancyBetStatus Is Block So You Can't Do Any Thing Without your UpperLine Permission";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.FancyBetStatus = true;
                                }
                                else
                                {
                                    SignObj.FancyBetStatus = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.FancyBetStatus = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.FancyBetStatus = false);
                                    }
                                }
                            }
                            break;
                        case "CasinoStatus":
                            if (checkParent.CasinoStatus == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent CasinoStatus Is Block So You Can't Do Any Things Without your UpperLine Permission";
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.CasinoStatus = true;
                                }
                                else
                                {
                                    SignObj.CasinoStatus = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.CasinoStatus = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.CasinoStatus = false);
                                    }

                                }
                            }
                            break;
                        case "TableStatus":
                            if (checkParent.TableStatus == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent VirtualStatus Is Block So You Can't Do Any Thing Without your UpperLine Permission";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.TableStatus = true;
                                }
                                else
                                {
                                    SignObj.TableStatus = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.TableStatus = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.TableStatus = false);
                                    }
                                }
                            }
                            break;

                        case "Status":
                            if (checkParent.status == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent Status Is Block So You Can't Do Any Thing Without your UpperLine Permission";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.status = true;
                                }
                                else
                                {
                                    SignObj.status = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.status = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.status = false);
                                    }
                                }
                            }
                            break;
                        case "deleted":
                            if (checkParent.deleted == true)
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Parent Status Is Closed So You Can't Do Any Thing Without your UpperLine Permission";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                if (Value == "true")
                                {
                                    SignObj.deleted = true;
                                }
                                else
                                {
                                    SignObj.deleted = false;
                                }
                                if (parentObj.Count > 0)
                                {
                                    if (Value == "true")
                                    {
                                        parentObj.ForEach(x => x.deleted = true);
                                    }
                                    else
                                    {
                                        parentObj.ForEach(x => x.deleted = false);
                                    }
                                }
                            }
                            break;
                        case "Password":
                            if (SignObj.Password == Value)
                            {
                                responseDTO.Status = true;
                                responseDTO.Result = "Done";
                                return Ok(responseDTO);
                            }
                            else
                            {
                                SignObj.Password = Value;
                            }
                            break;
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
                        responseDTO.Result = "DataBase Exception At the time of Update data";
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "NO Data Found";
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
        [Route("getPwd")]
        public async Task<IHttpActionResult> getPwd()
        {
            int id = Convert.ToInt32(run_time.RunTimeUserId());
            string pwd = (await db.SignUp.AsNoTracking().Where(x => x.id == id).FirstOrDefaultAsync()).Password;
            responseDTO.Status = true;
            responseDTO.Result = pwd;
            return Ok(responseDTO);
        }


        [HttpGet]
        [Route("GetGeoLocation")]
        public async Task<IHttpActionResult> GetGeoLocation(string ipAddress)
        {
            try
            {
                string geoLocationUrl = System.Configuration.ConfigurationManager.AppSettings["GeoLocationAPIUrl"].ToString();
                var client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                var responseMessage = await client.GetAsync(requestUri: geoLocationUrl + "&ipAddress=" + ipAddress);
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
        [Route("GetUserDetails")]
        public async Task<IHttpActionResult> GetUserDetails(int id, string role, string token)
        {
            try
            {
                if (role == "SuperAdmin")
                {
                    var userDetails = await GetUserDetailsAsync(id, role);

                    if (userDetails != null)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = userDetails;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = userDetails;
                    }

                }
                else
                {
                    bool response = await CheckIsTokenValid(token);
                    if (response)
                    {
                        var userDetails = await GetUserDetailsAsync(id, role);

                        if (userDetails != null)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = userDetails;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = userDetails;
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Token Expired";
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


        private async Task<bool> CheckIsTokenValid(string userToken)
        {
            try
            {
                Runtime _runtime = new Runtime();
                int id = Convert.ToInt32(_runtime.RunTimeUserId());
                var checkIsTokenValid = await db.SignUp.AsNoTracking().FirstOrDefaultAsync(x => x.id == id);
                if (checkIsTokenValid.LoginToken == userToken)
                {
                    responseDTO.Status = true;
                }
                else
                {
                    responseDTO.Status = false;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;

            }
            return responseDTO.Status;
        }

        private async Task<object> GetUserDetailsAsync(int id, string role)
        {
            switch (role)
            {
                case "Client":
                    return await GetClientDetailsAsync(id);
                case "Agent":
                    return await GetAgentDetailsAsync(id);
                case "SuperAgent":
                    return await GetSuperAgentDetailsAsync(id);
                case "Master":
                    return await GetMasterDetailsAsync(id);
                case "SuperMaster":
                    return await GetSuperMasterDetailsAsync(id);
                case "SubAdmin":
                    return await GetSubAdminDetailsAsync(id);
                case "Admin":
                    return await GetAdminDetailsAsync(id);
                case "SuperAdmin":
                    return await GetSuperAdminDetailsAsync(id);

                default:
                    return null;
            }
        }

        private async Task<object> GetClientDetailsAsync(int id)
        {
            double exposure = await db.Exposure.AsNoTracking().Where(x => x.UserId == id && !x.deleted).Select(l => l.Exposure).DefaultIfEmpty(0).SumAsync();
            double balance = await db.Transaction.AsNoTracking().Where(x => x.UserId == id && !x.deleted).Select(l => l.Amount).DefaultIfEmpty(0).SumAsync();
            double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.UserId == id && !x.deleted && x.SportsId > 0).Select(l => l.Amount).DefaultIfEmpty(0).SumAsync();

            var objData = await (from s in db.SignUp
                                 where s.id == id
                                 select new
                                 {
                                     s.id,
                                     s.UserId,
                                     Bal = balance + exposure,
                                     TotalBal = balance,
                                     Settlement = balance - s.CreditLimit,
                                     ProfitLoss = profitLoss,
                                     s.CreditLimit,
                                     Exp = exposure,
                                     s.UserName,
                                     s.MobileNumber,
                                     s.Password,
                                     s.CasinoStatus,
                                     s.BetStatus,
                                     s.FancyBetStatus,
                                     s.TableStatus,
                                     s.status,
                                     s.LoginToken,
                                     Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id)==null?10: db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records,
                                     casinoStatus = db.UserSetting.FirstOrDefault(x => x.SportsId == 15).status,
                                     liveGameStatus = db.UserSetting.FirstOrDefault(x => x.SportsId == 11).status
                                 }).AsNoTracking().FirstOrDefaultAsync();

            return objData;
        }

        private async Task<object> GetAgentDetailsAsync(int id)
        {
            var adData = await (from s in db.SignUp
                                where s.id == id
                                select new
                                {
                                    s.id,
                                    s.LoginToken,
                                    s.UserId,
                                    Bal = s.Balance,
                                    Exp = db.Exposure.Where(x => x.ParentId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                    s.Share,
                                    News = db.News.Where(x => !x.deleted).Select(n => new
                                    {
                                        n.createdOn,
                                        n.News,
                                    }).ToList(),
                                    TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                    {
                                        e.EventName,
                                        e.EventTime,
                                    }).OrderBy(x => x.EventTime).ToList(),
                                    Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                }).AsNoTracking().FirstOrDefaultAsync();

            return adData;
        }

        private async Task<object> GetSuperMasterDetailsAsync(int id)
        {
            var adData = await (from s in db.SignUp
                                where s.id == id
                                select new
                                {
                                    s.id,
                                    s.LoginToken,
                                    s.UserId,
                                    Bal = s.Balance,
                                    Exp = db.Exposure.Where(x => x.SuperMasterId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                    s.Share,
                                    News = db.News.Where(x => !x.deleted).Select(n => new
                                    {
                                        n.createdOn,
                                        n.News,
                                    }).ToList(),
                                    TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                    {
                                        e.EventName,
                                        e.EventTime,
                                    }).OrderBy(x => x.EventTime).ToList(),
                                    Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                }).AsNoTracking().FirstOrDefaultAsync();

            return adData;
        }

        private async Task<object> GetSuperAgentDetailsAsync(int id)
        {
            var saData = await (from s in db.SignUp
                                where s.id == id
                                select new
                                {
                                    s.id,
                                    s.LoginToken,
                                    s.UserId,
                                    Bal = s.Balance,
                                    Exp = db.Exposure.Where(x => x.SuperAgentId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                    s.Share,
                                    News = db.News.Where(x => !x.deleted).Select(n => new
                                    {
                                        n.createdOn,
                                        n.News,
                                    }).ToList(),
                                    TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                    {
                                        e.EventName,
                                        e.EventTime,
                                    }).OrderBy(x => x.EventTime).ToList(),
                                    Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                }).AsNoTracking().FirstOrDefaultAsync();

            return saData;
        }

        private async Task<object> GetMasterDetailsAsync(int id)
        {
            var mdData = await (from s in db.SignUp
                                where s.id == id
                                select new
                                {
                                    s.id,
                                    s.LoginToken,
                                    s.UserId,
                                    Bal = s.Balance,
                                    Exp = db.Exposure.Where(x => x.MasterId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                    s.Share,
                                    News = db.News.Where(x => !x.deleted).Select(n => new
                                    {
                                        n.createdOn,
                                        n.News,
                                    }).ToList(),
                                    TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                    {
                                        e.EventName,
                                        e.EventTime,
                                    }).OrderBy(x => x.EventTime).ToList(),
                                    Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                }).AsNoTracking().FirstOrDefaultAsync();

            return mdData;
        }

        private async Task<object> GetSubAdminDetailsAsync(int id)
        {
            var sdData = await (from s in db.SignUp
                                where s.id == id
                                select new
                                {
                                    s.id,
                                    s.LoginToken,
                                    s.UserId,
                                    Bal = s.Balance,
                                    Exp = db.Exposure.Where(x => x.SubAdminId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                    s.Share,
                                    News = db.News.Where(x => !x.deleted).Select(n => new
                                    {
                                        n.createdOn,
                                        n.News,
                                    }).ToList(),
                                    TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                    {
                                        e.EventName,
                                        e.EventTime,
                                    }).OrderBy(x => x.EventTime).ToList(),
                                    Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                }).AsNoTracking().FirstOrDefaultAsync();

            return sdData;
        }

        private async Task<object> GetAdminDetailsAsync(int id)
        {
            var sudData = await (from s in db.SignUp
                                 where s.id == id
                                 select new
                                 {
                                     s.id,
                                     s.LoginToken,
                                     s.UserId,
                                     Bal = s.Balance,
                                     Exp = db.Exposure.Where(x => x.AdminId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                     s.Share,
                                     News = db.News.Where(x => !x.deleted).Select(n => new
                                     {
                                         n.createdOn,
                                         n.News,
                                     }).ToList(),
                                     TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                     {
                                         e.EventName,
                                         e.EventTime,
                                     }).OrderBy(x => x.EventTime).ToList(),
                                     Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                 }).AsNoTracking().FirstOrDefaultAsync();

            return sudData;
        }
        private async Task<object> GetSuperAdminDetailsAsync(int id)
        {
            var sudData = await (from s in db.SignUp
                                 where s.id == id
                                 select new
                                 {
                                     s.id,
                                     s.LoginToken,
                                     s.UserId,
                                     Bal = s.Balance,
                                     Exp = db.Exposure.Where(x => x.SuperId == id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).Sum(),
                                     s.Share,
                                     News = db.News.Where(x => !x.deleted).Select(n => new
                                     {
                                         n.createdOn,
                                         n.News,
                                     }).ToList(),
                                     TopEvents = db.Event.Where(x => !x.deleted && x.IsFav).Select(e => new
                                     {
                                         e.EventName,
                                         e.EventTime,
                                     }).OrderBy(x => x.EventTime).ToList(),
                                     Take = db.TakeRecord.FirstOrDefault(x => x.UserId == id).Records
                                 }).AsNoTracking().FirstOrDefaultAsync();

            return sudData;
        }

        [HttpGet]
        [Route("GetUserIds")]
        public async Task<IHttpActionResult> getUserIds(string value)
        {
            try
            {
                int _id = Convert.ToInt32(run_time.RunTimeUserId());
                switch (run_time.RunTimeRole())
                {
                    case "SuperAdmin":
                        var SAduserIds = await db.SignUp.AsNoTracking().Where(x => x.SuperId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (SAduserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = SAduserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Admin":
                        var AduserIds = await db.SignUp.AsNoTracking().Where(x => x.AdminId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (AduserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = AduserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SubAdmin":
                        var SduserIds = await db.SignUp.AsNoTracking().Where(x => x.SubAdminId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (SduserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = SduserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperMaster":
                        var SMduserIds = await db.SignUp.AsNoTracking().Where(x => x.SuperMasterId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (SMduserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = SMduserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Master":
                        var MduserIds = await db.SignUp.AsNoTracking().Where(x => x.MasterId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (MduserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = MduserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperAgent":
                        var SAguserIds = await db.SignUp.AsNoTracking().Where(x => x.SuperAgentId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (SAguserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = SAguserIds;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Agent":
                        var AguserIds = await db.SignUp.AsNoTracking().Where(x => x.ParentId == _id && x.UserId.ToLower().Contains(value.ToLower())).Select(x => new { x.UserId, x.id, x.Role }).ToListAsync();
                        if (AguserIds.Count > 0)
                        {
                            responseDTO.Status = true;
                            responseDTO.Result = AguserIds;
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


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        [HttpGet]
        [Route("CheckLoginId")]
        public bool ExistsUserName(string userId)
        {
            return db.SignUp.Count(e => e.UserId == userId) > 0;
        }
        [HttpGet]
        [Route("CheckMobile")]
        public bool ExistsMnumber(string m_number)
        {
            return db.SignUp.Count(e => e.MobileNumber == m_number) > 0;
        }

     
    }
}