using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Description;
using AutoMapper;
using FixWebApi.Authentication;
using FixWebApi.Models;
using FixWebApi.Models.DTO;
using Newtonsoft.Json;

namespace FixWebApi.Controllers
{
    [RoutePrefix("CasinoSettings")]
    [CustomAuthorization]
    public class CasinoSettingsModelsController : ApiController
    {
        private FixDbContext db = new FixDbContext();
        ResponseDTO responseDTO = new ResponseDTO();


        [HttpPost]
        [Route("UpdateCasinoSettings")]
        public async Task<IHttpActionResult> UpdateCasinoSettings(CasinoSettingsModel casinoSettingsModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var casinoSettings = await db.CasinoSettingsModel.Where(x => x.deleted == false).FirstOrDefaultAsync();
            casinoSettings.ExtraParameter = casinoSettingsModel.ExtraParameter;
            casinoSettings.Url = casinoSettingsModel.Url;
            casinoSettings.Currency = casinoSettingsModel.Currency;
            casinoSettings.Environment = casinoSettingsModel.Environment;

            try
            {
                await db.SaveChangesAsync();
                responseDTO.Status = true;
                responseDTO.Result = "Done";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CasinoSettingsModelExists(casinoSettingsModel.id))
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Not found";
                }
                else
                {
                    throw;
                }
            }

            return Ok(responseDTO);
        }

        [HttpPost]
        [Route("AddCasinoSettings")]
        public async Task<IHttpActionResult> AddCasinoSettings(CasinoSettingsModel casinoSettingsModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var checkIsExist = await db.CasinoSettingsModel.Where(x => x.deleted == false).FirstOrDefaultAsync();
                if (checkIsExist != null)
                {
                    checkIsExist = Mapper.Map<CasinoSettingsModel>(casinoSettingsModel);

                }
                else
                {
                    casinoSettingsModel.createdOn = DateTime.Now;
                    casinoSettingsModel.status = false;
                    casinoSettingsModel.deleted = false;
                    db.CasinoSettingsModel.Add(casinoSettingsModel);
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
        [Route("GetCasinoSettings")]
        public async Task<IHttpActionResult> GetCasinoSettings()
        {
            try
            {
                var casionSettings = await db.CasinoSettingsModel.AsNoTracking().Select(x => new { x.id, x.Environment, x.Url, x.Currency, x.ExtraParameter }).FirstOrDefaultAsync();

                responseDTO.Status = casionSettings == null ? false : true;
                responseDTO.Result = casionSettings;
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpPost]
        [Route("AddGameProviders")]
        public async Task<IHttpActionResult> AddGameProviders(string systemId, string providerName)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var httpRequest = HttpContext.Current.Request;
                    if (httpRequest.Files == null)
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Please Upload Your file";
                    }
                    else if (httpRequest.Files.Count > 0)
                    {
                        var file = httpRequest.Files[0];
                        string[] AllowedFileExtensions = new string[] { ".jpg" };
                        if (!AllowedFileExtensions.Contains(file.FileName.Substring(file.FileName.LastIndexOf('.'))))
                        {
                            responseDTO.Status = false;
                            responseDTO.Result = "Please file of type: " + string.Join(", ", AllowedFileExtensions);
                        }
                        else
                        {
                            //TO:DO
                            var fileName = Path.GetFileName(file.FileName);
                            var path = Path.Combine(HttpContext.Current.Server.MapPath("/Content/casino/"), fileName);
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                            file.SaveAs(path);
                            var checkIsExist = await db.LiveCasinoProvidersModel.Where(x => x.SystemId.Equals(systemId)).FirstOrDefaultAsync();
                            if (checkIsExist != null)
                            {
                                checkIsExist.ImagePath = path.ToString().Replace("C:\\", "https://");

                                responseDTO.Status = true;
                                responseDTO.Result = "success";
                            }
                            else
                            {
                                LiveCasinoProvidersModel liveCasinoProviders = new LiveCasinoProvidersModel()
                                {
                                    SystemId = systemId,
                                    ProviderName = providerName,
                                    ImagePath = path.ToString().Replace("C:\\", "https://"),
                                    createdOn = DateTime.Now,
                                    status = false,
                                    deleted = false
                                };


                                db.LiveCasinoProvidersModel.Add(liveCasinoProviders);
                            }

                            await db.SaveChangesAsync();
                            ModelState.Clear();
                            responseDTO.Status = true;
                            responseDTO.Result = path;

                            responseDTO.Status = true;
                            responseDTO.Result = "success";
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Please attach the file";
                    }
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Please attach the file";
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetCasinoProviders")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetCasinoProviders(string role)
        {
            try
            {

                if (role == "SuperAdmin")
                {
                    var casinoProviders = await db.LiveCasinoProvidersModel.AsNoTracking().OrderByDescending(x=>x.id).ToListAsync();
                    if (casinoProviders.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = casinoProviders;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = casinoProviders;
                    }
                }
                else
                {
                    var casinoProviders = await db.LiveCasinoProvidersModel.AsNoTracking().Where(x => !x.deleted).OrderByDescending(x => x.id).ToListAsync();
                    if (casinoProviders.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = casinoProviders;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = casinoProviders;
                    }
                }

            }
            catch (Exception ex)
            {

                throw ex;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("BlockUnBlockProvider")]
        public async Task<IHttpActionResult> BlockUnBlockProvider(string systemId, bool status)
        {
            try
            {
                var casinoGames = await db.LiveCasinoProvidersModel.Where(x => x.SystemId.Equals(systemId)).FirstOrDefaultAsync();
                if (casinoGames != null)
                {
                    casinoGames.deleted = status;
                    await db.SaveChangesAsync();
                    responseDTO.Status = true;
                    responseDTO.Result = casinoGames;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = casinoGames;
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("AddCasinoGames")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> AddCasinoGames()
        {
            try
            {
                string fundistUrl = ConfigurationManager.AppSettings["FundistCasinoGamesUrl"];

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage response = await client.GetAsync(fundistUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadAsStringAsync();
                        List<LiveCasinoGamesDTO> liveCasinoGamesDTOs = JsonConvert.DeserializeObject<List<LiveCasinoGamesDTO>>(data);

                        if (liveCasinoGamesDTOs.Count > 0)
                        {
                            List<LiveCasinoGamesModel> liveCasinoSystemIdAndPageCodes = new List<LiveCasinoGamesModel>();

                            foreach (var item in liveCasinoGamesDTOs)
                            {
                                var checkGameIsExist = await db.LiveCasinoGamesModel.AsNoTracking()
                                    .FirstOrDefaultAsync(x => x.SystemId.Equals(item.System) && x.PageCode.Equals(item.PageCode));

                                if (checkGameIsExist == null)
                                {
                                    LiveCasinoGamesModel liveCasinoGames = new LiveCasinoGamesModel()
                                    {
                                        SystemId = item.System,
                                        PageCode = item.PageCode,
                                        MerchantName = item.MerchantName,
                                        GameName = item.Trans.en,
                                        TableId = item.TableID,
                                        ImagePath = item.ImageFullPath,
                                        createdOn = DateTime.Now,
                                        status = false,
                                        deleted = false,
                                    };

                                    liveCasinoSystemIdAndPageCodes.Add(liveCasinoGames);
                                }
                            }

                            db.LiveCasinoGamesModel.AddRange(liveCasinoSystemIdAndPageCodes);
                            await db.SaveChangesAsync();

                            responseDTO.Status = true;
                            responseDTO.Result = "Success";
                        }
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "No response";
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(responseDTO);
        }


        [HttpGet]
        [Route("GetCasinoGames")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetCasinoGames(string systemId, string role)
        {
            try
            {
                IQueryable<LiveCasinoGamesModel> casinoGamesQuery = db.LiveCasinoGamesModel.AsNoTracking().Where(x => x.SystemId.Equals(systemId));

                if (role != "SuperAdmin")
                {
                    casinoGamesQuery = casinoGamesQuery.Where(x => !x.status);
                }

                var casinoGames = await casinoGamesQuery.ToListAsync();

                responseDTO.Status = casinoGames.Count > 0;
                responseDTO.Result = casinoGames;
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }

            return Ok(responseDTO);
        }



        [HttpGet]
        [Route("BlockUnBlockCasinoGame")]
        public async Task<IHttpActionResult> BlockUnBlockCasinoGame(string systemId, string pageCode, bool status)
        {
            try
            {
                var casinoGame = await db.LiveCasinoGamesModel.FirstOrDefaultAsync(x => x.SystemId.Equals(systemId) && x.PageCode.Equals(pageCode));

                if (casinoGame != null)
                {
                    casinoGame.status = status;
                    await db.SaveChangesAsync();
                    responseDTO.Status = true;
                    responseDTO.Result = casinoGame;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = null;
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
        [Route("AddTableGames")]
        public async Task<IHttpActionResult> AddTableGames(string gameName, string apiUrl, string resultUrl, string videoUrl)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Invalid model state";
                    return Ok(responseDTO);
                }

                if (HttpContext.Current.Request.Files == null || HttpContext.Current.Request.Files.Count == 0)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Please Upload Your file";
                    return Ok(responseDTO);
                }

                var file = HttpContext.Current.Request.Files[0];
                string[] AllowedFileExtensions = new string[] { ".jpg" };
                if (!AllowedFileExtensions.Contains(Path.GetExtension(file.FileName).ToLower()))
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Please upload a file of type: " + string.Join(", ", AllowedFileExtensions);
                    return Ok(responseDTO);
                }

                var fileName = Path.GetFileName(file.FileName);
                var path = Path.Combine(HttpContext.Current.Server.MapPath("/Content/casino/"), fileName);
                if (!File.Exists(path))
                {
                    file.SaveAs(path);
                }
                

                var existingTableGame = await db.TableGamesModel.FirstOrDefaultAsync(x => x.GameName.ToLower() == gameName.ToLower());
                if (existingTableGame != null)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Game with the same name already exists";
                    return Ok(responseDTO);
                }

                TableGamesModel tableGamesModel = new TableGamesModel()
                {
                    MarketId = Guid.NewGuid().ToString(),
                    GameName = gameName,
                    APIUrl = apiUrl,
                    VedioUrl = videoUrl,
                    ResultAPIUrl = resultUrl,
                    ImagePath = path.Replace("C:\\", "https://"),
                    SportsId = Convert.ToInt32(ConfigurationManager.AppSettings["TableGamesSportsId"]),
                    status = false,
                    deleted = false,
                    createdOn = DateTime.Now
                };

                db.TableGamesModel.Add(tableGamesModel);

                var eventId = ConfigurationManager.AppSettings["TableEventId"];
                var eventData = await db.Event.AsNoTracking().FirstOrDefaultAsync(x => x.EventId == eventId);
                if (eventData == null)
                {
                    EventModel eventModel = new EventModel()
                    {
                        EventId = eventId,
                        SportsId = Convert.ToInt32(ConfigurationManager.AppSettings["TableGamesSportsId"]),
                        EventName = "Table Casino",
                        EventTime = DateTime.Now,
                        SportsName = "Table Games",
                        Betdelay = 1,
                        MaxStake = 50000,
                        MinStake = 500,
                        MaxProfit = 1000000,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now
                    };
                    db.Event.Add(eventModel);
                }

                MarketModel market = new MarketModel()
                {
                    EventId = eventId,
                    MarketId = tableGamesModel.MarketId,
                    marketName = tableGamesModel.GameName,
                    Result = "",
                    Betdelay = 1,
                    Fancydelay = 0,
                    MaxStake = 500,
                    MinStake = 1000000,
                    createdOn = DateTime.Now
                };
                db.Market.Add(market);

                int runnerLength = gameName == "Amar Akbar Anthony" ? 3 : 2;
                int no = new Random().Next(100, 1000);
                for (int i = 0; i < runnerLength; i++)
                {
                    string runnerName = gameName == "Amar Akbar Anthony" ? (i == 0 ? "Amar" : i == 1 ? "Akbar" : "Anthony") : gameName == "Andar Bahar" ? (i == 0 ? "SA" : "SB") : gameName == "DragonTiger20-20" ? (i == 0 ? "Dragon" : "Tiger") : i == 0 ? "PlayerA" : "PlayerB";
                    RunnerModel runner = new RunnerModel()
                    {
                        EventId = eventId,
                        MarketId = tableGamesModel.MarketId,
                        RunnerId = no + i,
                        RunnerName = runnerName,
                        Book = 0.00,
                        MarketName = tableGamesModel.GameName,
                        status = false,
                        deleted = false,
                        createdOn = DateTime.Now
                    };
                    db.Runner.Add(runner);
                }

                await db.SaveChangesAsync();
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
        [Route("GetTableGames")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetTableGames(string role)
        {
            try
            {
                List<TableGamesModel> tableGames = null;
                switch (role)
                {
                    case "SuperAdmin":
                        tableGames = await db.TableGamesModel.AsNoTracking().ToListAsync();
                        break;
                    case "Client":
                        tableGames = await db.TableGamesModel.AsNoTracking().Where(x => !x.deleted && !x.status).ToListAsync();
                        break;
                }

                responseDTO.Status = tableGames != null && tableGames.Count > 0;
                responseDTO.Result = tableGames ?? new List<TableGamesModel>();
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }

            return Ok(responseDTO);
        }


        [HttpPost]
        [Route("UpdateTableGames")]
        public async Task<IHttpActionResult> UpdateTableGames(TableGamesModel tableGamesModel)
        {
            try
            {
                var existingTableGames = await db.TableGamesModel.FirstOrDefaultAsync(x => x.GameName.ToLower() == tableGamesModel.GameName.ToLower());
                if (existingTableGames != null)
                {
                    existingTableGames.GameName = tableGamesModel.GameName;
                    existingTableGames.APIUrl = tableGamesModel.APIUrl;
                    existingTableGames.ResultAPIUrl = tableGamesModel.ResultAPIUrl;
                    existingTableGames.VedioUrl = tableGamesModel.VedioUrl;
                    existingTableGames.status = tableGamesModel.status;
                    existingTableGames.deleted = tableGamesModel.deleted;
                    await db.SaveChangesAsync();
                    responseDTO.Status = true;
                    responseDTO.Result = "Done";
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



        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool CasinoSettingsModelExists(int id)
        {
            return db.CasinoSettingsModel.Count(e => e.id == id) > 0;
        }
    }
}