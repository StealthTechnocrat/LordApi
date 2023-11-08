using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using FixWebApi.Authentication;
using FixWebApi.Models;
using FixWebApi.Models.DTO;

namespace FixWebApi.Controllers
{
    [RoutePrefix("Chip")]
    [CustomAuthorization]
    public class ChipModelsController : ApiController
    {
        ResponseDTO responseDTO = new ResponseDTO();
        Runtime run_time = new Runtime();
        private FixDbContext db = new FixDbContext();


        // Post: api/AddChips
        [HttpPost]
        [Route("AddChips")]
        public async Task<IHttpActionResult> PostChipModel(List<ChipModel> chipModel)
        {
            try
            {
                int id = 1;// Convert.ToInt32(run_time.RunTimeUserId());
                var chipObj = await db.Chip.Where(x => x.UserId == id).ToListAsync();
                if (chipObj.Count > 0)
                {
                    foreach (var item in chipModel)
                    {
                        foreach (var obj in chipObj)
                        {
                            if (obj.id == item.id)
                            {
                                obj.ChipName = item.ChipName;
                                obj.ChipValue = item.ChipValue;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    db.Chip.AddRange(chipModel);
                }
                int returnValue = await db.SaveChangesAsync();
                if (returnValue > 0)
                {
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

        //Get: api/GetChips
        [HttpGet]
        [Route("GetChips")]
        public async Task<IHttpActionResult> GetChips()
        {
            try
            {
                int id = Convert.ToInt32(run_time.RunTimeUserId());
                var chipObj = await (from s in db.SignUp
                                     where s.id == id
                                     from c in db.Chip
                                     where c.UserId == s.id
                                     select new
                                     {
                                         c.id,
                                         c.ChipName,
                                         c.ChipValue,
                                     }).AsNoTracking().ToListAsync();
                if (chipObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = chipObj;
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

        //Get: api/AddNews
        [HttpPost]
        [Route("AddNews")]
        public async Task<IHttpActionResult> PostNews(NewsModel newsModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                else
                {
                    newsModel.createdOn = DateTime.Now;
                    db.News.Add(newsModel);
                    int returnVal = await db.SaveChangesAsync();
                    if (returnVal > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Done";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Error";
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
        [Route("GetNews")]
        public async Task<IHttpActionResult> GetNews()
        {
            try
            {
                var newsObj = await db.News.AsNoTracking().Where(x => !x.deleted).Select(x => new
                {
                    x.id,
                    x.News,
                    x.createdOn,
                }).ToListAsync();
                if (newsObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = newsObj;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = newsObj;
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        //Delete : api/UpdateNews
        [HttpGet]
        [Route("UpdateNews")]
        public async Task<IHttpActionResult> UpdateNews(int id, string type, string value)
        {
            try
            {
                var newsObj = await db.News.Where(x => x.id == id).FirstOrDefaultAsync();
                if (newsObj != null)
                {
                    switch (type)
                    {
                        case "Delete":
                            newsObj.deleted = true;
                            break;
                        case "News":
                            newsObj.News = value;
                            break;
                    }
                    int returnVal = await db.SaveChangesAsync();
                    if (returnVal > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = "Done";
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = "Error";
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

        //Get: api/AddOffer
        [HttpPost]
        [Route("AddOffer")]
        public async Task<IHttpActionResult> AddOffer(OfferModel offerModel)
        {
            try
            {
                var offerObj = await db.Offer.Where(x => !x.deleted).FirstOrDefaultAsync();
                if (offerObj != null)
                {
                    offerObj.Offer = offerModel.Offer;
                }
                else
                {
                    offerModel.createdOn = DateTime.Now;
                    db.Offer.Add(offerModel);
                }
                int returnVal = await db.SaveChangesAsync();
                if (returnVal > 0)
                {
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


        [HttpGet]
        [Route("GetOffer")]
        public async Task<IHttpActionResult> GetOffer()
        {
            try
            {
                var offerObj = await db.Offer.AsNoTracking().Where(x => !x.deleted).Select(x => new
                {
                    x.id,
                    x.Offer,
                    x.createdOn,
                }).ToListAsync();
                if (offerObj.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = offerObj;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = offerObj;
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
        [Route("UploadFile")]
        public async Task<IHttpActionResult> UploadFile(int sportsId, string type, bool logo)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Invalid model state";
                    return Ok(responseDTO);
                }

                var httpRequest = HttpContext.Current.Request;

                if (httpRequest.Files == null || httpRequest.Files.Count == 0)
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Please attach the file";
                    return Ok(responseDTO);
                }

                var file = httpRequest.Files[0];

                if (!IsFileExtensionValid(file.FileName))
                {
                    responseDTO.Status = false;
                    responseDTO.Result = "Please upload a file of type: .jpg, .png";
                    return Ok(responseDTO);
                }

                var fileName = Path.GetFileName(file.FileName);
                var path = Path.Combine(HttpContext.Current.Server.MapPath(logo ? "/Content/logo/" : "/Content/banner/"), fileName);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                file.SaveAs(path);

                if (!logo)
                {
                    var banner = await db.BannerModel.FirstOrDefaultAsync(x => x.SportsId == sportsId && x.BannerType == type);

                    if (banner != null)
                    {
                        banner.FileName = fileName;
                        banner.FilePath = GetFilePath(path);
                    }
                    else
                    {
                        BannerModel bannerModel = new BannerModel()
                        {
                            SportsId = sportsId,
                            BannerType = type,
                            FileName = fileName,
                            FilePath = GetFilePath(path),
                            status = false,
                            deleted = false,
                            createdOn = DateTime.Now,
                        };
                        db.BannerModel.Add(bannerModel);
                    }
                }
                else
                {
                    var logoModel = await db.LogoModel.FirstOrDefaultAsync(x => x.Type == type);

                    if (logoModel != null)
                    {
                        logoModel.LogoPath = GetFilePath(path);
                    }
                    else
                    {
                        WebSiteLogoModel webSiteLogoModel = new WebSiteLogoModel()
                        {
                            Type = type,
                            LogoPath = GetFilePath(path),
                            status = false,
                            deleted = false,
                            createdOn = DateTime.Now,
                        };
                        db.LogoModel.Add(webSiteLogoModel);
                    }
                }

                await db.SaveChangesAsync();

                responseDTO.Status = true;
                responseDTO.Result = path;
            }
            catch (Exception ex)
            {
                responseDTO.Status = false;
                responseDTO.Result = ex.Message;
            }

            return Ok(responseDTO);
        }

        private bool IsFileExtensionValid(string fileName)
        {
            string[] allowedExtensions = { ".jpg", ".png" };
            string fileExtension = Path.GetExtension(fileName);
            return allowedExtensions.Contains(fileExtension);
        }

        private string GetFilePath(string savePath)
        {
            return savePath.Replace("C:\\", "https://");
        }


        [HttpGet]
        [Route("GetBanners")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetBanners(string type)
        {
            try
            {
                if (type == "All")
                {
                    var bannerObj = await db.BannerModel.AsNoTracking().Where(x => x.deleted == false).Select(x => new
                    {
                        x.FilePath,
                        x.BannerType
                    }).ToListAsync();
                    if (bannerObj.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = bannerObj;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = bannerObj;
                    }
                }
                else
                {
                    var bannerObj = await db.BannerModel.AsNoTracking().Where(x => x.BannerType.Equals(type)).Select(x => new
                    {
                        x.FilePath
                    }).ToListAsync();
                    if (bannerObj.Count > 0)
                    {
                        responseDTO.Status = true;
                        responseDTO.Result = bannerObj;
                    }
                    else
                    {
                        responseDTO.Status = false;
                        responseDTO.Result = bannerObj;
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
        [Route("GetLogo")]
        [AllowAnonymous]
        public async Task<IHttpActionResult> GetLogo()
        {
            try
            {
                var logo = await db.LogoModel.AsNoTracking().Where(x => x.deleted == false).Select(x => new
                {
                    x.Type,
                    x.LogoPath,
                }).ToListAsync();
                if (logo.Count > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = logo;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = logo;
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
        [Route("Update_TakeRecord")]
        public async Task<IHttpActionResult> UpdateRecVal(int value)
        {
            try
            {
                int userId = Convert.ToInt32(run_time.RunTimeUserId());
                var take_Rec = await db.TakeRecord.Where(x => x.UserId == userId).FirstOrDefaultAsync();
                if (take_Rec != null)
                {
                    take_Rec.Records = value;
                }
                else
                {
                    TakeRecord takeRec = new TakeRecord()
                    {
                        UserId = userId,
                        Records = value,
                    };
                    db.TakeRecord.Add(takeRec);
                }
                int retrunVal = await db.SaveChangesAsync();
                if (retrunVal > 0)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = "Done";
                }
                else
                {
                    responseDTO.Status = false; ;
                    responseDTO.Result = "Error";
                }
            }
            catch (Exception ex)
            {
                responseDTO.Status = false; ;
                responseDTO.Result = ex.Message;
            }
            return Ok(responseDTO);
        }

        [HttpGet]
        [Route("GetRecValue")]
        public async Task<IHttpActionResult> GetRecValue()
        {
            try
            {
                int userId = Convert.ToInt32(run_time.RunTimeUserId());
                var take_Rec = await db.TakeRecord.Where(x => x.UserId == userId).FirstOrDefaultAsync();
                if (take_Rec != null)
                {
                    responseDTO.Status = true;
                    responseDTO.Result = take_Rec.Records;
                }
                else
                {
                    responseDTO.Status = false;
                    responseDTO.Result = 10;
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