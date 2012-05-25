using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using Ionic.Zip;

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
            HttpWebRequest MyRequest = (HttpWebRequest) WebRequest.Create(url);
            MyRequest.Timeout = timeoutInSeconds*1000;
            try
            {
                // Get the web response
                HttpWebResponse MyResponse = (HttpWebResponse) MyRequest.GetResponse();

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
                                Thread.Sleep(1000);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                e.Result = "ERROR:" + err.Message;
                //throw new Exception("Error saving file from URL:" + err.Message, err);
            }
        }

        public static void DoImport(object sender, DoWorkEventArgs e)               // static bool    (string _URL, out string message)
        {
            //bool rc = false;
            //message = String.Empty;
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters)e.Argument;
            string _URL = param.Url;
            int categoryId = param.CategoryId;
            int portalId = param.PortalId;
            int vendorId = param.VendorId;
            string advancedCategoryRoot = param.AdvancedCategoryRoot;
            string filter = param.Filter;

#if DEBUG
            _URL = @"C:\Temp\Hotels_Standard.xml";
#else
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
                bw.ReportProgress(0);           // start new step of background process

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
#endif

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true; return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(50);

            // read hotels from XML
            // use XmlReader to avoid huge file size dependence
            var products =
                from el in StreamRootChildDoc(_URL)
                select new
                {
                    Country = (string)el.Element("hotel_country"),
                    County = (string)el.Element("hotel_county"),
                    City = (string)el.Element("hotel_city"),
                    ProductNumber = (string)el.Element("hotel_ref"),
                    Name = (string)el.Element("hotel_name"),
                    Images = el.Element("images"),
                    UnitCost = (decimal)el.Element("PricesFrom"),
                    Description = (string)el.Element("hotel_description"),
                    DescriptionHTML = (string)el.Element("alternate_description"),
                    URL = (string)el.Element("hotel_link")
                };

            if (!String.IsNullOrEmpty(filter))
            {
                products = products.Where(p => p.City == filter || p.County == filter || p.Country == filter);
            }

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true; return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(100);
            Thread.Sleep(1000); // a little bit slow working for visualisation Progress

           // Set step for backgroundWorker
           Form1.activeStep = "Import records..";
           bw.ReportProgress(0);           // start new step of background process
           long countProducts = products.Count();
           long currentProduct = 0;

            try
            {
                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name); // debug print

                        // create advanced categories
                        var product1 = product;
                        string hotelCity = product1.City;
                        if (product1.City.Length > 50)
                        {
                            hotelCity = product1.City.Substring(0, 47).PadRight(50, '.');
                        }
                        int? parentID = null;
                        int? catRootID = null;
                        int? catCountryID = null;
                        int? catCountyID = null;
                        int? catCityID = null;
                        int level = 0;
                        int maxOrder = 0;

                        if (!String.IsNullOrEmpty(advancedCategoryRoot))
                        {
                            var advCatRoot =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == advancedCategoryRoot && ac.Level == level);
                            if (advCatRoot == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatRoot = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = advancedCategoryRoot,
                                    IsVisible = true,
                                    DisableLink = false,
                                    Url = String.Empty,
                                    Title = String.Empty,
                                    Description = String.Empty,
                                    KeyWords = String.Empty,
                                    IsDeleted = false,
                                    IconFile = String.Empty,
                                    Level = level,
                                    AdvCatImportID = String.Empty
                                };
                                db.AdvCats.Add(advCatRoot);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatRoot.AdvCatID);
                            }
                            parentID = advCatRoot.AdvCatID;
                            catRootID = advCatRoot.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product1.Country))
                        {
                            AdvCat advCatCountry;
                            if (parentID.HasValue)
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product1.Country &&
                                        ac.Level == level && ac.ParentId == parentID.Value);
                            }
                            else
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product1.Country &&
                                        ac.Level == level);
                            }
                            if (advCatCountry == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCountry = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = product.Country,
                                    IsVisible = true,
                                    DisableLink = false,
                                    Url = String.Empty,
                                    Title = String.Empty,
                                    Description = String.Empty,
                                    KeyWords = String.Empty,
                                    IsDeleted = false,
                                    IconFile = String.Empty,
                                    Level = level,
                                    AdvCatImportID = String.Empty
                                };
                                if (parentID.HasValue)
                                {
                                    advCatCountry.ParentId = parentID.Value;
                                }
                                db.AdvCats.Add(advCatCountry);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCountry.AdvCatID);
                            }
                            parentID = advCatCountry.AdvCatID;
                            catCountryID = advCatCountry.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product1.County))
                        {
                            var advCatCounty =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == product1.County && ac.Level == level);
                            if (advCatCounty == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCounty = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = product.County,
                                    IsVisible = true,
                                    ParentId = parentID.Value,
                                    DisableLink = false,
                                    Url = String.Empty,
                                    Title = String.Empty,
                                    Description = String.Empty,
                                    KeyWords = String.Empty,
                                    IsDeleted = false,
                                    IconFile = String.Empty,
                                    Level = level,
                                    AdvCatImportID = String.Empty
                                };
                                db.AdvCats.Add(advCatCounty);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCounty.AdvCatID);
                            }
                            parentID = advCatCounty.AdvCatID;
                            catCountyID = advCatCounty.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product1.City))
                        {
                            var advCatCity =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == product1.City && ac.Level == level);
                            if (advCatCity == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCity = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = hotelCity,
                                    IsVisible = true,
                                    ParentId = parentID.Value,
                                    DisableLink = false,
                                    Url = String.Empty,
                                    Title = String.Empty,
                                    Description = String.Empty,
                                    KeyWords = String.Empty,
                                    IsDeleted = false,
                                    IconFile = String.Empty,
                                    Level = level,
                                    AdvCatImportID = String.Empty
                                };
                                db.AdvCats.Add(advCatCity);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCity.AdvCatID);
                            }
                            parentID = advCatCity.AdvCatID;
                            catCityID = advCatCity.AdvCatID;
                            level++;
                        }

                        // create new product record
                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber);
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                CategoryID = categoryId,
                                Category2ID = 0,
                                Category3 = String.Empty,
                                ProductName = product.Name,
                                ProductNumber = product.ProductNumber,
                                UnitCost = product.UnitCost,
                                UnitCost2 = product.UnitCost,
                                UnitCost3 = product.UnitCost,
                                UnitCost4 = product.UnitCost,
                                UnitCost5 = product.UnitCost,
                                UnitCost6 = product.UnitCost,
                                Description = product.Description,
                                DescriptionHTML = product.DescriptionHTML,
                                URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' '),
                                ProductCost = product.UnitCost,
                                CreatedByUser = vendorId,
                                DateCreated = DateTime.Now
                            };

                            // add additional product images
                            product2.ProductImage = (string)product.Images.Element("url");
                            foreach (var image in product.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    ProductImage productImage = new ProductImage();
                                    productImage.ImageFile = image.Value;
                                    product2.ProductImages.Add(productImage);
                                }
                            }
                            // trick to hide Add To Cart button
                            product2.OrderQuant = "0";

                            // add product to product set
                            db.Products.Add(product2);
                            // store  changes
                            db.SaveChanges();
                        }
                        else
                        {
                            product2.CategoryID = categoryId;
                            product2.Category2ID = 0;
                            product2.ProductName = product.Name;
                            product2.ProductNumber = product.ProductNumber;
                            product2.UnitCost = product.UnitCost;
                            product2.UnitCost2 = product.UnitCost;
                            product2.UnitCost3 = product.UnitCost;
                            product2.UnitCost4 = product.UnitCost;
                            product2.UnitCost5 = product.UnitCost;
                            product2.UnitCost6 = product.UnitCost;
                            product2.Description = product.Description;
                            product2.DescriptionHTML = product.DescriptionHTML;
                            product2.URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            product2.ProductCost = product.UnitCost;

                            product2.ProductImage = (string)product.Images.Element("url");
                            foreach (var productImage in product2.ProductImages)
                            {
                                if (!product1.Images.Elements("url").Any(x => x.Value == productImage.ImageFile))
                                {
                                    productImage.ImageFile = String.Empty;
                                }
                            }
                            var oldImages = product2.ProductImages.Where(pi => pi.ImageFile == String.Empty).ToList();
                            foreach (var oldImage in oldImages)
                            {
                                db.ProductImages.Remove(oldImage);
                            }
                            foreach (var image in product1.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    if (!product2.ProductImages.Any(pi => pi.ImageFile == image.Value))
                                    {
                                        ProductImage productImage = new ProductImage();
                                        productImage.ImageFile = image.Value;
                                        product2.ProductImages.Add(productImage);
                                    }
                                }
                            }

                            product2.OrderQuant = "0";

                            db.SaveChanges();
                        }

                        // add product to advanced categories
                        if (catRootID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catRootID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catRootID.Value,
                                ProductID = product2.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCountryID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCountryID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCountryID.Value,
                                ProductID = product2.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCountyID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCountyID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCountyID.Value,
                                ProductID = product2.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCityID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCityID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCityID.Value,
                                ProductID = product2.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }

                        currentProduct++;
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        else if (bw.WorkerReportsProgress && currentProduct % 100 == 0) bw.ReportProgress((int)(100 * currentProduct / countProducts));
                    }
                    //    rc = true;
                }
            }
            catch (Exception ex)
            {
                e.Result =  "ERROR:" + ex.Message;
                //message = ex.Message;
                log.Error("Error error logging", ex);
            }
            //return rc;
        }
    }
}
