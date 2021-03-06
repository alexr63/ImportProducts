﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Xml.Schema;
using Ionic.Zip;

namespace ImportProducts
{


    class ImportProductserve
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static IEnumerable<XElement> StreamRootChildDoc(string uri)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            using (XmlReader reader = XmlReader.Create(uri, settings))
            {
                reader.MoveToContent();
                // Parse the file and display each of the nodes.
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == "prod")
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
            string countryFilter = param.CountryFilter;
            string cityFilter = param.CityFilter;

            string zipFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath,
                                                "datafeed.zip");
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
                    _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, zipEntry.FileName);
                }
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add("", "datafeed.xsd");
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
                    ProductNumber = (string)el.Attribute("id"),
                    Name = (string)el.Element("text").Element("name"),
                    Description = (string)el.Element("text").Element("desc"),
                    DescriptionHTML = (string)el.Element("text").Element("desc"),
                    UnitCost = (decimal)el.Element("price").Element("buynow"),
                    Category = (string)el.Element("cat").Element("mCat"),
                    Category2 = (string)el.Element("cat").Element("awCat"),
                    URL = (string)el.Element("uri").Element("mLink"),
                    Image = (string)el.Element("uri").Element("mImage"),
                };
            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true; return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(100);
            Thread.Sleep(100); // a little bit slow working for visualisation Progress

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

                        int? parentID = null;
                        int? catRootID = null;
                        int? catCategoryID = null;
                        int? catCategory2ID = null;
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

                        if (!String.IsNullOrEmpty(product.Category))
                        {
                            var advCatCategory =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == product.Category && ac.Level == level);
                            if (advCatCategory == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCategory = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = product.Category,
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
                                db.AdvCats.Add(advCatCategory);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCategory.AdvCatID);
                            }
                            parentID = advCatCategory.AdvCatID;
                            catCategoryID = advCatCategory.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product.Category2))
                        {
                            var advCatCategory2 =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == product.Category2 && ac.Level == level);
                            if (advCatCategory2 == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCategory2 = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = portalId,
                                    AdvCatName = product.Category2,
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
                                db.AdvCats.Add(advCatCategory2);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCategory2.AdvCatID);
                            }
                            parentID = advCatCategory2.AdvCatID;
                            catCategory2ID = advCatCategory2.AdvCatID;
                            level++;
                        }

                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber);
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
                            bool isChanged = false;
                            if (product2.CategoryID != categoryId)
                            {
                                product2.CategoryID = categoryId;
                                isChanged = true;
                            }
                            if (product2.Category2ID != 0)
                            {
                                product2.Category2ID = 0;
                                isChanged = true;
                            }
                            if (product2.ProductName != product.Name.Replace("&apos;", "'"))
                            {
                                product2.ProductName = product.Name.Replace("&apos;", "'");
                                isChanged = true;
                            }
                            if (product2.ProductNumber != product.ProductNumber)
                            {
                                product2.ProductNumber = product.ProductNumber;
                                isChanged = true;
                            }
                            if (product2.UnitCost != product.UnitCost)
                            {
                                product2.UnitCost = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.UnitCost2 != product.UnitCost)
                            {
                                product2.UnitCost2 = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.UnitCost3 != product.UnitCost)
                            {
                                product2.UnitCost3 = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.UnitCost4 != product.UnitCost)
                            {
                                product2.UnitCost4 = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.UnitCost5 != product.UnitCost)
                            {
                                product2.UnitCost5 = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.UnitCost6 != product.UnitCost)
                            {
                                product2.UnitCost6 = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.Description != product.Description)
                            {
                                product2.Description = product.Description;
                                isChanged = true;
                            }
                            if (product2.DescriptionHTML != product.DescriptionHTML)
                            {
                                product2.DescriptionHTML = product.DescriptionHTML;
                                isChanged = true;
                            }
                            if (product2.URL != product.URL)
                            {
                                product2.URL = product.URL;
                                isChanged = true;
                            }
                            if (product2.ProductCost != product.UnitCost)
                            {
                                product2.ProductCost = product.UnitCost;
                                isChanged = true;
                            }
                            if (product2.ProductImage != product.Image)
                            {
                                product2.ProductImage = product.Image;
                                isChanged = true;
                            }
                            if (product2.OrderQuant != "0")
                            {
                                product2.OrderQuant = "0";
                                isChanged = true;
                            }

                            if (isChanged)
                            {
                                db.SaveChanges();
                            }
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
                        if (catCategoryID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCategoryID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCategoryID.Value,
                                ProductID = product2.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCategory2ID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCategory2ID.Value && act.ProductID == product2.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCategory2ID.Value,
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
