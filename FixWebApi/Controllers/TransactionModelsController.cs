using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using FixWebApi.Authentication;
using FixWebApi.Models;
using FixWebApi.Models.BetFairClasses;
using FixWebApi.Models.DTO;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Transaction")]
    [CustomAuthorization]
    public class TransactionModelsController : ApiController
    {
        private FixDbContext db = new FixDbContext();
        ResponseDTO responseDTO = new ResponseDTO();
        Runtime run_time = new Runtime();


        [HttpGet]
        [Route("ParentTrans")]
        public async Task<IHttpActionResult> ParentTrans(double amount)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                var parentObj = await db.SignUp.Where(x => x.id == id && !x.deleted && !x.status).FirstOrDefaultAsync();

                parentObj.Balance = Math.Round(parentObj.Balance + amount, 2);
                parentObj.CreditLimit = Math.Round(parentObj.CreditLimit + amount, 2);

                TransactionModel trnObj = new TransactionModel()
                {
                    UserId = id,
                    UserName = parentObj.UserName,
                    SportsId = 0,
                    EventId = "Deposit",
                    MarketId = "Balance",
                    SelectionId = "Plus",
                    Discription = "Credit To Own",
                    MarketName = "Cash",
                    Remark = "Self Deposit",
                    Amount = amount,
                    Balance = parentObj.Balance,
                    status = false,
                    deleted = false,
                    createdOn = DateTime.Now,
                };
                db.Transaction.Add(trnObj);
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
        [Route("Transactions")]
        public async Task<IHttpActionResult> Transactions(int userId, double amount, string type, string remark)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    //string runTimeToken = run_time.RunTimeToken();
                    //int id = Convert.ToInt32(run_time.RunTimeUserId());
                    //var checkValidUser= await db.SignUp.FirstOrDefaultAsync(x => x.id == id && !x.deleted);
                    //if(checkValidUser!=null)
                    //{
                    if (amount <= 0)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Amount must be greater than 0.";
                        return Ok(responseDTO);
                    }

                    var childObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == userId && !x.deleted);
                    var parentObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == childObj.ParentId && !x.deleted);

                    double balance = 0;
                    if (childObj.Role == "Client")
                    {
                        balance = await db.Transaction.Where(x => x.UserId == userId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                    }
                    else
                    {
                        balance = childObj.Balance;
                    }

                    //double parentBalance = await db.Transaction.Where(x => x.UserId == childObj.ParentId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();

                    switch (type)
                    {
                        case "Deposit":
                            if (parentObj.Balance >= amount)
                            {
                                parentObj.Balance -= amount;
                                childObj.Balance += amount;
                                childObj.ExposureLimit = childObj.Balance;

                                if (childObj.CreditLimit == 0)
                                {
                                    childObj.CreditLimit = childObj.Balance;
                                }
                                else
                                {
                                    childObj.CreditLimit += amount;
                                }


                                TransactionModel trnChildObj = new TransactionModel()
                                {
                                    UserId = userId,
                                    UserName = childObj.UserId,
                                    SportsId = 0,
                                    EventId = "Balance",
                                    MarketId = "Deposit",
                                    SelectionId = "Plus",
                                    Discription = "Deposit given by Parent.",
                                    MarketName = "Cash",
                                    Remark = remark,
                                    Amount = amount,
                                    Balance = childObj.Balance,
                                    AgentId =childObj.AgentId,
                                    SuperAgentId= childObj.SuperAgentId,
                                    SuperMasterId = childObj.SuperMasterId,
                                    SubAdminId = childObj.SubAdminId,
                                    ParentId = childObj.ParentId,
                                    MasterId = childObj.MasterId,
                                    AdminId = childObj.AdminId,
                                    SuperId = childObj.SuperId,
                                    status = false,
                                    deleted = false,
                                    createdOn = DateTime.Now,
                                    Parent = 1,
                                };
                                db.Transaction.Add(trnChildObj);

                                TransactionModel trnPrntObj = new TransactionModel()
                                {
                                    UserId = parentObj.id,
                                    UserName = parentObj.UserId,
                                    SportsId = 0,
                                    EventId = "Deposit",
                                    MarketId = "Balance",
                                    SelectionId = "Minus",
                                    Discription = "Deposit given to " + childObj.UserId,
                                    MarketName = "Cash",
                                    Remark = remark,
                                    Amount = -amount,
                                    Balance = parentObj.Balance,
                                    AgentId = parentObj.AgentId,
                                    SuperAgentId = parentObj.SuperAgentId,
                                    SuperMasterId = parentObj.SuperMasterId,
                                    SubAdminId = parentObj.SubAdminId,
                                    ParentId = parentObj.ParentId,
                                    MasterId = parentObj.MasterId,
                                    AdminId = parentObj.AdminId,
                                    SuperId = parentObj.SuperId,
                                    status = false,
                                    deleted = false,
                                    createdOn = DateTime.Now,
                                };
                                db.Transaction.Add(trnPrntObj);

                                await db.SaveChangesAsync();
                                dbContextTransaction.Commit();
                                responseDTO.Status = true;
                                responseDTO.Result = "Done";

                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Low Balance.";
                            }
                            break;

                        case "Withdraw":
                            double exposure = await db.Exposure.Where(x => x.UserId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                            double balanceWithExposure = balance + exposure;
                            // && childObj.CreditLimit>amount

                            if (balanceWithExposure >= amount)
                            {
                                parentObj.Balance += amount;
                                childObj.Balance -= amount;

                                var profitLoss = await db.Transaction.Where(x => x.UserId == userId).Select(l => l.Amount).DefaultIfEmpty(0).SumAsync() - childObj.CreditLimit;
                                double difference = 0;
                                if (profitLoss > 0)
                                {
                                    //if (profitLoss == balanceWithExposure)
                                    //{
                                    //    responseDTO.Status = false;
                                    //    responseDTO.Result = "Low balance...or..settlement is pending.";
                                    //}
                                    //else
                                    //{
                                    difference = profitLoss - amount;
                                    if (difference < 0)
                                    {
                                        childObj.CreditLimit = childObj.CreditLimit + difference;
                                    }
                                    //}

                                }
                                else
                                {
                                    childObj.CreditLimit -= amount;
                                }

                                TransactionModel trnChildObj = new TransactionModel()
                                {
                                    UserId = userId,
                                    UserName = childObj.UserId,
                                    SportsId = 0,
                                    EventId = "Withdraw",
                                    MarketId = "Balance",
                                    SelectionId = "Minus",
                                    Discription = "Withdrawal given by Parent.",
                                    MarketName = "Cash",
                                    Remark = remark,
                                    Amount = -amount,
                                    Balance = childObj.Balance,
                                    AgentId = childObj.AgentId,
                                    SuperAgentId = childObj.SuperAgentId,
                                    SuperMasterId = childObj.SuperMasterId,
                                    SubAdminId = childObj.SubAdminId,
                                    ParentId = childObj.ParentId,
                                    MasterId = childObj.MasterId,
                                    AdminId = childObj.AdminId,
                                    SuperId = childObj.SuperId,
                                    status = false,
                                    deleted = false,
                                    createdOn = DateTime.Now,
                                    Parent = 1,
                                };
                                db.Transaction.Add(trnChildObj);

                                TransactionModel trnPrntObj = new TransactionModel()
                                {
                                    UserId = parentObj.id,
                                    UserName = parentObj.UserId,
                                    SportsId = 0,
                                    EventId = "Withdraw",
                                    MarketId = "Balance",
                                    SelectionId = "Plus",
                                    Discription = "Withdrawal given to " + childObj.UserId,
                                    MarketName = "Cash",
                                    Remark = remark,
                                    Amount = amount,
                                    Balance = parentObj.Balance,
                                    AgentId = parentObj.AgentId,
                                    SuperAgentId = parentObj.SuperAgentId,
                                    SuperMasterId = parentObj.SuperMasterId,
                                    SubAdminId = parentObj.SubAdminId,
                                    ParentId = parentObj.ParentId,
                                    MasterId = parentObj.MasterId,
                                    AdminId = parentObj.AdminId,
                                    SuperId = parentObj.SuperId,
                                    status = false,
                                    deleted = false,
                                    createdOn = DateTime.Now,
                                };
                                db.Transaction.Add(trnPrntObj);

                                await db.SaveChangesAsync();
                                dbContextTransaction.Commit();
                                responseDTO.Status = true;
                                responseDTO.Result = "Done";
                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Low balance...or..settlement is pending.";
                            }
                            break;
                    }
                    // }
                    //else
                    //{
                    //    responseDTO.Status = false;
                    //    responseDTO.Result = "UnAuthorized request.";
                    //}                   
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
        [Route("MultiTransactions")]
        public async Task<IHttpActionResult> MultiTransactions(string password, List<DepositeWithDrawDTO> depositeWithDrawDTOs)
        {
            int id = Convert.ToInt32(run_time.RunTimeUserId());
            var checkPassword = await db.SignUp.AsNoTracking().FirstOrDefaultAsync(x => x.id == id);
            if (password.ToLower() == checkPassword.Password.ToLower())
            {
                if (depositeWithDrawDTOs.Count > 0)
                {
                    using (var dbContextTransaction = db.Database.BeginTransaction())
                    {
                        try
                        {
                            foreach (var item in depositeWithDrawDTOs)
                            {
                                if (item.Amount <= 0)
                                {
                                    responseDTO.Status = false;
                                    responseDTO.Result = "Amount must be greater than 0.";
                                    return Ok(responseDTO);
                                }

                                var childObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.UserId && !x.deleted);
                                var parentObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == childObj.ParentId && !x.deleted);

                                double balance = 0;
                                if (childObj.Role == "Client")
                                {
                                    balance = await db.Transaction.Where(x => x.UserId == item.UserId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                }
                                else
                                {
                                    balance = childObj.Balance;
                                }

                                //double parentBalance = await db.Transaction.Where(x => x.UserId == childObj.ParentId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();

                                switch (item.Type)
                                {
                                    case "Deposit":
                                        if (parentObj.Balance >= item.Amount)
                                        {
                                            parentObj.Balance -= item.Amount;
                                            childObj.Balance += item.Amount;
                                            childObj.ExposureLimit = childObj.Balance;

                                            if (childObj.CreditLimit == 0)
                                            {
                                                childObj.CreditLimit = childObj.Balance;
                                            }
                                            else
                                            {
                                                childObj.CreditLimit += item.Amount;
                                            }


                                            TransactionModel trnChildObj = new TransactionModel()
                                            {
                                                UserId = item.UserId,
                                                UserName = childObj.UserId,
                                                SportsId = 0,
                                                EventId = "Balance",
                                                MarketId = "Deposit",
                                                SelectionId = "Plus",
                                                Discription = "Deposit given by Parent.",
                                                MarketName = "Cash",
                                                Remark = item.Remarks,
                                                Amount = item.Amount,
                                                Balance = childObj.Balance,
                                                AgentId = childObj.AgentId,
                                                SuperAgentId = childObj.SuperAgentId,
                                                SuperMasterId = childObj.SuperMasterId,
                                                SubAdminId = childObj.SubAdminId,
                                                ParentId = childObj.ParentId,
                                                MasterId = childObj.MasterId,
                                                AdminId = childObj.AdminId,
                                                SuperId = childObj.SuperId,
                                                status = false,
                                                deleted = false,
                                                createdOn = DateTime.Now,
                                                Parent = 1,
                                            };
                                            db.Transaction.Add(trnChildObj);

                                            TransactionModel trnPrntObj = new TransactionModel()
                                            {
                                                UserId = parentObj.id,
                                                UserName = parentObj.UserId,
                                                SportsId = 0,
                                                EventId = "Deposit",
                                                MarketId = "Balance",
                                                SelectionId = "Minus",
                                                Discription = "Deposit given to " + childObj.UserId,
                                                MarketName = "Cash",
                                                Remark = item.Remarks,
                                                Amount = -item.Amount,
                                                Balance = parentObj.Balance,
                                                AgentId = parentObj.AgentId,
                                                SuperAgentId = parentObj.SuperAgentId,
                                                SuperMasterId = parentObj.SuperMasterId,
                                                SubAdminId = parentObj.SubAdminId,
                                                ParentId = parentObj.ParentId,
                                                MasterId = parentObj.MasterId,
                                                AdminId = parentObj.AdminId,
                                                SuperId = parentObj.SuperId,
                                                status = false,
                                                deleted = false,
                                                createdOn = DateTime.Now,
                                            };
                                            db.Transaction.Add(trnPrntObj);

                                            await db.SaveChangesAsync();


                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = "Low Balance.";
                                        }
                                        break;

                                    case "Withdraw":
                                        double exposure = await db.Exposure.Where(x => x.UserId == item.UserId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();
                                        double balanceWithExposure = balance + exposure;
                                        // && childObj.CreditLimit>amount
                                        if (balanceWithExposure >= item.Amount)
                                        {
                                            parentObj.Balance += item.Amount;
                                            childObj.Balance -= item.Amount;

                                            var profitLoss = await db.Transaction.Where(x => x.UserId == item.UserId).Select(l => l.Amount).DefaultIfEmpty(0).SumAsync() - childObj.CreditLimit;
                                            double difference = 0;
                                            if (profitLoss > 0)
                                            {
                                                difference = profitLoss - item.Amount;
                                                if (difference < 0)
                                                {
                                                    childObj.CreditLimit = childObj.CreditLimit + difference;
                                                }
                                            }
                                            else
                                            {
                                                childObj.CreditLimit -= item.Amount;

                                            }

                                            TransactionModel trnChildObj = new TransactionModel()
                                            {
                                                UserId = item.UserId,
                                                UserName = childObj.UserId,
                                                SportsId = 0,
                                                EventId = "Withdraw",
                                                MarketId = "Balance",
                                                SelectionId = "Minus",
                                                Discription = "Withdrawal given by Parent.",
                                                MarketName = "Cash",
                                                Remark = item.Remarks,
                                                Amount = -item.Amount,
                                                Balance = childObj.Balance,
                                                AgentId = childObj.AgentId,
                                                SuperAgentId = childObj.SuperAgentId,
                                                SuperMasterId = childObj.SuperMasterId,
                                                SubAdminId = childObj.SubAdminId,
                                                ParentId = childObj.ParentId,
                                                MasterId = childObj.MasterId,
                                                AdminId = childObj.AdminId,
                                                SuperId = childObj.SuperId,
                                                status = false,
                                                deleted = false,
                                                createdOn = DateTime.Now,
                                                Parent = 1,
                                            };
                                            db.Transaction.Add(trnChildObj);

                                            TransactionModel trnPrntObj = new TransactionModel()
                                            {
                                                UserId = parentObj.id,
                                                UserName = parentObj.UserId,
                                                SportsId = 0,
                                                EventId = "Withdraw",
                                                MarketId = "Balance",
                                                SelectionId = "Plus",
                                                Discription = "Withdrawal given to " + childObj.UserId,
                                                MarketName = "Cash",
                                                Remark = item.Remarks,
                                                Amount = item.Amount,
                                                Balance = parentObj.Balance,
                                                AgentId = parentObj.AgentId,
                                                SuperAgentId = parentObj.SuperAgentId,
                                                SuperMasterId = parentObj.SuperMasterId,
                                                SubAdminId = parentObj.SubAdminId,
                                                ParentId = parentObj.ParentId,
                                                MasterId = parentObj.MasterId,
                                                AdminId = parentObj.AdminId,
                                                SuperId = parentObj.SuperId,
                                                status = false,
                                                deleted = false,
                                                createdOn = DateTime.Now,
                                            };
                                            db.Transaction.Add(trnPrntObj);

                                            await db.SaveChangesAsync();
                                        }
                                        else
                                        {
                                            responseDTO.Status = false;
                                            responseDTO.Result = "InSufficent Funds";
                                        }

                                        break;
                                }
                            }
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
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Empty list";
                }
            }
            else
            {
                responseDTO.Status = false;
                responseDTO.Result = "Wrong password";
            }

            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("TranHistory")]
        public async Task<IHttpActionResult> GetTransactions(string role, int userId, int skipRec, int take, string type, int sportsId, string marketName, DateTime sDate, DateTime eDate)
        {
            try
            {
                List<TransactionDTO> transObjList = new List<TransactionDTO>();
                var usrObj = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (role)
                {
                    case "Client":
                        switch (type)
                        {
                            case "All":
                                var tranObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                                if (tranObj.Count > 0)
                                {
                                    foreach (var obj in tranObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = obj.Commission,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }

                                    }
                                    responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate);
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.MarketName == "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id)
                                .Skip(skipRec).Take(take).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = obj.Commission,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                    responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == userId && x.MarketName == "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate);
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                            if (chk == null)
                                            {
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = obj.EventId,
                                                    MarketId = obj.MarketId,
                                                    SelectionId = obj.SelectionId,
                                                    Discription = obj.Discription,
                                                    MarketName = obj.MarketName,
                                                    Remark = obj.Remark,
                                                    Amount = obj.Amount,
                                                    Commission = obj.Commission,
                                                    Balance = obj.Balance,
                                                    CreatedOn = obj.createdOn,
                                                    SportsId = obj.SportsId,
                                                    Id = obj.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate);
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (chk == null)
                                                {
                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = obj.Amount,
                                                        Balance = obj.Balance,
                                                        Commission = obj.Commission,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate);
                                        }

                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (chk == null)
                                                {
                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = obj.Amount,
                                                        Commission = obj.Commission,
                                                        Balance = obj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate);
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "Agent":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();

                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = cash.Commission,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {
                                    foreach (var item in tranAgObj)
                                    {

                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = -profitLoss,
                                                    Commission = commission,
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = -profitLoss,
                                                    Commission = 0.00,
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = -profitLoss,
                                                        Commission = commission,
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = -profitLoss,
                                                        Commission = commission,
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.ParentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "SuperAgent":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();

                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = cash.Commission,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {
                                    foreach (var item in tranAgObj)
                                    {

                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                               double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperAgentId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "Master":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = 0.00,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {

                                    foreach (var item in tranAgObj)
                                    {

                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.MasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "SuperMaster":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();

                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = cash.Commission,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {
                                    foreach (var item in tranAgObj)
                                    {

                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                                double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var chk = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (chk == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperMasterId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "SubAdmin":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var checkTrans = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (checkTrans == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = 0.00,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {
                                    foreach (var item in tranAgObj)
                                    {
                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (checkTrans == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SubAdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "Admin":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {
                                        var checkTrans = transObjList.FirstOrDefault(x => x.Id == cash.id);
                                        if (checkTrans == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = cash.EventId,
                                                MarketId = cash.MarketId,
                                                SelectionId = cash.SelectionId,
                                                Discription = cash.Discription,
                                                MarketName = cash.MarketName,
                                                Remark = cash.Remark,
                                                Amount = cash.Amount,
                                                Commission = 0.00,
                                                Balance = cash.Balance,
                                                CreatedOn = cash.createdOn,
                                                SportsId = cash.SportsId,
                                                Id = cash.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {
                                    foreach (var item in tranAgObj)
                                    {
                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                    Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                    Id = item.id,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (checkTrans == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                                Id = obj.id,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                        Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                        Id = obj.id,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.AdminId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                                            Commission = Math.Round(usrObj.Share / 100 * commission, 2),
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                            Id = obj.id,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    case "SuperAdmin":
                        switch (type)
                        {
                            case "All":
                                var tranAgObj = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName != "Cash").GroupBy(x => new { x.MarketId, x.SelectionId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                var agCashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (agCashObj.Count > 0)
                                {
                                    foreach (var cash in agCashObj)
                                    {

                                        TransactionDTO transDTO = new TransactionDTO()
                                        {
                                            Id = cash.id,
                                            UserName = usrObj.UserName,
                                            EventId = cash.EventId,
                                            MarketId = cash.MarketId,
                                            SelectionId = cash.SelectionId,
                                            Discription = cash.Discription,
                                            MarketName = cash.MarketName,
                                            Remark = cash.Remark,
                                            Amount = cash.Amount,
                                            Commission = 0.00,
                                            Balance = cash.Balance,
                                            CreatedOn = cash.createdOn,
                                            SportsId = cash.SportsId,
                                        };
                                        transObjList.Add(transDTO);
                                    }
                                }
                                if (tranAgObj.Count > 0)
                                {

                                    foreach (var item in tranAgObj)
                                    {
                                        if (item.MarketName != "Fancy")
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    Id = item.id,
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = -profitLoss,
                                                    Commission = commission,
                                                    CreatedOn = item.createdOn,
                                                    Balance = usrObj.Balance,
                                                    SportsId = item.SportsId,
                                                };
                                                transObjList.Add(transDTO);
                                            }

                                        }
                                        else
                                        {
                                            var checkTrans = transObjList.FirstOrDefault(x => x.Id == item.id);
                                            if (checkTrans == null)
                                            {
                                                double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == item.MarketName && x.MarketId == item.MarketId && x.SelectionId == item.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                TransactionDTO transDTO = new TransactionDTO()
                                                {
                                                    Id = item.id,
                                                    UserName = usrObj.UserName,
                                                    EventId = item.EventId,
                                                    MarketId = item.MarketId,
                                                    SelectionId = item.SelectionId,
                                                    Discription = item.Discription,
                                                    MarketName = item.MarketName,
                                                    Remark = item.Remark,
                                                    Amount = -profitLoss,
                                                    Commission = commission,
                                                    Balance = usrObj.Balance,
                                                    CreatedOn = item.createdOn,
                                                    SportsId = item.SportsId,
                                                };
                                                transObjList.Add(transDTO);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "Cash":
                                var cashObj = await db.Transaction.Where(x => x.UserId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == "Cash").OrderByDescending(x => x.id).ToListAsync();
                                if (cashObj.Count > 0)
                                {
                                    foreach (var obj in cashObj)
                                    {
                                        var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                        if (checkTrans == null)
                                        {
                                            TransactionDTO transDTO = new TransactionDTO()
                                            {
                                                Id = obj.id,
                                                UserName = usrObj.UserName,
                                                EventId = obj.EventId,
                                                MarketId = obj.MarketId,
                                                SelectionId = obj.SelectionId,
                                                Discription = obj.Discription,
                                                MarketName = obj.MarketName,
                                                Remark = obj.Remark,
                                                Amount = obj.Amount,
                                                Commission = 0.00,
                                                Balance = obj.Balance,
                                                CreatedOn = obj.createdOn,
                                                SportsId = obj.SportsId,
                                            };
                                            transObjList.Add(transDTO);
                                        }
                                    }
                                }
                                break;
                            case "Sports":
                                if (sportsId == 0)
                                {
                                    var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.MarketName != "Cash" && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                    if (sportsObj.Count > 0)
                                    {
                                        foreach (var obj in sportsObj)
                                        {
                                            if (obj.MarketName != "Fancy")
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        Id = obj.id,
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = -profitLoss,
                                                        Commission = commission,
                                                        CreatedOn = obj.createdOn,
                                                        Balance = usrObj.Balance,
                                                        SportsId = obj.SportsId,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                            else
                                            {
                                                var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                if (checkTrans == null)
                                                {
                                                    double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                    double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                    TransactionDTO transDTO = new TransactionDTO()
                                                    {
                                                        Id = obj.id,
                                                        UserName = usrObj.UserName,
                                                        EventId = obj.EventId,
                                                        MarketId = obj.MarketId,
                                                        SelectionId = obj.SelectionId,
                                                        Discription = obj.Discription,
                                                        MarketName = obj.MarketName,
                                                        Remark = obj.Remark,
                                                        Amount = -profitLoss,
                                                        Commission = commission,
                                                        Balance = usrObj.Balance,
                                                        CreatedOn = obj.createdOn,
                                                        SportsId = obj.SportsId,
                                                    };
                                                    transObjList.Add(transDTO);
                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (marketName == "All")
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.SportsId == sportsId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId, x.MarketName }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            Id = obj.id,
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            Id = obj.id,
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sportsObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.SportsId == sportsId && x.MarketName == marketName && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => new { x.MarketId }).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                                        if (sportsObj.Count > 0)
                                        {
                                            foreach (var obj in sportsObj)
                                            {
                                                if (obj.MarketName != "Fancy")
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            Id = obj.id,
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            CreatedOn = obj.createdOn,
                                                            Balance = usrObj.Balance,
                                                            SportsId = obj.SportsId,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                                else
                                                {
                                                    var checkTrans = transObjList.FirstOrDefault(x => x.Id == obj.id);
                                                    if (checkTrans == null)
                                                    {
                                                        double profitLoss = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                                        double commission = await db.Transaction.Where(x => x.SuperId == userId && !x.deleted && x.createdOn >= sDate && x.createdOn <= eDate && x.MarketName == obj.MarketName && x.MarketId == obj.MarketId && x.SelectionId == obj.SelectionId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();


                                                        TransactionDTO transDTO = new TransactionDTO()
                                                        {
                                                            Id = obj.id,
                                                            UserName = usrObj.UserName,
                                                            EventId = obj.EventId,
                                                            MarketId = obj.MarketId,
                                                            SelectionId = obj.SelectionId,
                                                            Discription = obj.Discription,
                                                            MarketName = obj.MarketName,
                                                            Remark = obj.Remark,
                                                            Amount = -profitLoss,
                                                            Commission = commission,
                                                            Balance = usrObj.Balance,
                                                            CreatedOn = obj.createdOn,
                                                            SportsId = obj.SportsId,
                                                        };
                                                        transObjList.Add(transDTO);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                }
                if (transObjList.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = transObjList.OrderByDescending(x => x.Id);
                    var sportsProfitLoss = new
                    {
                        cricket = transObjList.Where(x => x.SportsId == 4).Select(x => x.Amount).DefaultIfEmpty(0).Sum(),
                        football = transObjList.Where(x => x.SportsId == 1).Select(x => x.Amount).DefaultIfEmpty(0).Sum(),
                        tennis = transObjList.Where(x => x.SportsId == 2).Select(x => x.Amount).DefaultIfEmpty(0).Sum(),
                        tablegames = transObjList.Where(x => x.SportsId == 15).Select(x => x.Amount).DefaultIfEmpty(0).Sum(),
                        Casino = transObjList.Where(x => x.SportsId == 11).Select(x => x.Amount).DefaultIfEmpty(0).Sum(),
                    };
                    responseDTO.Pay = sportsProfitLoss;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = transObjList;
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
        [Route("ProfitLoss")]
        public async Task<IHttpActionResult> profitLoss(string role, int userId, int sportsId, DateTime sDate, DateTime eDate)
        {
            try
            {
                List<TransactionDTO> transObjList = new List<TransactionDTO>();
                List<TransactionModel> transObj = new List<TransactionModel>();
                var usrObj = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (role)
                {
                    case "Client":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.UserId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = item.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = profitLoss,
                                        Balance = -Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = item.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = profitLoss,
                                        Balance = -Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Agent":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperAgent":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Master":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                        Balance = Math.Round(usrObj.Share / 100 * Com, 2),
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperMaster":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                        Balance = Math.Round(usrObj.Share / 100 * Com, 2),
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
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
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                        Balance = Math.Round(usrObj.Share / 100 * Com, 2),
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Admin":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                        Balance = Math.Round(usrObj.Share / 100 * Com, 2),
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = Math.Round(usrObj.Share / 100 * -profitLoss, 2),
                                        Balance = Math.Round(usrObj.Share / 100 * Com, 2),
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperAdmin":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                double profitLoss = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                double Com = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();

                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = db.Bet.Where(x => x.EventId == item.EventId).FirstOrDefault().EventName,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                                else
                                {
                                    TransactionDTO transDTO = new TransactionDTO()
                                    {
                                        UserName = usrObj.UserName,
                                        EventId = item.EventId,
                                        Discription = item.Discription,
                                        Remark = "",
                                        Amount = -profitLoss,
                                        Balance = Com,
                                        CreatedOn = item.createdOn,
                                    };
                                    transObjList.Add(transDTO);
                                }
                            }
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                }
                if (transObjList.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = transObjList;
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
        [Route("ClientProfitLoss")]
        public async Task<IHttpActionResult> clientProfitLoss(string role, int userId, int sportsId, DateTime sDate, DateTime eDate, int skip, int takeRecord)
        {
            try
            {
                ArrayList marketIds = new ArrayList();
                ClientProfitLossDTO clientProfitLoss = new ClientProfitLossDTO();
                List<ClientProfitLossDTOList> clientProfitLossDTO = new List<ClientProfitLossDTOList>();
                ClientProfitLossDTOTotal clientProfitLossDTOTotal = new ClientProfitLossDTOTotal();
                List<TransactionModel> transObj = new List<TransactionModel>();
                var usrObj = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (role)
                {

                    case "Agent":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperAgent":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperAgentId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Master":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperMaster":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperMasterId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SubAdmin":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.SubAdminId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "Admin":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;// usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.AdminId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.MDL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                    case "SuperAdmin":
                        if (sportsId == 0)
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && !x.deleted && x.SportsId > 0 && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }
                        else
                        {
                            transObj = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && !x.deleted && x.SportsId == sportsId && x.createdOn >= sDate && x.createdOn <= eDate).GroupBy(x => x.EventId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToListAsync();
                        }

                        if (transObj.Count > 0)
                        {
                            foreach (var item in transObj)
                            {
                                if (item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["TableGamesSportsId"]) && item.SportsId != Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LiveGamesSportsId"]))
                                {
                                    double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                    double sessionCom = 0;//  usrObj.SessionCommission * await db.Bet.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Stake).DefaultIfEmpty(0).SumAsync() / 100;
                                    double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double session = -await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    double toss = -await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                    ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                    {
                                        EventId = item.EventId,
                                        MATCH = db.Event.FirstOrDefault(x => x.EventId == item.EventId).EventName,
                                        ODDS = odds,
                                        SESSION = session,
                                        TOSS = toss,
                                        MATCHCOMM = Math.Round(matchCom, 2),
                                        SESSIONCOMM = Math.Round(sessionCom, 2),
                                        COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                        NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                        DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                        MDL = 0,
                                        CreatedOn = item.createdOn,
                                    };
                                    clientProfitLossDTO.Add(clientProfitLossDTOList);
                                }
                                else
                                {
                                    if (!marketIds.Contains(item.MarketId))
                                    {
                                        marketIds.Add(item.MarketId);
                                        double matchCom = await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Commission).DefaultIfEmpty(0).SumAsync();
                                        double sessionCom = 0;
                                        double odds = -await db.Transaction.AsNoTracking().Where(x => x.SuperId == userId && x.EventId == item.EventId && x.MarketId == item.MarketId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                        double session = 0;
                                        double toss = 0;
                                        ClientProfitLossDTOList clientProfitLossDTOList = new ClientProfitLossDTOList()
                                        {
                                            EventId = item.EventId,
                                            MATCH = item.Discription,
                                            ODDS = odds,
                                            SESSION = session,
                                            TOSS = toss,
                                            MATCHCOMM = Math.Round(matchCom, 2),
                                            SESSIONCOMM = Math.Round(sessionCom, 2),
                                            COMMTOTAL = Math.Round(matchCom + sessionCom, 2),
                                            NETAMOUNT = Math.Round(matchCom + sessionCom + odds + session + toss, 2),
                                            DL = usrObj.Share * (matchCom + sessionCom + odds + session + toss) / 100,
                                            MDL = (100 - usrObj.Share) * (matchCom + sessionCom + odds + session + toss) / 100,
                                        };
                                        clientProfitLossDTO.Add(clientProfitLossDTOList);
                                    }
                                }
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                MATCHCOMMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM),
                                SESSIONCOMMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.COMMTOTAL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.DL),
                                MDLTOTAL = 0,
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        else
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "No Data";
                        }
                        break;
                }
                if (clientProfitLossDTO.Count > 0)
                {
                    clientProfitLoss.clientProfitLossDTOList = clientProfitLossDTO;
                    clientProfitLoss.clientProfitLossDTOTotal = clientProfitLossDTOTotal;
                    responseDTO.Status = true;
                    responseDTO.Result = clientProfitLoss;
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
        [Route("pL_Summary")]
        public async Task<IHttpActionResult> getPLSummary(string eventId, int userId)
        {
            try
            {
                ClientProfitLossDTO clientProfitLoss = new ClientProfitLossDTO();
                List<ClientDLProfitLossDTO> clientProfitLossDTO = new List<ClientDLProfitLossDTO>();
                ClientProfitLossDTOTotal clientProfitLossDTOTotal = new ClientProfitLossDTOTotal();
                List<TransactionModel> transObj = new List<TransactionModel>();
                List<TransactionModel> transObjGrpBy = new List<TransactionModel>();
                var userObj = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (userObj.Role)
                {
                    case "Agent":
                        transObj = await db.Transaction.AsNoTracking().Where(x => x.ParentId == userId && x.EventId == eventId).ToListAsync();
                        transObjGrpBy = transObj.GroupBy(x => x.UserId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToList();

                        if (transObjGrpBy.Count > 0)
                        {
                            foreach (var item in transObjGrpBy)
                            {
                                var userData = transObj.Where(x => x.UserId == item.UserId && x.MarketName == "Match Odds").FirstOrDefault();
                                double matchCom = userData == null ? 0 : userData.Commission;

                                double parentShare = 100 - userObj.Share;
                                double odds = transObj.Where(x => x.UserId == item.UserId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                double session = transObj.Where(x => x.UserId == item.UserId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                double toss = transObj.Where(x => x.UserId == item.UserId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                ClientDLProfitLossDTO clientDLProfitLossDTO = new ClientDLProfitLossDTO()
                                {
                                    NAME = item.UserName,
                                    ODDS = Math.Round(odds, 2),
                                    SESSION = Math.Round(session, 2),
                                    TOSS = Math.Round(toss, 2),
                                    TOTAL = Math.Round(odds + session + toss, 2),
                                    MATCHCOMM_DL = matchCom,
                                    SESSIONCOMM_DL = 0.00,
                                    TOTALCOM_DL = matchCom,
                                    MATCHCOMM_MDL = matchCom,
                                    SESSIONCOMM_MDL = 0.00,
                                    TOTALCOM_MDL = matchCom,
                                    NETAMOUNT = Math.Round(odds + session + toss - matchCom, 2),
                                    SHRAMT_DL = Math.Round(userObj.Share * (odds + session + toss - matchCom) / 100, 2),
                                    FINAL = Math.Round(odds + session + toss - matchCom - userObj.Share * (odds + session + toss - matchCom) / 100, 2),
                                };
                                clientProfitLossDTO.Add(clientDLProfitLossDTO);
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                TOTAL = clientProfitLossDTO.Sum(x => x.TOTAL),
                                DLMATCHCOMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM_DL),
                                DLSESSIONCOMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM_DL),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.TOTALCOM_DL),
                                MDLMATCHCOMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM_MDL),
                                MDLSESSIONCOMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM_MDL),
                                MDLCOMMTOTAL = clientProfitLossDTO.Sum(x => x.TOTALCOM_MDL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.SHRAMT_DL),
                                FINALTOTAL = clientProfitLossDTO.Sum(x => x.FINAL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }
                        break;
                    case "Master":
                        transObj = await db.Transaction.AsNoTracking().Where(x => x.MasterId == userId && x.EventId == eventId).ToListAsync();
                        transObjGrpBy = transObj.GroupBy(x => x.ParentId).Select(x => x.FirstOrDefault()).OrderByDescending(x => x.id).ToList();

                        if (transObjGrpBy.Count > 0)
                        {
                            foreach (var item in transObjGrpBy)
                            {
                                var parentObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == item.ParentId);
                                var superParentObj = await db.SignUp.FirstOrDefaultAsync(x => x.id == parentObj.ParentId);
                                double matchCom = transObj.Where(x => x.ParentId == item.ParentId && x.MarketName == "Match Odds").Select(x => x.Commission).DefaultIfEmpty(0).Sum();

                                double parentShare = superParentObj.Share - parentObj.Share;
                                double odds = transObj.Where(x => x.ParentId == item.ParentId && (x.MarketName == "Match Odds" || x.MarketName == "BookMaker")).Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                double session = transObj.Where(x => x.ParentId == item.ParentId && x.MarketName == "Fancy").Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                double toss = transObj.Where(x => x.ParentId == item.ParentId && x.MarketName == "To Win the Toss").Select(x => x.Amount).DefaultIfEmpty(0).Sum();
                                ClientDLProfitLossDTO clientDLProfitLossDTO = new ClientDLProfitLossDTO()
                                {
                                    NAME = parentObj.UserName,
                                    ODDS = Math.Round(odds, 2),
                                    SESSION = Math.Round(session, 2),
                                    TOSS = Math.Round(toss, 2),
                                    TOTAL = Math.Round(odds + session + toss, 2),
                                    MATCHCOMM_DL = matchCom,
                                    SESSIONCOMM_DL = 0.00,
                                    TOTALCOM_DL = matchCom,
                                    MATCHCOMM_MDL = matchCom,
                                    SESSIONCOMM_MDL = 0.00,
                                    TOTALCOM_MDL = matchCom,
                                    NETAMOUNT = Math.Round(odds + session + toss - matchCom, 2),
                                    SHRAMT_DL = Math.Round(parentObj.Share * (odds + session + toss - matchCom) / 100, 2),
                                    SHRAMT_MDL = Math.Round(parentShare * (odds + session + toss - matchCom) / 100, 2),
                                    FINAL = Math.Round(odds + session + toss - matchCom - (parentObj.Share * (odds + session + toss - matchCom) / 100 + parentShare * (odds + session + toss - matchCom) / 100)),
                                };
                                clientProfitLossDTO.Add(clientDLProfitLossDTO);
                            }
                            ClientProfitLossDTOTotal clientProfitLossDTOTotal1 = new ClientProfitLossDTOTotal()
                            {
                                ODDSTOTAL = clientProfitLossDTO.Sum(x => x.ODDS),
                                SESSIONTOTAL = clientProfitLossDTO.Sum(x => x.SESSION),
                                TOSSTOTAL = clientProfitLossDTO.Sum(x => x.TOSS),
                                TOTAL = clientProfitLossDTO.Sum(x => x.TOTAL),
                                DLMATCHCOMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM_DL),
                                DLSESSIONCOMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM_DL),
                                COMMTOTAL = clientProfitLossDTO.Sum(x => x.TOTALCOM_DL),
                                MDLMATCHCOMTOTAL = clientProfitLossDTO.Sum(x => x.MATCHCOMM_MDL),
                                MDLSESSIONCOMTOTAL = clientProfitLossDTO.Sum(x => x.SESSIONCOMM_MDL),
                                MDLCOMMTOTAL = clientProfitLossDTO.Sum(x => x.TOTALCOM_MDL),
                                NETAMOUNTTOTAL = clientProfitLossDTO.Sum(x => x.NETAMOUNT),
                                DLTOTAL = clientProfitLossDTO.Sum(x => x.SHRAMT_DL),
                                MDLTOTAL = clientProfitLossDTO.Sum(x => x.SHRAMT_MDL),
                                FINALTOTAL = clientProfitLossDTO.Sum(x => x.FINAL),
                            };
                            clientProfitLossDTOTotal = clientProfitLossDTOTotal1;
                        }

                        break;
                    case "SubAdmin":

                        break;
                    case "Admin":

                        break;
                }

                if (clientProfitLossDTO.Count > 0)
                {
                    clientProfitLoss.clientDLProfitLossDTOList = clientProfitLossDTO;
                    clientProfitLoss.clientProfitLossDTOTotal = clientProfitLossDTOTotal;
                    responseDTO.Status = true;
                    responseDTO.Result = clientProfitLoss;
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
        [Route("chips_Detail")]
        public async Task<IHttpActionResult> getChipDetails(int _id, string role)
        {
            try
            {
                double plusSum = 0;
                double minSum = 0;
                double diff = 0;
                double bal = 0;
                double chips = 0;
                //double com = 0;
                //int _id = Convert.ToInt32(run_time.RunTimeUserId());
                ChipsDTO chipObj = new ChipsDTO();
                List<Plus> plusList = new List<Plus>();
                List<Minus> minusList = new List<Minus>();
                double ownBalance = 0;
                var signObj = await db.SignUp.AsNoTracking().Select(x => new { x.Role, x.CreditLimit, x.id, x.Balance, x.ParentId, x.deleted, x.UserId, x.Share }).Where(x => x.ParentId == _id && x.Role != "Client" && !x.deleted).ToListAsync();
                if (signObj.Count > 0)
                {
                    foreach (var item in signObj)
                    {
                        bal = 0;
                        chips = 0;
                        switch (role)
                        {   
                            case "SuperAdmin":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.AdminId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "Admin":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.SubAdminId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "SubAdmin":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.SuperMasterId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "SuperMaster":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.MasterId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "Master":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.SuperAgentId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "SuperAgent":
                                bal = Math.Round(item.Balance + await db.SignUp.Where(x => x.ParentId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                                break;
                            case "Agent":
                                //bal = Math.Round(item.Balance, 2);
                                bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                                break;
                        }

                        chips = Math.Round((100 - item.Share) / 100 * (bal - item.CreditLimit), 2);
                        //chips = Math.Round(bal - item.CreditLimit, 2);
                        if (chips >= 0)
                        {
                            Plus plusObj = new Plus()
                            {
                                id = item.id,
                                UserId = item.UserId,
                                Chips = chips,
                                Role = item.Role,
                            };
                            plusList.Add(plusObj);
                        }
                        else
                        {
                            Minus minusObj = new Minus()
                            {
                                id = item.id,
                                UserId = item.UserId,
                                Chips = -chips,
                                Role = item.Role,
                            };
                            minusList.Add(minusObj);
                        }
                    }


                    plusSum = plusList.Select(x => x.Chips).DefaultIfEmpty(0).Sum();
                    minSum = minusList.Select(x => x.Chips).DefaultIfEmpty(0).Sum();
                    diff = plusSum - minSum;
                    if (diff > 0)
                    {
                        Minus minObj = new Minus()
                        {
                            UserId = db.SignUp.AsNoTracking().Where(x => x.id == _id).FirstOrDefault().UserId + "(Parent)",
                            Chips = diff,
                        };
                        minusList.Add(minObj);
                        responseDTO.Count = plusSum;
                    }
                    if (diff < 0)
                    {
                        Plus pluObj = new Plus()
                        {
                            UserId = db.SignUp.AsNoTracking().Where(x => x.id == _id).FirstOrDefault().UserId + "(Parent)",
                            Chips = -diff,
                        };
                        plusList.Add(pluObj);
                        responseDTO.Count = minSum;
                    }
                }
                switch (role)
                {
                    case "Admin":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.AdminId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                    case "SubAdmin":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.SubAdminId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                    case "SuperMaster":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.SuperMasterId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                    case "Master":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.MasterId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                    case "SuperAgent":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.SuperAgentId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                    case "Agent":
                        ownBalance = Math.Round(await db.SignUp.Where(x => x.id == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync() + await db.SignUp.Where(x => x.ParentId == _id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);

                        break;
                }
                var signUpData = await db.SignUp.AsNoTracking().Where(x => x.id == _id).Select(x => new { x.Share, x.CreditLimit }).FirstOrDefaultAsync();
                if (plusList.Count > 0 || minusList.Count > 0)
                {
                    chipObj.PlusObj = plusList.OrderByDescending(x => x.Chips).ToList();
                    chipObj.MinusObj = minusList.OrderByDescending(x => x.Chips).ToList();
                    responseDTO.Status = true;
                    responseDTO.Pay = Math.Round((100 - signUpData.Share) / 100 * (ownBalance - signUpData.CreditLimit), 2);
                    responseDTO.Result = chipObj;
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
        [Route("Chip_Settlement")]
        public async Task<IHttpActionResult> Settlement(int userId, double amount, string type, int _id, string role)
        {
            using (var dbContextTransaction = db.Database.BeginTransaction())
            {
                try
                {
                    double bal = 0;
                    double chips = 0;
                    //int _id = Convert.ToInt32(run_time.RunTimeUserId());
                    var userObj = await db.SignUp.Where(x => x.id == userId).FirstOrDefaultAsync();
                    var parentObj = await db.SignUp.Where(x => x.id == _id).FirstOrDefaultAsync();

                    switch (role)
                    {
                        case "SuperAdmin":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.AdminId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "Admin":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.SubAdminId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "SubAdmin":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.SuperMasterId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "SuperMaster":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.SuperAgentId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "Master":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.SuperAgentId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "SuperAgent":
                            bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.ParentId == userObj.id).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                            break;
                        case "Agent":
                            bal = await db.Transaction.AsNoTracking().Where(x => x.UserId == userObj.id).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
                            break;
                    }
                    //bal = Math.Round(userObj.Balance + await db.SignUp.Where(x => x.ParentId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync(), 2);
                    chips = Math.Round((100 - userObj.Share) / 100 * (bal - userObj.CreditLimit), 2);
                    if (chips < 0)
                    {
                        chips = -chips;
                    }
                    if (amount <= chips)
                    {
                        double checkBal = 100 * amount / (100 - userObj.Share);
                        if (type == "PLUS")
                        {
                            if (checkBal <= userObj.Balance)
                            {
                                userObj.Balance = Math.Round(userObj.Balance - checkBal, 2);
                                parentObj.Balance = Math.Round(parentObj.Balance + checkBal, 2);
                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Low Balance for settlement";
                                return Ok(responseDTO);
                            }

                        }
                        else
                        {
                            if (checkBal <= parentObj.Balance)
                            {
                                userObj.Balance = Math.Round(userObj.Balance + checkBal, 2);
                                parentObj.Balance = Math.Round(parentObj.Balance - checkBal, 2);
                            }
                            else
                            {
                                responseDTO.Status = false;
                                responseDTO.Result = "Low Balance for settlement";
                                return Ok(responseDTO);
                            }
                        }

                        if (type == "PLUS")
                        {
                            TransactionModel transChild = new TransactionModel()
                            {
                                UserId = userId,
                                UserName = userObj.UserId,
                                SportsId = 0,
                                EventId = "Settlement",
                                MarketId = "Balance",
                                SelectionId = "Minus",
                                Discription = "Settlement done by Parent",
                                MarketName = "Cash",
                                Remark = "Settlement",
                                Amount = -checkBal,
                                Balance = userObj.Balance,
                                ParentId = userObj.ParentId,
                                SuperAgentId = userObj.SuperAgentId,
                                MasterId = userObj.MasterId,
                                SuperMasterId = userObj.SuperMasterId,
                                SubAdminId = userObj.SubAdminId,
                                AdminId = userObj.AdminId,
                                SuperId = userObj.SuperId,
                                status = false,
                                deleted = false,
                                createdOn = DateTime.Now,
                                Parent = 1,
                            };
                            TransactionModel transParnt = new TransactionModel()
                            {
                                UserId = parentObj.id,
                                UserName = parentObj.UserId,
                                SportsId = 0,
                                EventId = "Settlement",
                                MarketId = "Balance",
                                SelectionId = "Plus",
                                Discription = "Settlement to " + userObj.UserId,
                                MarketName = "Cash",
                                Remark = "Settlement",
                                Amount = checkBal,
                                Balance = parentObj.Balance,
                                ParentId = userObj.ParentId,
                                SuperAgentId = userObj.SuperAgentId,
                                MasterId = userObj.MasterId,
                                SuperMasterId = userObj.SuperMasterId,
                                SubAdminId = userObj.SubAdminId,
                                AdminId = userObj.AdminId,
                                SuperId = userObj.SuperId,
                                status = false,
                                deleted = false,
                                createdOn = DateTime.Now,
                                Parent = 1,
                            };
                            db.Transaction.Add(transParnt);
                            db.Transaction.Add(transChild);
                        }
                        else
                        {
                            TransactionModel transChild = new TransactionModel()
                            {
                                UserId = userId,
                                UserName = userObj.UserId,
                                SportsId = 0,
                                EventId = "Settlement",
                                MarketId = "Balance",
                                SelectionId = "Plus",
                                Discription = "Settlement given by Parent",
                                MarketName = "Cash",
                                Remark = "Settlement",
                                Amount = checkBal,
                                Balance = userObj.Balance,
                                ParentId = userObj.ParentId,
                                SuperAgentId = userObj.SuperAgentId,
                                MasterId = userObj.MasterId,
                                SuperMasterId = userObj.SuperMasterId,
                                SubAdminId = userObj.SubAdminId,
                                AdminId = userObj.AdminId,
                                SuperId = userObj.SuperId,
                                status = false,
                                deleted = false,
                                createdOn = DateTime.Now,
                                Parent = 1,
                            };
                            TransactionModel transParnt = new TransactionModel()
                            {
                                UserId = parentObj.id,
                                UserName = parentObj.UserId,
                                SportsId = 0,
                                EventId = "Settlement",
                                MarketId = "Balance",
                                SelectionId = "Minus",
                                Discription = "Settlement to " + userObj.UserId,
                                MarketName = "Cash",
                                Remark = "Settlement",
                                Amount = -checkBal,
                                Balance = parentObj.Balance,
                                ParentId = userObj.ParentId,
                                SuperAgentId = userObj.SuperAgentId,
                                MasterId = userObj.MasterId,
                                SuperMasterId = userObj.SuperMasterId,
                                SubAdminId = userObj.SubAdminId,
                                AdminId = userObj.AdminId,
                                SuperId = userObj.SuperId,
                                status = false,
                                deleted = false,
                                createdOn = DateTime.Now,
                                Parent = 1,
                            };
                            db.Transaction.Add(transParnt);
                            db.Transaction.Add(transChild);

                        }
                        await db.SaveChangesAsync();
                        dbContextTransaction.Commit();

                        responseDTO.Status = true;
                        responseDTO.Result = "Executed Successfully";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Invalid Amount Entered";
                        return Ok(responseDTO);
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
        [Route("Chip_Summary")]
        public async Task<IHttpActionResult> getChipSummary(int take, int skipRec, DateTime sDate, DateTime eDate)
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());

                var chipObj = await db.Transaction.AsNoTracking().Where(x => x.UserId == id && x.Remark == "Settlement" && x.createdOn >= sDate && x.createdOn <= eDate).OrderByDescending(x => x.id).Skip(skipRec).Take(take).ToListAsync();
                if (chipObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = chipObj;
                    responseDTO.Count = await db.Transaction.AsNoTracking().CountAsync(x => x.UserId == id && x.Remark == "Settlement" && x.createdOn >= sDate && x.createdOn <= eDate);
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = chipObj;
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
        [Route("GetBalDetail")]
        public async Task<IHttpActionResult> getBalDetail(string role, int userId)
        {
            try
            {
                BalanceDetailDTO balObj = new BalanceDetailDTO();
                var userObj = await db.SignUp.AsNoTracking().Where(x => x.id == userId).FirstOrDefaultAsync();
                switch (role)
                {
                    case "Client":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = await db.Transaction.Where(x => x.UserId == userId).Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.UserId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.OwnBal + balObj.DownLineExp;

                        balObj.ProfitLoss = balObj.OwnBal - balObj.CreditLimit;
                        break;
                    case "Agent":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.ParentId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.ParentId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "SuperAgent":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.SuperAgentId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.SuperAgentId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "Master":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.MasterId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.MasterId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "SuperMaster":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.SuperMasterId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.SuperMasterId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "SubAdmin":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.SubAdminId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.SubAdminId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;


                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "Admin":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.AdminId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.AdminId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                    case "SuperAdmin":
                        balObj.CreditLimit = userObj.CreditLimit;

                        balObj.OwnBal = userObj.Balance;

                        balObj.DownLineBal = await db.SignUp.Where(x => x.SuperId == userId).Select(x => x.Balance).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineExp = await db.Exposure.Where(x => x.SuperId == userId && !x.deleted).Select(x => x.Exposure).DefaultIfEmpty(0).SumAsync();

                        balObj.DownLineAvailBal = balObj.DownLineBal + balObj.DownLineExp;

                        balObj.TotalBal = balObj.OwnBal + balObj.DownLineBal;

                        balObj.ProfitLoss = balObj.TotalBal - balObj.CreditLimit;
                        break;
                }
                if (userObj != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = balObj;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = balObj;
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
        //[Route("SetCreditLimit")]
        //[AllowAnonymous]
        //public async Task<IHttpActionResult> SetCreditLimit()
        //{
        //    try
        //    {
        //        var users = await db.SignUp.Where(x => x.Role == "Client").ToListAsync();
        //        if (users.Count > 0)
        //        {
        //            foreach (var item in users)
        //            {
        //                double creditLimit = await db.Transaction.AsNoTracking().Where(x => x.UserId == item.id && x.MarketName == "Cash").Select(x => x.Amount).DefaultIfEmpty(0).SumAsync();
        //                item.CreditLimit = creditLimit;

        //            }
        //            await db.SaveChangesAsync();
        //            responseDTO.Status = true;
        //            responseDTO.Result = "Success";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        responseDTO.Status = false;
        //        responseDTO.Result = ex.Message;
        //    }
        //    return Ok(responseDTO);
        //}

        //[HttpGet]
        //[Route("OpeningBalance")]
        //[AllowAnonymous]
        //public async Task<IHttpActionResult> OpeningBalance()
        //{
        //    try
        //    {
        //        List<TransactionModel> transactionModels = new List<TransactionModel>();
        //        var signUp = await db.SignUp.Where(x => x.deleted == false && x.Role != "Client").ToListAsync();
        //        if (signUp.Count > 0)
        //        {
        //            foreach (var item in signUp)
        //            {
        //                switch (item.Role)
        //                {
        //                    case "Master":
        //                        item.CreditLimit = item.Balance + db.SignUp.Where(x => x.MasterId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum();
        //                        break;
        //                    case "Agent":
        //                        item.CreditLimit = item.Balance + db.SignUp.Where(x => x.ParentId == item.id).Select(x => x.Balance).DefaultIfEmpty(0).Sum();
        //                        break;
        //                }

        //            }
        //            await db.SaveChangesAsync();
        //            responseDTO.Status = true;
        //            responseDTO.Result = "Done";
        //        }
        //        else
        //        {
        //            responseDTO.Status = false;
        //            responseDTO.Result = "No Client";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        responseDTO.Status = false;
        //        responseDTO.Result = ex.Message;
        //    }
        //    return Ok(responseDTO);
        //}



    }
}