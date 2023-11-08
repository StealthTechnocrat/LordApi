using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Globalization;
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
    [RoutePrefix("Casino")]
    public class SuperNowaTransModelsController : ApiController
    {
        ResponseDTO responseDTO = new ResponseDTO();
        CasinoResponseDTO casinoResponseDTO = new CasinoResponseDTO();
        Runtime _runtime = new Runtime();
        private FixDbContext db = new FixDbContext();
        status staObj = new status();


        [HttpGet]
        [Route("getLobby")]
        public async Task<IHttpActionResult> getLobbyUrl(string gameCode)
        {
            try
            {
                int _id = Convert.ToInt32(_runtime.RunTimeUserId());
                string userId = (await db.SignUp.AsNoTracking().Where(x => x.id == _id).FirstOrDefaultAsync()).UserName;
                game gameOj = new game()
                {
                    gameCode = gameCode,
                    providerCode = "SN",
                };
                user userObj = new user()
                {
                    id = userId,
                    currency = "INR",
                };
                AuthDTO authDTO = new AuthDTO()
                {
                    partnerName = "fix2club",
                    partnerKey =
    "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=",
                    timestamp = DateTime.Now.ToString(),
                    game = gameOj,
                    user = userObj,
                };
                string nowaUrl = System.Configuration.ConfigurationManager.AppSettings["SuperNowaAuth"].ToString();
                var client = new HttpClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType: "application/json"));
                var responseMessage = await client.PostAsJsonAsync(requestUri: nowaUrl, authDTO);
                if (responseMessage.IsSuccessStatusCode)
                {
                    var data = responseMessage.Content.ReadAsStringAsync().Result;
                    dynamic response = JsonConvert.DeserializeObject<dynamic>(data);
                    if (response.launchURL != null)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = response;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Server Error";
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Error";
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
        [Route("balance")]
        public async Task<IHttpActionResult> balance(BalanceRequestDTO balanceRequestDTO)
        {
            try
            {
                if (balanceRequestDTO.SecurityToken == System.Configuration.ConfigurationManager.AppSettings["SecurityToken"])
                {
                    if (balanceRequestDTO.UserId != null)
                    {
                        var checkUserExist = await db.SignUp.AsNoTracking().FirstOrDefaultAsync(x => x.UserId.Equals(balanceRequestDTO.UserId));
                        if (checkUserExist != null)
                        {
                            double balance = await db.Transaction.AsNoTracking().Where(x => x.UserId == checkUserExist.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() + await db.Exposure.AsNoTracking().Where(x => x.UserId == checkUserExist.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                            casinoResponseDTO.status = "OK";
                            casinoResponseDTO.balance = balance.ToString();
                        }
                        else
                        {
                            casinoResponseDTO.status = "ERROR";
                            casinoResponseDTO.balance = "0.00";
                        }

                    }
                    else
                    {
                        casinoResponseDTO.status = "ERROR";
                        casinoResponseDTO.balance = "0.00";
                    }
                }
                else
                {
                    casinoResponseDTO.status = "InValid token";
                    casinoResponseDTO.balance = "0.00";
                }
            }
            catch (Exception ex)
            {
                casinoResponseDTO.status = "ERROR";
                casinoResponseDTO.balance = "0.00";

            }
            return Ok(casinoResponseDTO);
        }

        [HttpPost]
        [Route("debit")]
        public async Task<IHttpActionResult> debit(LiveCasinoDTO liveCasinoDTO)
        {

            if (liveCasinoDTO.SecurityToken == System.Configuration.ConfigurationManager.AppSettings["SecurityToken"])
            {
                var checkUserExist = await db.SignUp.FirstOrDefaultAsync(x => x.UserId == liveCasinoDTO.UserId);
                double balance = await db.Transaction.AsNoTracking().Where(x => x.UserId == checkUserExist.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() +
                                    await db.Exposure.AsNoTracking().Where(x => x.UserId == checkUserExist.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                if (balance >= liveCasinoDTO.Amount)
                {
                    using (var dbContextTransaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            var checkGameStatus = await db.LiveCasinoGamesModel.AsNoTracking().FirstOrDefaultAsync(x => x.SystemId == liveCasinoDTO.SystemId && x.status == false && x.deleted == false && x.PageCode.ToLower().Contains(liveCasinoDTO.PageCode.ToLower()));
                            var checkProviderStatus = await db.LiveCasinoProvidersModel.AsNoTracking().FirstOrDefaultAsync(x => x.SystemId == liveCasinoDTO.SystemId && x.deleted == false);
                            
                            if (checkUserExist != null)
                            {
                                if (checkUserExist.CasinoStatus)
                                {
                                    casinoResponseDTO.status = "ERROR";
                                    casinoResponseDTO.balance = "0.00";
                                    return Ok(casinoResponseDTO);
                                }
                                else
                                {

                                    if (checkProviderStatus == null)
                                    {
                                        casinoResponseDTO.status = "ERROR";
                                        casinoResponseDTO.balance = "0.00";
                                        return Ok(casinoResponseDTO);
                                    }

                                    if (checkGameStatus == null)
                                    {

                                        casinoResponseDTO.status = "ERROR";
                                        casinoResponseDTO.balance = "0.00";
                                        return Ok(casinoResponseDTO);
                                    }
                                }
                                
                                balance = Math.Round(balance - liveCasinoDTO.Amount, 2);
                                checkUserExist.Balance = balance;
                                checkUserExist.ExposureLimit = balance;
                                TransactionModel transactionModel = new TransactionModel()
                                {
                                    UserId = checkUserExist.id,
                                    UserName = checkUserExist.UserName,
                                    SportsId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]),
                                    EventId = liveCasinoDTO.SystemId,
                                    MarketId = liveCasinoDTO.PageCode,
                                    SelectionId = liveCasinoDTO.TransactionId,
                                    Discription = checkGameStatus.GameName + " " + checkProviderStatus.ProviderName + "(" + liveCasinoDTO.TransactionId + ")",
                                    MarketName = liveCasinoDTO.PageCode,
                                    Amount = -liveCasinoDTO.Amount,
                                    Balance = balance,
                                    SuperMasterId = checkUserExist.SuperMasterId,
                                    AgentId = checkUserExist.AgentId,
                                    SuperAgentId = checkUserExist.SuperAgentId,
                                    SubAdminId = checkUserExist.SubAdminId,
                                    ParentId = checkUserExist.ParentId,
                                    MasterId = checkUserExist.MasterId,
                                    AdminId = checkUserExist.AdminId,
                                    SuperId = checkUserExist.SuperId,
                                    Parent = 0,
                                    createdOn = DateTime.Now,
                                };
                                db.Transaction.Add(transactionModel);

                                await db.SaveChangesAsync();
                                dbContextTransaction.Commit();
                                casinoResponseDTO.status = "OK";
                                casinoResponseDTO.balance = balance.ToString();
                            }
                            else
                            {
                                casinoResponseDTO.status = "ERROR";
                                casinoResponseDTO.balance = "0.00";
                            }

                        }
                        catch (Exception ex)
                        {
                            dbContextTransaction.Rollback();
                            casinoResponseDTO.status = "ERROR";
                            casinoResponseDTO.balance = "0.00";

                        }
                    }
                }
                else
                {
                    casinoResponseDTO.status = "ERROR";
                    casinoResponseDTO.balance = "0.00";
                }
               
            }
            else
            {
                casinoResponseDTO.status = "InValid Token";
                casinoResponseDTO.balance = "0.00";
            }
            return Ok(casinoResponseDTO);
        }
        [HttpPost]
        [Route("credit")]
        public async Task<IHttpActionResult> credit(LiveCasinoDTO liveCasinoDTO)
        {
            if (liveCasinoDTO.SecurityToken == System.Configuration.ConfigurationManager.AppSettings["SecurityToken"])
            {

                using (var dbContextTransaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var checkGameStatus = await db.LiveCasinoGamesModel.AsNoTracking().FirstOrDefaultAsync(x => x.SystemId == liveCasinoDTO.SystemId && x.status == false && x.deleted == false && x.PageCode.ToLower().Contains(liveCasinoDTO.PageCode.ToLower()));
                        var checkProviderStatus = await db.LiveCasinoProvidersModel.AsNoTracking().FirstOrDefaultAsync(x => x.SystemId == liveCasinoDTO.SystemId && x.deleted == false);
                        var checkUserExist = await db.SignUp.Where(x => x.UserId == liveCasinoDTO.UserId).FirstOrDefaultAsync();
                        if (checkUserExist != null)
                        {
                            if (checkUserExist.CasinoStatus)
                            {
                                casinoResponseDTO.status = "ERROR";
                                casinoResponseDTO.balance = "0.00";
                                return Ok(casinoResponseDTO);
                            }
                            else
                            {
                                if (checkProviderStatus == null)
                                {
                                    casinoResponseDTO.status = "ERROR";
                                    casinoResponseDTO.balance = "0.00";
                                    return Ok(casinoResponseDTO);
                                }
                                if (checkGameStatus == null)
                                {
                                    casinoResponseDTO.status = "ERROR";
                                    casinoResponseDTO.balance = "0.00";
                                    return Ok(casinoResponseDTO);
                                }
                            }
                            var balance = await db.Transaction.AsNoTracking().Where(x => x.UserId == checkUserExist.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() +
                                await db.Exposure.AsNoTracking().Where(x => x.UserId == checkUserExist.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                            balance = Math.Round(balance + liveCasinoDTO.Amount, 2);
                            checkUserExist.Balance = balance;
                            checkUserExist.ExposureLimit = balance;
                            TransactionModel transactionModel = new TransactionModel()
                            {
                                UserId = checkUserExist.id,
                                UserName = checkUserExist.UserName,
                                SportsId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]),
                                EventId = liveCasinoDTO.SystemId,
                                MarketId = liveCasinoDTO.PageCode,
                                SelectionId = liveCasinoDTO.TransactionId,
                                Discription = checkGameStatus.GameName + " " + checkProviderStatus.ProviderName + "(" + liveCasinoDTO.TransactionId + ")",
                                MarketName = liveCasinoDTO.PageCode,
                                Amount = liveCasinoDTO.Amount,
                                Balance = balance,
                                SuperMasterId = checkUserExist.SuperMasterId,
                                AgentId = checkUserExist.AgentId,
                                SuperAgentId = checkUserExist.SuperAgentId,
                                SubAdminId = checkUserExist.SubAdminId,
                                ParentId = checkUserExist.ParentId,
                                MasterId = checkUserExist.MasterId,
                                AdminId = checkUserExist.AdminId,
                                SuperId = checkUserExist.SuperId,
                                Parent = 0,
                                createdOn = DateTime.Now,
                            };
                            db.Transaction.Add(transactionModel);

                            await db.SaveChangesAsync();
                            dbContextTransaction.Commit();
                            casinoResponseDTO.status = "OK";
                            casinoResponseDTO.balance = balance.ToString();
                        }
                        else
                        {
                            casinoResponseDTO.status = "ERROR";
                            casinoResponseDTO.balance = "0.00";
                        }

                    }
                    catch (Exception ex)
                    {
                        dbContextTransaction.Rollback();
                        casinoResponseDTO.status = "ERROR";
                        casinoResponseDTO.balance = "0.00";

                    }
                }
            }
            else
            {
                casinoResponseDTO.status = "InValid Token";
                casinoResponseDTO.balance = "0.00";
            }
            return Ok(casinoResponseDTO);
        }


        //[HttpPost]
        //[Route("balance")]
        //public async Task<IHttpActionResult> balance(SuperNowaBalDTO superBalObj)
        //{
        //    try
        //    {
        //        if (superBalObj.userId != null)
        //        {
        //            if (superBalObj.timestamp != null)
        //            {
        //                if (superBalObj.partnerKey != null && superBalObj.partnerKey == "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=")
        //                {

        //                    var signObj = await db.SignUp.AsNoTracking().Where(x => x.UserId == superBalObj.userId).FirstOrDefaultAsync();
        //                    if (signObj != null)
        //                    {
        //                        double bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == signObj.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() + await db.Exposure.AsNoTracking().Where(x => x.UserId == signObj.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
        //                        if (bal >= 0)
        //                        {
        //                            staObj.code = "SUCCESS";
        //                            staObj.message = "";
        //                            CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                            CasinoResDTO.userId = superBalObj.userId;
        //                            CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                            CasinoResDTO.balance = bal;
        //                            CasinoResDTO.status = staObj;
        //                        }
        //                        else
        //                        {
        //                            staObj.code = "SUCCESS";
        //                            staObj.message = "";
        //                            CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                            CasinoResDTO.userId = superBalObj.userId;
        //                            CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                            CasinoResDTO.balance = 0;
        //                            CasinoResDTO.status = staObj;
        //                        }
        //                    }
        //                    else
        //                    {
        //                        staObj.code = "INVALID_TOKEN";
        //                        staObj.message = "";
        //                        CasinoResDTO.partnerKey = null;
        //                        CasinoResDTO.userId = null;
        //                        CasinoResDTO.timestamp = null;
        //                        CasinoResDTO.balance = 0;
        //                        CasinoResDTO.status = staObj;
        //                    }
        //                }
        //                else
        //                {
        //                    staObj.code = "VALIDATION_ERROR";
        //                    staObj.message = "PartnerKey is a mandatory.";
        //                    CasinoResDTO.partnerKey = null;
        //                    CasinoResDTO.userId = null;
        //                    CasinoResDTO.timestamp = null;
        //                    CasinoResDTO.balance = 0;
        //                    CasinoResDTO.status = staObj;
        //                }
        //            }
        //            else
        //            {
        //                staObj.code = "VALIDATION_ERROR";
        //                staObj.message = "Timestamp is a mandatory.";
        //                CasinoResDTO.partnerKey = null;
        //                CasinoResDTO.userId = null;
        //                CasinoResDTO.timestamp = null;
        //                CasinoResDTO.balance = 0;
        //                CasinoResDTO.status = staObj;
        //            }
        //        }
        //        else
        //        {
        //            staObj.code = "VALIDATION_ERROR";
        //            staObj.message = "UserId is a mandatory.";
        //            CasinoResDTO.partnerKey = null;
        //            CasinoResDTO.userId = null;
        //            CasinoResDTO.timestamp = null;
        //            CasinoResDTO.balance = 0;
        //            CasinoResDTO.status = staObj;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        staObj.code = "INVALID_TOKEN";
        //        staObj.message = "";
        //        CasinoResDTO.partnerKey = null;
        //        CasinoResDTO.userId = null;
        //        CasinoResDTO.timestamp = null;
        //        CasinoResDTO.balance = 0;
        //        CasinoResDTO.status = staObj;
        //    }
        //    return Ok(CasinoResDTO);
        //}

        //[HttpPost]
        //[Route("debit")]
        //public async Task<IHttpActionResult> debit(SuperTranDTO superTranDTO)
        //{
        //    if (superTranDTO.partnerKey == "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=")
        //    {
        //        if (superTranDTO.timestamp != null)
        //        {
        //            if (superTranDTO.user.id != null)
        //            {
        //                if (superTranDTO.transactionData.id != null)
        //                {
        //                    using (var dbContextTransaction = db.Database.BeginTransaction())
        //                    {
        //                        try
        //                        {
        //                            var signObj = await db.SignUp.AsNoTracking().Where(x => x.UserId == superTranDTO.user.id).FirstOrDefaultAsync();
        //                            if (signObj != null)
        //                            {
        //                                var bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == signObj.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() + await db.Exposure.AsNoTracking().Where(x => x.UserId == signObj.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

        //                                var transData = await db.SuperNowaTransModels.Where(x => x.transId == superTranDTO.transactionData.id && x.userName == superTranDTO.user.id && !x.deleted).FirstOrDefaultAsync();
        //                                if (transData != null)
        //                                {
        //                                    if (superTranDTO.gameData.description == "cancel")
        //                                    {
        //                                        if (superTranDTO.transactionData.referenceId == transData.refId)
        //                                        {
        //                                            //rollback transaction
        //                                            var transModel = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (transModel != null)
        //                                            {
        //                                                transModel.Amount = Math.Round(transModel.Amount + superTranDTO.transactionData.amount, 2);
        //                                                transModel.Balance = Math.Round(transModel.Balance + superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > transModel.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance + superTranDTO.transactionData.amount);
        //                                                }
        //                                                bal = Math.Round(bal + superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                transData.deleted = true;
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            staObj.code = "VALIDATION_ERROR";
        //                                            staObj.message = "Invalid referenceId for cancellation";
        //                                            CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                            CasinoResDTO.userId = superTranDTO.user.id;
        //                                            CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                            CasinoResDTO.balance = bal;
        //                                            CasinoResDTO.status = staObj;
        //                                        }
        //                                    }
        //                                    else
        //                                    {
        //                                        staObj.code = "VALIDATION_ERROR";
        //                                        staObj.message = "Transaction with transactionId already processed.";
        //                                        CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                        CasinoResDTO.userId = superTranDTO.user.id;
        //                                        CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                        CasinoResDTO.balance = bal;
        //                                        CasinoResDTO.status = staObj;
        //                                    }
        //                                }
        //                                else
        //                                {
        //                                    if (superTranDTO.gameData.description == "cancel")
        //                                    {
        //                                        var refData = await db.SuperNowaTransModels.Where(x => x.transId == superTranDTO.transactionData.referenceId && x.userName == superTranDTO.user.id).FirstOrDefaultAsync();
        //                                        if (refData != null && !refData.deleted)
        //                                        {
        //                                            //rollback transaction
        //                                            var tranOBJ = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (tranOBJ != null)
        //                                            {
        //                                                tranOBJ.Amount = Math.Round(tranOBJ.Amount + superTranDTO.transactionData.amount, 2);
        //                                                tranOBJ.Balance = Math.Round(tranOBJ.Balance + superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > tranOBJ.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance + superTranDTO.transactionData.amount);
        //                                                }
        //                                                bal = Math.Round(bal + superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                refData.deleted = true;
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            if (refData == null)
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Invalid referenceId for cancellation.";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;

        //                                            }
        //                                            else
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Transaction with referenceId already processed.";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;

        //                                            }
        //                                        }
        //                                    }
        //                                    else
        //                                    {
        //                                        if (superTranDTO.transactionData.amount > 0)
        //                                        {
        //                                            var checkTran = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (checkTran != null)
        //                                            {
        //                                                bal = Math.Round(bal - superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                checkTran.Amount = Math.Round(checkTran.Amount - superTranDTO.transactionData.amount, 2);
        //                                                checkTran.Balance = Math.Round(checkTran.Balance - superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > checkTran.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance - superTranDTO.transactionData.amount);
        //                                                }
        //                                                SuperNowaTransModel superNowaTransModel = new SuperNowaTransModel()
        //                                                {
        //                                                    userId = signObj.id,
        //                                                    userName = signObj.UserId,
        //                                                    amount = -superTranDTO.transactionData.amount,
        //                                                    refId = superTranDTO.transactionData.id,
        //                                                    transId = superTranDTO.transactionData.id,

        //                                                    discription = "SuperNowa(" + superTranDTO.gameData.gameCode + ")" + superTranDTO.gameData.providerRoundId + "(" + superTranDTO.gameData.description + ")",
        //                                                    createdOn = DateTime.Now,
        //                                                    status = false,
        //                                                    deleted = false,
        //                                                };
        //                                                db.SuperNowaTransModels.Add(superNowaTransModel);
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                            else
        //                                            {
        //                                                bal = Math.Round(bal - superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                TransactionModel transactionModel = new TransactionModel()
        //                                                {
        //                                                    UserId = signObj.id,
        //                                                    UserName = signObj.UserId,
        //                                                    SportsId = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]),
        //                                                    EventId = "SuperNowa",
        //                                                    MarketId = superTranDTO.gameData.providerRoundId,
        //                                                    Discription = "SuperNowa(" + superTranDTO.gameData.gameCode + ")" + superTranDTO.gameData.providerRoundId + "(" + superTranDTO.gameData.description + ")",
        //                                                    MarketName = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=",
        //                                                    Amount = -superTranDTO.transactionData.amount,
        //                                                    Balance = bal,
        //                                                    ParentId = signObj.ParentId,
        //                                                    MasterId = signObj.MasterId,
        //                                                    SuperId = signObj.SuperId,
        //                                                    AdminId = signObj.AdminId,
        //                                                    createdOn = DateTime.Now,
        //                                                    status = false,
        //                                                    deleted = false,
        //                                                };
        //                                                SuperNowaTransModel superNowaTransModel = new SuperNowaTransModel()
        //                                                {
        //                                                    userId = signObj.id,
        //                                                    userName = signObj.UserId,
        //                                                    amount = -superTranDTO.transactionData.amount,
        //                                                    refId = superTranDTO.transactionData.id,
        //                                                    transId = superTranDTO.transactionData.id,

        //                                                    discription = "SuperNowa(" + superTranDTO.gameData.gameCode + ")" + superTranDTO.gameData.providerRoundId + "(" + superTranDTO.gameData.description + ")",
        //                                                    createdOn = DateTime.Now,
        //                                                    status = false,
        //                                                    deleted = false,
        //                                                };
        //                                                db.Transaction.Add(transactionModel);
        //                                                db.SuperNowaTransModels.Add(superNowaTransModel);
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            if (superTranDTO.transactionData.amount == 0)
        //                                            {
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                            else
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Invalid amount";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;

        //                                            }
        //                                        }
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                staObj.code = "VALIDATION_ERROR";
        //                                staObj.message = "UserId not found";
        //                                CasinoResDTO.partnerKey = null;
        //                                CasinoResDTO.userId = null;
        //                                CasinoResDTO.timestamp = null;
        //                                CasinoResDTO.balance = 0;
        //                                CasinoResDTO.status = staObj;
        //                            }
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            dbContextTransaction.Rollback();
        //                            staObj.code = "VALIDATION_ERROR";
        //                            staObj.message = "Transaction Failed";
        //                            CasinoResDTO.partnerKey = null;
        //                            CasinoResDTO.userId = null;
        //                            CasinoResDTO.timestamp = null;
        //                            CasinoResDTO.balance = 0;
        //                            CasinoResDTO.status = staObj;
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    staObj.code = "VALIDATION_ERROR";
        //                    staObj.message = "Transaction Id is mandatory field";
        //                    CasinoResDTO.partnerKey = null;
        //                    CasinoResDTO.userId = null;
        //                    CasinoResDTO.timestamp = null;
        //                    CasinoResDTO.balance = 0;
        //                    CasinoResDTO.status = staObj;
        //                }
        //            }
        //            else
        //            {
        //                staObj.code = "VALIDATION_ERROR";
        //                staObj.message = "Userid is a mandatory.";
        //                CasinoResDTO.partnerKey = null;
        //                CasinoResDTO.userId = null;
        //                CasinoResDTO.timestamp = null;
        //                CasinoResDTO.balance = 0;
        //                CasinoResDTO.status = staObj;
        //            }
        //        }
        //        else
        //        {
        //            staObj.code = "VALIDATION_ERROR";
        //            staObj.message = "Timestamp is a mandatory.";
        //            CasinoResDTO.partnerKey = null;
        //            CasinoResDTO.userId = null;
        //            CasinoResDTO.timestamp = null;
        //            CasinoResDTO.balance = 0;
        //            CasinoResDTO.status = staObj;
        //        }
        //    }
        //    else
        //    {
        //        staObj.code = "VALIDATION_ERROR";
        //        staObj.message = "Wrong PartnerKey";
        //        CasinoResDTO.partnerKey = null;
        //        CasinoResDTO.userId = null;
        //        CasinoResDTO.timestamp = null;
        //        CasinoResDTO.balance = 0;
        //        CasinoResDTO.status = staObj;
        //    }
        //    return Ok(CasinoResDTO);
        //}
        //[HttpPost]
        //[Route("credit")]
        //public async Task<IHttpActionResult> credit(SuperTranDTO superTranDTO)
        //{

        //    if (superTranDTO.partnerKey == "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=")
        //    {
        //        if (superTranDTO.timestamp != null)
        //        {
        //            if (superTranDTO.user.id != null)
        //            {
        //                if (superTranDTO.transactionData.id != null)
        //                {
        //                    using (var dbContextTransaction = db.Database.BeginTransaction())
        //                    {
        //                        try
        //                        {
        //                            var signObj = await db.SignUp.AsNoTracking().Where(x => x.UserId == superTranDTO.user.id).FirstOrDefaultAsync();
        //                            if (signObj != null)
        //                            {
        //                                double dt = await db.Transaction.AsNoTracking().Where(x => x.UserId == signObj.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
        //                                var bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == signObj.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync() + await db.Exposure.AsNoTracking().Where(x => x.UserId == signObj.id && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

        //                                var transData = await db.SuperNowaTransModels.Where(x => x.transId == superTranDTO.transactionData.id && x.userName == superTranDTO.user.id && !x.deleted).FirstOrDefaultAsync();
        //                                if (transData != null)
        //                                {
        //                                    if (superTranDTO.gameData.description == "cancel")
        //                                    {
        //                                        if (superTranDTO.transactionData.referenceId == transData.refId)
        //                                        {
        //                                            //rollback transaction
        //                                            var transModel = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (transModel != null)
        //                                            {
        //                                                transModel.Amount = Math.Round(transModel.Amount - superTranDTO.transactionData.amount, 2);
        //                                                transModel.Balance = Math.Round(transModel.Balance - superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > transModel.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance - superTranDTO.transactionData.amount);
        //                                                }
        //                                                bal = Math.Round(bal - superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                transData.deleted = true;
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            staObj.code = "VALIDATION_ERROR";
        //                                            staObj.message = "Invalid referenceId for cancellation";
        //                                            CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                            CasinoResDTO.userId = superTranDTO.user.id;
        //                                            CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                            CasinoResDTO.balance = bal;
        //                                            CasinoResDTO.status = staObj;
        //                                        }
        //                                    }
        //                                    else
        //                                    {
        //                                        staObj.code = "VALIDATION_ERROR";
        //                                        staObj.message = "Transaction with transactionId already processed.";
        //                                        CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                        CasinoResDTO.userId = superTranDTO.user.id;
        //                                        CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                        CasinoResDTO.balance = bal;
        //                                        CasinoResDTO.status = staObj;
        //                                    }
        //                                }
        //                                else
        //                                {
        //                                    if (superTranDTO.gameData.description == "cancel")
        //                                    {
        //                                        var refData = await db.SuperNowaTransModels.Where(x => x.transId == superTranDTO.transactionData.referenceId && x.userName == superTranDTO.user.id).FirstOrDefaultAsync();
        //                                        if (refData != null && !refData.deleted)
        //                                        {
        //                                            //rollback transaction
        //                                            var transObj = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (transObj != null)
        //                                            {
        //                                                transObj.Amount = Math.Round(transObj.Amount - superTranDTO.transactionData.amount, 2);
        //                                                transObj.Balance = Math.Round(transObj.Balance - superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > transObj.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance - superTranDTO.transactionData.amount);
        //                                                }
        //                                                bal = Math.Round(bal - superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                refData.deleted = true;
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            if (refData == null)
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Invalid referenceId for cancellation";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                            else
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Transaction with referenceId already processed.";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }

        //                                    }
        //                                    else
        //                                    {
        //                                        if (superTranDTO.transactionData.amount > 0)
        //                                        {
        //                                            var checkTran = await db.Transaction.Where(x => x.UserId == signObj.id && x.MarketId == superTranDTO.gameData.providerRoundId).FirstOrDefaultAsync();
        //                                            if (checkTran != null)
        //                                            {
        //                                                bal = Math.Round(bal + superTranDTO.transactionData.amount, 2);
        //                                                signObj.Balance = bal;
        //                                                checkTran.Amount = Math.Round(checkTran.Amount + superTranDTO.transactionData.amount, 2);
        //                                                checkTran.Balance = Math.Round(checkTran.Balance + superTranDTO.transactionData.amount, 2);
        //                                                var tranList = await db.Transaction.Where(x => x.UserId == signObj.id && x.id > checkTran.id).ToListAsync();
        //                                                if (tranList.Count > 0)
        //                                                {
        //                                                    tranList.ForEach(x => x.Balance = x.Balance + superTranDTO.transactionData.amount);
        //                                                }
        //                                                SuperNowaTransModel superNowaTransModel = new SuperNowaTransModel()
        //                                                {
        //                                                    userId = signObj.id,
        //                                                    userName = signObj.UserId,
        //                                                    amount = superTranDTO.transactionData.amount,
        //                                                    refId = superTranDTO.transactionData.id,
        //                                                    transId = superTranDTO.transactionData.id,
        //                                                    discription = "SuperNowa(" + superTranDTO.gameData.gameCode + ")" + superTranDTO.gameData.providerRoundId + "(" + superTranDTO.gameData.description + ")",
        //                                                    createdOn = DateTime.Now,
        //                                                    status = false,
        //                                                    deleted = false,
        //                                                };
        //                                                db.SuperNowaTransModels.Add(superNowaTransModel);
        //                                                await db.SaveChangesAsync();
        //                                                dbContextTransaction.Commit();
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                        }
        //                                        else
        //                                        {
        //                                            if (superTranDTO.transactionData.amount == 0)
        //                                            {
        //                                                staObj.code = "SUCCESS";
        //                                                staObj.message = "";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;
        //                                            }
        //                                            else
        //                                            {
        //                                                staObj.code = "VALIDATION_ERROR";
        //                                                staObj.message = "Invalid amount";
        //                                                CasinoResDTO.partnerKey = "oo7wFFl2JTa8285rOWbuBOK2e7nCgiES7+CPi3dICTn7BOD+w9DYv9EsQa5CutwZJ6ckdfPCsAQ=";
        //                                                CasinoResDTO.userId = superTranDTO.user.id;
        //                                                CasinoResDTO.timestamp = DateTime.Now.ToString();
        //                                                CasinoResDTO.balance = bal;
        //                                                CasinoResDTO.status = staObj;

        //                                            }
        //                                        }
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                staObj.code = "VALIDATION_ERROR";
        //                                staObj.message = "UserId not found";
        //                                CasinoResDTO.partnerKey = null;
        //                                CasinoResDTO.userId = null;
        //                                CasinoResDTO.timestamp = null;
        //                                CasinoResDTO.balance = 0;
        //                                CasinoResDTO.status = staObj;
        //                            }
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            dbContextTransaction.Rollback();
        //                            staObj.code = "VALIDATION_ERROR";
        //                            staObj.message = "Transaction Failed";
        //                            CasinoResDTO.partnerKey = null;
        //                            CasinoResDTO.userId = null;
        //                            CasinoResDTO.timestamp = null;
        //                            CasinoResDTO.balance = 0;
        //                            CasinoResDTO.status = staObj;
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    staObj.code = "VALIDATION_ERROR";
        //                    staObj.message = "Transaction Id is mandatory field";
        //                    CasinoResDTO.partnerKey = null;
        //                    CasinoResDTO.userId = null;
        //                    CasinoResDTO.timestamp = null;
        //                    CasinoResDTO.balance = 0;
        //                    CasinoResDTO.status = staObj;
        //                }
        //            }
        //            else
        //            {
        //                staObj.code = "VALIDATION_ERROR";
        //                staObj.message = "Userid is a mandatory.";
        //                CasinoResDTO.partnerKey = null;
        //                CasinoResDTO.userId = null;
        //                CasinoResDTO.timestamp = null;
        //                CasinoResDTO.balance = 0;
        //                CasinoResDTO.status = staObj;
        //            }
        //        }
        //        else
        //        {
        //            staObj.code = "VALIDATION_ERROR";
        //            staObj.message = "Timestamp is a mandatory.";
        //            CasinoResDTO.partnerKey = null;
        //            CasinoResDTO.userId = null;
        //            CasinoResDTO.timestamp = null;
        //            CasinoResDTO.balance = 0;
        //            CasinoResDTO.status = staObj;
        //        }
        //    }
        //    else
        //    {
        //        staObj.code = "VALIDATION_ERROR";
        //        staObj.message = "Wrong PartnerKey";
        //        CasinoResDTO.partnerKey = null;
        //        CasinoResDTO.userId = null;
        //        CasinoResDTO.timestamp = null;
        //        CasinoResDTO.balance = 0;
        //        CasinoResDTO.status = staObj;
        //    }
        //    return Ok(CasinoResDTO);
        //}



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