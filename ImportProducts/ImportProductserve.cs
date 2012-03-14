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
            int portalId = param.PortalId;

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
            Thread.Sleep(1000); // a little bit slow working for visualisation Progress

           // Set step for backgroundWorker
           Form1.activeStep = "Import records..";
           bw.ReportProgress(0);           // start new step of background process
           long countProducts = products.Count();
           long currentProduct = 0;

            try
            {
                using (DNN_6_0_0Entities db = new DNN_6_0_0Entities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name); // debug print

                        Category category = db.Categories.SingleOrDefault(c => c.CategoryName == product.Category && c.PortalID == portalId);
                        if (category == null)
                        {
                            category = new Category
                            {
                                CategoryName = product.Category,
                                Description = String.Empty,
                                PortalID = portalId,
                                CategoryImportID = String.Empty,
                                CategoryFolderImage = String.Empty,
                                CategoryOpenFolderImage = String.Empty,
                                CategoryPageImage = String.Empty,
                                CreatedByUser = 1
                            };
                            db.Categories.Add(category);
                            db.SaveChanges();
                        }
                        Category category2 = db.Categories.SingleOrDefault(c => c.CategoryName == product.Category2 && c.PortalID == portalId);
                        if (category2 == null)
                        {
                            category2 = new Category
                            {
                                CategoryName = product.Category2,
                                Description = String.Empty,
                                PortalID = portalId,
                                CategoryImportID = String.Empty,
                                CategoryFolderImage = String.Empty,
                                CategoryOpenFolderImage = String.Empty,
                                CategoryPageImage = String.Empty,
                                CreatedByUser = 1
                            };
                            db.Categories.Add(category2);
                            db.SaveChanges();
                        }

                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == category.CategoryID && p.ProductNumber == product.ProductNumber);
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                CategoryID = category.CategoryID,
                                Category2ID = category2.CategoryID,
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
                                DateCreated = DateTime.Now
                            };

                            product2.ProductImage = product.Image;
                            product2.OrderQuant = "0";

                            db.Products.Add(product2);
                            db.SaveChanges();
                        }
                        else
                        {
                            product2.CategoryID = category.CategoryID;
                            product2.Category2ID = category2.CategoryID;
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

                        // break application after first record - enough for debiggung/discussing
                        //break;
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
