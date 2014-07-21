﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Xml.Schema;
using ImportProducts.Properties;
using Ionic.Zip;
using NGeo.GeoNames;
using SelectedHotelsModel;

namespace ImportProducts
{
    class ImportLaterooms
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static IEnumerable<XElement> StreamRootChildDoc(string uri)
        {
            using (XmlReader reader = XmlReader.Create(uri))
            {
                reader.MoveToContent();
                // Parse the file and display each of the nodes.
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "hotel")
                            {
                                XElement el = XElement.ReadFrom(reader) as XElement;
                                if (el != null)
                                    yield return el;
                            }
                            break;
                    }
                }
            }
        }


        public static void SaveFileFromURL(string url, string destinationFileName, int timeoutInSeconds, BackgroundWorker bw, DoWorkEventArgs e)
        {

            // Create a web request to the URL
            HttpWebRequest MyRequest = (HttpWebRequest)WebRequest.Create(url);
            MyRequest.Timeout = timeoutInSeconds * 1000;
            try
            {
                // Get the web response
                HttpWebResponse MyResponse = (HttpWebResponse)MyRequest.GetResponse();

                // Make sure the response is valid
                if (HttpStatusCode.OK == MyResponse.StatusCode)
                {
                    // Open the response stream
                    using (Stream MyResponseStream = MyResponse.GetResponseStream())
                    {
                        // Set step for backgroundWorker
                        Form1.activeStep = "Load file..";
                        bw.ReportProgress(0);           // start new step of background process

                        // Open the destination file
                        using (
                            FileStream MyFileStream = new FileStream(destinationFileName, FileMode.OpenOrCreate,
                                                                     FileAccess.Write))
                        {
                            // Get size of stream - it is impossible in advance at all
                            // so we just can set approximate value if know it or get it before by experience
                            long countBuffer = 30000;
                            long currentBuffer = 0;
                            // Create a 4K buffer to chunk the file
                            byte[] MyBuffer = new byte[4096];
                            int BytesRead;
                            // Read the chunk of the web response into the buffer
                            while (0 < (BytesRead = MyResponseStream.Read(MyBuffer, 0, MyBuffer.Length)))
                            {
                                // Write the chunk from the buffer to the file
                                MyFileStream.Write(MyBuffer, 0, BytesRead);
                                // show progress & catch Cancel
                                currentBuffer++;
                                if (bw.CancellationPending)
                                {
                                    // it is neccessary explicit closing Stream else operation in background will be cancelled after total download file
                                    MyRequest.Abort();
                                    // cancel background work
                                    e.Cancel = true;
                                    break;
                                }
                                else if (bw.WorkerReportsProgress && currentBuffer % 100 == 0) bw.ReportProgress((int)(100 * currentBuffer / countBuffer));
                            }
                            // visualization finish process
                            if (!e.Cancel && currentBuffer < countBuffer)
                            {
                                bw.ReportProgress(100);
                                Thread.Sleep(100);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                e.Result = "ERROR:" + err.Message;
                log.Error("Error error logging", err);
            }
        }

        public static void DoImport(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters) e.Argument;
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
                // unzip file to temp folder if needed
                if (_URL.EndsWith(".zip"))
                {
                    string zipFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath,
                                                       "Hotels_Standard.zip");
                    // inside this function show progressBar for step: LoadFile
                    if (File.Exists(zipFileName))
                    {
                        File.Delete(zipFileName);
                    }
                    SaveFileFromURL(_URL, zipFileName, 60, bw, e);

                    // if user cancel during saving file or ERROR
                    if (e.Cancel || (e.Result != null) && e.Result.ToString().Substring(0, 6).Equals("ERROR:")) return;
                    // Set step for backgroundWorker
                    Form1.activeStep = "Extract file..";
                    bw.ReportProgress(0); // start new step of background process

                    using (ZipFile zip1 = ZipFile.Read(zipFileName))
                    {

                        foreach (ZipEntry zipEntry in zip1)
                        {
                            zipEntry.Extract(Properties.Settings.Default.TempPath,
                                             ExtractExistingFileAction.OverwriteSilently);


                        }
                    }
                    _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "Hotels_Standard.xml");
                }
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add("", "Hotels_Standard.xsd");
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
                from el in StreamRootChildDoc(_URL)
                select new ProductView
                           {
                               Country = (string) el.Element("hotel_country"),
                               County = (string) el.Element("hotel_county"),
                               City = (string) el.Element("hotel_city"),
                               ProductNumber = (string) el.Element("hotel_ref"),
                               Name = (string) el.Element("hotel_name"),
                               Images = el.Element("images"),
                               UnitCost = (string) el.Element("PricesFrom"),
                               Description = (string) el.Element("hotel_description"),
                               DescriptionHTML = (string) el.Element("alternate_description"),
                               URL = (string) el.Element("hotel_link"),
                               Star = (string) el.Element("hotel_star"),
                               CustomerRating = (string) el.Element("customerrating"),
                               Rooms = (string)el.Element("hotel_total_rooms"),
                               Address = (string)el.Element("hotel_address"),
                               PostCode = (string)el.Element("hotel_pcode"),
                               CurrencyCode = (string)el.Element("CurrencyCode"),
                               Lat = (string)el.Element("geo_code").Element("lat"),
                               Long = (string)el.Element("geo_code").Element("long")
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
                    foreach (ProductView product in xmlProducts)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

#if DEBUG
                        Console.WriteLine(i + " - " + product.Name); // debug print
#endif
                        if (product.County == "Greater London (county)")
                        {
                            product.County = "Greater-London";
                        }
                        if (product.County == "Greater Manchester (county)")
                        {
                            product.County = "Greater-Manchester";
                        }

                        // create new product record
                        Hotel hotel =
                            db.Products.SingleOrDefault(
                                p => p.ProductTypeId == (int)Enums.ProductTypeEnum.Hotels && p.Categories.Any(c => c.Id == categoryId) && p.Name == product.Name && p.Number == product.ProductNumber) as Hotel;
                        if (hotel == null)
                        {
                            hotel = new Hotel();
                            hotel.ProductTypeId = (int) Enums.ProductTypeEnum.Hotels;
                            hotel.Name = product.Name;
                            hotel.Number = product.ProductNumber;
                            if (!String.IsNullOrEmpty(product.UnitCost))
                            {
                                hotel.UnitCost = Convert.ToDecimal(product.UnitCost);
                            }
                            hotel.Description = product.Description;
                            hotel.URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            hotel.Image = (string)product.Images.Element("url");
                            int star = 0;
                            string strStar = new string(product.Star.TakeWhile(char.IsDigit).ToArray());
                            if (strStar.Length > 0)
                            {
                                star = int.Parse(strStar);
                                hotel.Star = star;
                            }
                            if (!String.IsNullOrEmpty(product.CustomerRating))
                            {
                                hotel.CustomerRating = int.Parse(product.CustomerRating);
                            }
                            if (!String.IsNullOrEmpty(product.Rooms))
                            {
                                hotel.Rooms = int.Parse(product.Rooms);
                            }
                            if (!String.IsNullOrEmpty(product.Address))
                            {
                                hotel.Address = product.Address;
                            }
                            if (!String.IsNullOrEmpty(product.PostCode))
                            {
                                hotel.PostCode = product.PostCode;
                            }
                            if (!String.IsNullOrEmpty(product.CurrencyCode))
                            {
                                hotel.CurrencyCode = product.CurrencyCode;
                            }
                            if (!String.IsNullOrEmpty(product.Lat))
                            {
                                hotel.Lat = Convert.ToDouble(product.Lat);
                            }
                            if (!String.IsNullOrEmpty(product.Long))
                            {
                                hotel.Lon = Convert.ToDouble(product.Long);
                            }
                            hotel.CreatedByUser = vendorId;
                            hotel.CreatedDate = DateTime.Now;
                            hotel.IsDeleted = false;
                            hotel.HotelTypeId = (int) Enums.HotelTypeEnum.Hotels;
                            hotel.FeedId = feedId;
                            db.Products.Add(hotel);
                            db.SaveChanges();

                            // GeoNames
                            var geoNames = db.GeoNames.Where(gn => gn.Name.ToLower() == product.City.ToLower())
                                .OrderByDescending(gn => gn.Population)
                                .ThenByDescending(gn => gn.ModificationDate);
                            if (geoNames != null)
                            {
                                var geoName = geoNames.FirstOrDefault();
                                if (geoName != null)
                                {
                                    hotel.GeoNameId = geoName.Id;
                                }
                            }
                            else
                            {
                                if (hotel.Lat.HasValue && hotel.Lon.HasValue)
                                {
                                    using (var geoNamesClient = new GeoNamesClient())
                                    {
                                        var finder = new NearbyPlaceNameFinder
                                        {
                                            Latitude = hotel.Lat.Value,
                                            Longitude = hotel.Lon.Value,
                                            UserName = Settings.Default.GeoNamesUserName
                                        };
                                        var results = geoNamesClient.FindNearbyPlaceName(finder);
                                        if (results != null && results.Count > 0)
                                        {
                                            var toponym = results.First();
                                            hotel.GeoNameId = toponym.GeoNameId;
                                        }
                                    }
                                }
                            }

                            Category category = db.Categories.Find(categoryId);
                            if (category != null)
                            {
                                hotel.Categories.Add(category);
                            }
                            db.SaveChanges();

                            var imageURLs = product.Images.Elements("url").Where(xe => xe.Value.EndsWith(".jpg") || xe.Value.EndsWith(".png")).Select(xe => xe.Value);
                            IEnumerable<string> imageURLList = imageURLs as IList<string> ?? imageURLs.ToList();
                            foreach (var imageURL in imageURLList)
                            {
                                ProductImage productImage = new ProductImage
                                {
                                    URL = imageURL
                                };
                                hotel.ProductImages.Add(productImage);
                            }
                            db.SaveChanges();

                            i++;
                            //Common.UpdateSteps(stepImport: i);
                        }
                        else
                        {
                            // GeoNames
                            if (!hotel.GeoNameId.HasValue)
                            {
                                var geoNames = db.GeoNames.Where(gn => gn.Name.ToLower() == product.City.ToLower())
                                    .OrderByDescending(gn => gn.Population)
                                    .ThenByDescending(gn => gn.ModificationDate);
                                if (geoNames != null && geoNames.Any())
                                {
#if DEBUG
                                    continue;
#endif
                                    var geoName = geoNames.FirstOrDefault();
                                    if (geoName != null)
                                    {
                                        hotel.GeoNameId = geoName.Id;
                                    }
                                }
                                else
                                {
                                    log.Info(String.Format("City {0} is not found for hotel {1}.", product.City,
                                        hotel.Id));
                                    if (hotel.Lat.HasValue && hotel.Lon.HasValue)
                                    {
                                        using (var geoNamesClient = new GeoNamesClient())
                                        {
                                            var finder = new NearbyPlaceNameFinder
                                            {
                                                Latitude = hotel.Lat.Value,
                                                Longitude = hotel.Lon.Value,
                                                UserName = Settings.Default.GeoNamesUserName
                                            };
                                            var results = geoNamesClient.FindNearbyPlaceName(finder);
                                            if (results != null && results.Count > 0)
                                            {
                                                var toponym = results.First();
                                                log.Info(String.Format("Using {0} instead of city {1}", toponym.Name,
                                                    product.City));
                                                hotel.GeoNameId = toponym.GeoNameId;
                                            }
                                        }
                                    }
                                }
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
                            if (hotel.URL != product.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                            {
                                hotel.URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            }
                            if (hotel.Image != (string)product.Images.Element("url"))
                            {
                                hotel.Image = (string)product.Images.Element("url");
                            }
                            int? star = null;
                            string strStar = new string(product.Star.TakeWhile(char.IsDigit).ToArray());
                            if (strStar.Length > 0)
                            {
                                star = int.Parse(strStar);
                            }
                            if (hotel.Star != star)
                            {
                                hotel.Star = star;
                            }
                            int? customerRating = null;
                            if (!String.IsNullOrEmpty(product.CustomerRating))
                            {
                                customerRating = int.Parse(product.CustomerRating);
                            }
                            if (hotel.CustomerRating != customerRating)
                            {
                                hotel.CustomerRating = customerRating;
                            }
                            int? rooms = null;
                            if (!String.IsNullOrEmpty(product.Rooms))
                            {
                                rooms = int.Parse(product.Rooms);
                            }
                            if (hotel.Rooms != rooms)
                            {
                                hotel.Rooms = rooms;
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
                            double? lat = null;
                            if (!String.IsNullOrEmpty(product.Lat))
                            {
                                lat = Convert.ToDouble(product.Lat);
                            }
                            if (hotel.Lat != lat)
                            {
                                hotel.Lat = lat;
                            }
                            double? lon = null;
                            if (!String.IsNullOrEmpty(product.Long))
                            {
                                lon = Convert.ToDouble(product.Long);
                            }
                            if (hotel.Lon != lon)
                            {
                                hotel.Lon = lon;
                            }
                            db.SaveChanges();

                            var imageURLs = product.Images.Elements("url").Where(xe => xe.Value.EndsWith(".jpg") || xe.Value.EndsWith(".png")).Select(xe => xe.Value);
                            IEnumerable<string> imageURLList = imageURLs as IList<string> ?? imageURLs.ToList();
                            foreach (var imageURL in imageURLList)
                            {
                                ProductImage productImage =
                                    hotel.ProductImages.SingleOrDefault(pi => pi.URL == imageURL);
                                if (productImage == null)
                                {
                                    productImage = new ProductImage { URL = imageURL };
                                    hotel.ProductImages.Add(productImage);
                                }
                            }
                            db.SaveChanges();

                            var productImagesToRemove = db.ProductImages.Where(pi => pi.ProductId == hotel.Id &&
                                imageURLList.All(xe => xe != pi.URL));
                            if (productImagesToRemove.Any())
                            {
                                db.ProductImages.RemoveRange(productImagesToRemove);
                            }
                            db.SaveChanges();

                            i++;
                            //Common.UpdateSteps(stepImport: i);
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

        private class ProductView
        {
            public string Country { get; set; }
            public string County { get; set; }
            public string City { get; set; }
            public string ProductNumber { get; set; }
            public string Name { get; set; }
            public XElement Images { get; set; }
            public string UnitCost { get; set; }
            public string Description { get; set; }
            public string DescriptionHTML { get; set; }
            public string URL { get; set; }
            public string Star { get; set; }
            public string CustomerRating { get; set; }
            public string Rooms { get; set; }
            public string Address { get; set; }
            public string PostCode { get; set; }
            public string CurrencyCode { get; set; }
            public string Lat { get; set; }
            public string Long { get; set; }
        }
    }
}
