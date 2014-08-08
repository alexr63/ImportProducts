using System;
using System.ComponentModel;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.Schema;
using SelectedHotelsModel;

namespace ImportProducts
{
    class ImportTradeDoublerHotels
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void DoImport(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters)e.Argument;
            string _URL = param.Url;
            int categoryId = param.CategoryId;
            int portalId = param.PortalId;
            int vendorId = param.VendorId;
            string countryFilter = param.CountryFilter;
            string cityFilter = param.CityFilter;
            int? stepImport = param.StepImport;
            int feedId = param.FeedId;

            if (!File.Exists(_URL))
            {
                string xmlFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
                if (File.Exists(xmlFileName))
                {
                    File.Delete(xmlFileName);
                }
                // inside function display progressbar
                Common.SaveFileFromURL(_URL, xmlFileName, 60, bw, e, log);

                // exit if user cancel during saving file or error
                if (e.Cancel || (e.Result != null) && e.Result.ToString().Substring(0, 6).Equals("ERROR:")) return;

                _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add("", "tradedoublerHotels.xsd");
            Form1.activeStep = "Validating input..";
            bw.ReportProgress(0); // start new step of background process
            XDocument xDoc = XDocument.Load(_URL);
            bool errors = false;
            xDoc.Validate(schemas, (o, e2) =>
            {
                e.Result = "ERROR:" + e2.Message;
                log.Error(e2.Message);
                errors = true;
            });
            if (errors)
            {
                e.Cancel = true;
                return;
            }

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(50);

            // read hotels from XML
            // use XmlReader to avoid huge file size dependence
            var xmlProducts =
                from el in Common.StreamRootChildDoc(_URL, "product")
                where (string)el.Element("TDCategoryName") == "Hotels"
                select new ProductView
                {
                    Country = (string)el.Element("fields").Element("country"),
                    City = (string)el.Element("fields").Element("city"),
                    ProductNumber = (string)el.Element("TDProductId"),
                    Name = (string)el.Element("name"),
                    Image = (string)el.Element("imageUrl"),
                    UnitCost = (string)el.Element("price"),
                    Description = (string)el.Element("description"),
                    DescriptionHTML = (string)el.Element("description"),
                    URL = (string)el.Element("productUrl"),
                    Star = (string)el.Element("fields").Element("StarRating"),
                    CustomerRating = (string)el.Element("fields").Element("AverageOverallRating"),
                    Address = (string)el.Element("fields").Element("address"),
                    PostCode = (string)el.Element("fields").Element("postalcode"),
                    Telephone = (string)el.Element("fields").Element("telephone"),
                    CurrencyCode = (string)el.Element("currency")
                };

            if (!String.IsNullOrEmpty(countryFilter))
            {
                xmlProducts = xmlProducts.Where(p => p.Country == countryFilter);
            }

            if (!String.IsNullOrEmpty(cityFilter))
            {
                xmlProducts = xmlProducts.Where(p => p.City == cityFilter);
            }

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(100);
            Thread.Sleep(100); // a little bit slow working for visualisation Progress

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0); // start new step of background process
            int productCount = xmlProducts.Count();
            try
            {
                int initialStep = 0;
                if (stepImport.HasValue)
                {
                    initialStep = stepImport.Value;
                }

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    int i = 0;
                    foreach (var product in xmlProducts)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

                        string productName = product.Name.Replace("&apos;", "'");
#if DEBUG
                        Console.WriteLine(i + " - " + productName); // debug print
#endif

                        Hotel hotel =
                            db.Products.OfType<Hotel>().SingleOrDefault(
                                p => p.Name == productName && p.Number == product.ProductNumber);
                        if (hotel == null)
                        {
                            hotel = new Hotel
                            {
                                Name = productName,
                                ProductTypeId = (int)Enums.ProductTypeEnum.Hotels,
                                Number = product.ProductNumber,
                                Description = product.Description,
                                URL = product.URL,
                                Image = product.Image,
                                CreatedByUser = vendorId,
                                CreatedDate = DateTime.Now,
                                IsDeleted = false,
                                HotelTypeId = (int)Enums.HotelTypeEnum.Hotels
                            };

                            if (!String.IsNullOrEmpty(product.UnitCost))
                            {
                                hotel.UnitCost = Convert.ToDecimal(product.UnitCost);
                            }
                            if (!String.IsNullOrEmpty(product.Star))
                            {
                                hotel.Star = decimal.Parse(product.Star);
                            }
                            if (!String.IsNullOrEmpty(product.CustomerRating))
                            {
                                hotel.CustomerRating = decimal.Parse(product.CustomerRating);
                            }
                            if (!String.IsNullOrEmpty(product.Address))
                            {
                                hotel.Address = product.Address;
                            }
                            if (!String.IsNullOrEmpty(product.CurrencyCode))
                            {
                                hotel.CurrencyCode = product.CurrencyCode;
                            }
                            if (!String.IsNullOrEmpty(product.PostCode))
                            {
                                hotel.PostCode = product.PostCode;
                            }
                            hotel.FeedId = feedId;
                            db.Products.Add(hotel);
                            db.SaveChanges();

                            Common.SetLocation(product, db, hotel);
                            Common.SetGeoNameId(product, db, hotel);

                            Category category = db.Categories.Find(categoryId);
                            if (category != null)
                            {
                                hotel.Categories.Add(category);
                            }
                            db.SaveChanges();

                            i++;
                            Common.UpdateSteps(stepImport: i);
                        }
                        else
                        {
                            if (hotel.Location == null)
                            {
                                Common.SetLocation(product, db, hotel);
                            }
                            if (!hotel.GeoNameId.HasValue)
                            {
                                Common.SetGeoNameId(product, db, hotel);
                            }

                            // no need to check for null vallue because of previous if
                            decimal? unitCost = null;
                            if (!String.IsNullOrEmpty(product.UnitCost))
                            {
                                unitCost = decimal.Parse(product.UnitCost);
                            }
                            if (hotel.UnitCost != unitCost)
                            {
                                hotel.UnitCost = unitCost;
                            }
                            if (hotel.Description != product.Description)
                            {
                                hotel.Description = product.Description;
                            }
                            if (hotel.URL != product.URL)
                            {
                                hotel.URL = product.URL;
                            }
                            if (hotel.Image != product.Image)
                            {
                                hotel.Image = product.Image;
                            }
                            decimal? star = null;
                            if (!String.IsNullOrEmpty(product.Star))
                            {
                                star = decimal.Parse(product.Star);
                            }
                            if (hotel.Star != star)
                            {
                                hotel.Star = star;
                            }
                            decimal? customerRating = null;
                            if (!String.IsNullOrEmpty(product.CustomerRating))
                            {
                                customerRating = decimal.Parse(product.CustomerRating);
                            }
                            if (hotel.CustomerRating != customerRating)
                            {
                                hotel.CustomerRating = customerRating;
                            }
                            if (hotel.Address != product.Address)
                            {
                                hotel.Address = product.Address;
                            }
                            if (hotel.PostCode != product.PostCode)
                            {
                                hotel.PostCode = product.PostCode;
                            }
                            if (hotel.CurrencyCode != product.CurrencyCode)
                            {
                                hotel.CurrencyCode = product.CurrencyCode;
                            }
                            db.SaveChanges();

                            i++;
                            Common.UpdateSteps(stepImport: i);
                        }

                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            goto Cancelled;
                        }
                        else if (bw.WorkerReportsProgress && i % 100 == 0)
                        {
                            bw.ReportProgress((int) (100*i/productCount));
                        }
                    }
                }

                if (!e.Cancel)
                {
                    Common.UpdateSteps();
                }
            Cancelled:
                ;
            }
            catch (DbEntityValidationException exception)
            {
                foreach (var eve in exception.EntityValidationErrors)
                {
                    log.Error(String.Format("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State), exception);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        log.Error(String.Format("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage), exception);
                    }
                }
                throw;
            }
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                log.Error("Error error logging", ex);
            }
        }
    }
}
