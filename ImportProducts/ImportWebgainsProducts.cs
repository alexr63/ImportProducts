﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace ImportProducts
{
    class ImportWebgainsProducts
    {
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

        public static void DoImport(object sender, DoWorkEventArgs e)               //(string _URL, out string message)        // public static bool
        {
            //bool rc = false;
            //message = String.Empty;
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters)e.Argument;
            string _URL = param.Url;
            int portalId = param.PortalId;

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

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0);           // start new step of background process

            var products =
                from el in StreamRootChildDoc(_URL)
                select new
                {
                    Category = (string)el.Element("categories").Element("category").Attribute("path"),
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
                using (DNN_6_0_0Entities db = new DNN_6_0_0Entities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name.Replace("&apos;", "'")); // debug print

                        Product product2 = null;
                        var strCategory = product.Category;
                        var strCategory2 = String.Empty;
                        if (strCategory.Contains(" > "))
                        {
                            string[] categories = strCategory.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
                            strCategory = categories[0];
                            strCategory2 = categories[1];
                        }
                        Category category =
                            db.Categories.SingleOrDefault(c => c.CategoryName == strCategory && c.PortalID == portalId);
                        if (category == null)
                        {
                            category = new Category
                                           {
                                               CategoryName = strCategory,
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
                        Category category2 = null;
                        if (strCategory2 != String.Empty)
                        {
                            category2 =
                                db.Categories.SingleOrDefault(
                                    c => c.CategoryName == strCategory2 && c.PortalID == portalId);
                            if (category2 == null)
                            {
                                category2 = new Category
                                               {
                                                   CategoryName = strCategory2,
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
                        }

                        if (strCategory2 == String.Empty)
                        {
                            product2 = db.Products.SingleOrDefault(p => p.CategoryID == category.CategoryID && p.ProductNumber == product.ProductNumber);
                        }
                        else
                        {
                            product2 = db.Products.SingleOrDefault(p => p.CategoryID == category.CategoryID && p.Category2ID == category2.CategoryID && p.ProductNumber == product.ProductNumber);
                        }
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                CategoryID = category.CategoryID,
                                Category2ID = category2 != null ? category2.CategoryID : 0,
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
                //throw new Exception("Error saving file from URL:" + ex.Message, ex);
                //message = ex.Message;
            }
            //return rc;
        }
    }
}