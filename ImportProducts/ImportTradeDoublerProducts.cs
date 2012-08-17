﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace ImportProducts
{
    class ImportTradeDoublerProducts
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
                            if (reader.Name == "product")
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
                            long countBuffer = 1000000;
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
                                    // cancel background work
                                    e.Cancel = true;                                    
                                    // Due to too huge size of download it is neccessary explicit closing Stream else operation in background will be cancelled after total download file
                                    MyRequest.Abort();
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
                //throw new Exception("Error saving file from URL:" + err.Message, err);
            }
        }

        public static void DoImport(object sender, DoWorkEventArgs e)               //(string _URL, out string message)        // public static bool
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
            string countryFilter = param.CountryFilter;
            string cityFilter = param.CityFilter;

            if (!File.Exists(_URL))
            {
                string xmlFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
                if (File.Exists(xmlFileName))
                {
                    File.Delete(xmlFileName);
                }
                // inside function display progressbar
                SaveFileFromURL(_URL, xmlFileName, 60, bw, e);

                // exit if user cancel during saving file or error
                if (e.Cancel || (e.Result != null) && e.Result.ToString().Substring(0, 6).Equals("ERROR:")) return;

                _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add("", "tradedoubler.xsd");
            Form1.activeStep = "Validating inpout..";
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

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0);           // start new step of background process

            var products =
                from el in StreamRootChildDoc(_URL)
                select new
                {
                    Category = (string)el.Element("TDCategoryName"),
                    ProductNumber = (string)el.Element("TDProductId"),
                    Name = (string)el.Element("name"),
                    Image = (string)el.Element("imageUrl"),
                    UnitCost = (decimal)el.Element("price"),
                    Description = (string)el.Element("description"),
                    DescriptionHTML = (string)el.Element("description"),
                    URL = (string)el.Element("productUrl"),
                    Country = (string)el.Element("fields").Element("country"),
                    City = (string)el.Element("fields").Element("city")
                };

            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                RegionInfo ri = null;
                try
                {
                    ri = new RegionInfo(ci.Name);
                }
                catch
                {
                    // If a RegionInfo object could not be created we don't want to use the CultureInfo
                    // for the country list.
                    continue;
                }
                if (ri.EnglishName == countryFilter)
                {
                    countryFilter = ri.TwoLetterISORegionName;
                    break;
                }
            }
            if (!String.IsNullOrEmpty(countryFilter))
            {
                products = products.Where(p => p.Country == countryFilter);
            }

            if (!String.IsNullOrEmpty(cityFilter))
            {
                products = products.Where(p => p.City == cityFilter);
            }

            long countProducts = products.Count();
            long currentProduct = 0;

            try
            {
                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name.Replace("&apos;", "'")); // debug print

                        int? parentID = null;
                        int? catRootID = null;
                        int? catCountryID = null;
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

                        var regionInfo = new RegionInfo(product.Country);
                        var countryName = regionInfo.EnglishName;
                        if (!String.IsNullOrEmpty(countryName))
                        {
                            AdvCat advCatCountry;
                            if (parentID.HasValue)
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == countryName &&
                                        ac.Level == level && ac.ParentId == parentID.Value);
                            }
                            else
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == countryName &&
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
                                    AdvCatName = countryName,
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

                        if (!String.IsNullOrEmpty(product.City))
                        {
                            AdvCat advCatCity;
                            if (parentID.HasValue)
                            {
                                advCatCity =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product.City &&
                                        ac.Level == level && ac.ParentId == parentID.Value);
                            }
                            else
                            {
                                advCatCity =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product.City &&
                                        ac.Level == level);
                            }
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
                                    AdvCatName = product.City,
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
                                    advCatCity.ParentId = parentID.Value;
                                }
                                db.AdvCats.Add(advCatCity);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCity.AdvCatID);
                            }
                            parentID = advCatCity.AdvCatID;
                            catCityID = advCatCity.AdvCatID;
                            level++;
                        }

                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber && p.CreatedByUser == vendorId);
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                CategoryID = categoryId,
                                Category2ID = 0,
                                Category3 = String.Empty,
                                ProductName = product.Name.Replace("&apos;", "'"),
                                ProductNumber = product.ProductNumber,
                                UnitCost = product.UnitCost,
                                UnitCost2 = product.UnitCost,
                                UnitCost3 = product.UnitCost,
                                UnitCost4 = product.UnitCost,
                                UnitCost5 = product.UnitCost,
                                UnitCost6 = product.UnitCost,
                                Description = product.Description,
                                DescriptionHTML = product.DescriptionHTML,
                                URL = product.URL,
                                ProductCost = product.UnitCost,
                                ProductImage = product.Image,
                                CreatedByUser = vendorId,
                                DateCreated = DateTime.Now
                            };

                            product2.ProductImage = product.Image;
                            product2.OrderQuant = "0";

                            db.Products.Add(product2);
                            db.SaveChanges();
                        }
                        else
                        {
                            product2.CategoryID = categoryId;
                            product2.Category2ID = 0;
                            product2.ProductName = product.Name.Replace("&apos;", "'");
                            product2.ProductNumber = product.ProductNumber;
                            product2.UnitCost = product.UnitCost;
                            product2.UnitCost2 = product.UnitCost;
                            product2.UnitCost3 = product.UnitCost;
                            product2.UnitCost4 = product.UnitCost;
                            product2.UnitCost5 = product.UnitCost;
                            product2.UnitCost6 = product.UnitCost;
                            product2.Description = product.Description;
                            product2.DescriptionHTML = product.DescriptionHTML;
                            product2.URL = product.URL;
                            product2.ProductCost = product.UnitCost;
                            product2.ProductImage = product.Image;
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
                  // rc = true;
                }
            }
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                //message = ex.Message;
                log.Error("Error error logging", ex);
            }
            //return rc;
        }
    }
}
