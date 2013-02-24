using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    class ImportWebgainsProducts
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

            string xmlFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "webgainsproducts.xml");
            if (File.Exists(xmlFileName))
            {
                File.Delete(xmlFileName);
            }
            // inside function display progressbar
            SaveFileFromURL(_URL, xmlFileName, 60,bw,e);

            // exit if user cancel during saving file or error
             if (e.Cancel || (e.Result != null) && e.Result.ToString().Substring(0, 6).Equals("ERROR:")) return;

             _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "webgainsproducts.xml");

             XmlSchemaSet schemas = new XmlSchemaSet();
             schemas.Add("", "webgainsproducts.xsd");
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
                    Category = (string)el.Element("categories").Element("category"),
                    CategoryPath = (string)el.Element("categories").Element("category").Attribute("path"),
                    ProductNumber = (string)el.Element("product_id"),
                    Name = (string)el.Element("product_name"),
                    Image = (string)el.Element("image_url"),
                    UnitCost = (decimal)el.Element("price"),
                    Description = (string)el.Element("description"),
                    DescriptionHTML = (string)el.Element("description"),
                    URL = (string)el.Element("deeplink")
                };


            long countProducts = products.Count();
            long currentProduct = 0;

            try
            {
                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name.Replace("&apos;", "'")); // debug print

                        Category category;
                        if (!String.IsNullOrEmpty(product.CategoryPath))
                        {
                            if (product.CategoryPath.Contains(" > "))
                            {
                                string[] categoryNames = product.CategoryPath.Split(new[] {" > "},
                                                                                    StringSplitOptions.
                                                                                        RemoveEmptyEntries);
                                string rootCategoryName = categoryNames[0];

                                category =
                                    db.Categories.SingleOrDefault(
                                        c => c.Name == rootCategoryName && c.ParentId == null);
                                if (category == null)
                                {
                                    category = new Category
                                                   {
                                                       IsDeleted = false,
                                                       Name = rootCategoryName
                                                   };
                                    db.Categories.Add(category);
                                    db.SaveChanges();
                                }
                                Category parentCategory = category;

                                string subCategoryName = categoryNames[1];
                                category =
                                    db.Categories.SingleOrDefault(
                                        c => c.Name == subCategoryName && c.ParentId == parentCategory.Id);
                                if (category == null)
                                {
                                    category = new Category
                                                   {
                                                       IsDeleted = false,
                                                       Name = subCategoryName,
                                                       ParentCategory = parentCategory
                                                   };
                                    db.Categories.Add(category);
                                    db.SaveChanges();
                                }
                            }
                            else
                            {
                                category =
                                    db.Categories.SingleOrDefault(
                                        c => c.Name == product.CategoryPath && c.ParentId == null);
                                if (category == null)
                                {
                                    category = new Category
                                                   {
                                                       IsDeleted = false,
                                                       Name = product.CategoryPath
                                                   };
                                    db.Categories.Add(category);
                                    db.SaveChanges();
                                }
                            }
                        }
                        else
                        {
                            category =
                                db.Categories.SingleOrDefault(
                                    c => c.Name == product.Category && c.ParentId == null);
                            if (category == null)
                            {
                                category = new Category
                                {
                                    IsDeleted = false,
                                    Name = product.Category
                                };
                                db.Categories.Add(category);
                                db.SaveChanges();
                            }
                        }

                        var product2 = db.Products.SingleOrDefault(p => p.Name == product.Name && p.Number == product.ProductNumber);
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                Name = product.Name.Replace("&apos;", "'"),
                                Number = product.ProductNumber,
                                UnitCost = product.UnitCost,
                                Description = product.Description,
                                URL = product.URL,
                                Image = product.Image,
                                CreatedByUser = vendorId,
                                IsDeleted = false
                            };

                            db.Products.Add(product2);
                            db.SaveChanges();
                        }
                        else
                        {
                            product2.UnitCost = product.UnitCost;
                            product2.Description = product.Description;
                            product2.URL = product.URL;
                            product2.Image = product.Image;
                            db.SaveChanges();
                        }

                        // add product to advanced categories
                        if (!product2.Categories.Contains(category))
                        {
                            product2.Categories.Add(category);
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
