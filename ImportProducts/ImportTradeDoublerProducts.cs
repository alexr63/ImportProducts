using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ImportProducts
{
    class ImportTradeDoublerProducts
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

        public static bool SaveFileFromURL(string url, string destinationFileName, int timeoutInSeconds)
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
                        // Open the destination file
                        using (
                            FileStream MyFileStream = new FileStream(destinationFileName, FileMode.OpenOrCreate,
                                                                     FileAccess.Write))
                        {
                            // Create a 4K buffer to chunk the file
                            byte[] MyBuffer = new byte[4096];
                            int BytesRead;
                            // Read the chunk of the web response into the buffer
                            while (0 < (BytesRead = MyResponseStream.Read(MyBuffer, 0, MyBuffer.Length)))
                            {
                                // Write the chunk from the buffer to the file
                                MyFileStream.Write(MyBuffer, 0, BytesRead);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                throw new Exception("Error saving file from URL:" + err.Message, err);
            }
            return true;
        }

        public static bool DoImport(string _URL, out string message)
        {
            bool rc = false;
            message = String.Empty;

#if !NONAME
            string xmlFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
            SaveFileFromURL(_URL, xmlFileName, 60);
            _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "tradedoubler.xml");
#endif

#if NONAME
            var test =
                from el in StreamRootChildDoc(_URL)
                select new
                           {
                               Category = (string) el.Element("TDCategoryName"),
                               ProductNumber = (string) el.Element("TDProductId"),
                               Name = (string) el.Element("name"),
                               Image = (string) el.Element("imageUrl"),
                               UnitCost = (decimal) el.Element("price"),
                               Description = (string) el.Element("description"),
                               DescriptionHTML = (string) el.Element("description"),
                               URL = (string) el.Element("productUrl")
                           };
            Console.WriteLine(String.Format("Total count: {0}", test.Count()));
#endif

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
                    URL = (string)el.Element("productUrl")
                };
            try
            {
                using (DNN_6_0_0Entities db = new DNN_6_0_0Entities())
                {
                    foreach (var product in products)
                    {
                        Console.WriteLine(product.Name); // debug print

                        Category category = db.Categories.SingleOrDefault(c => c.CategoryName == product.Category);
                        if (category == null)
                        {
                            category = new Category
                            {
                                CategoryName = product.Category,
                                Description = String.Empty,
                                PortalID = 0,
                                CategoryImportID = String.Empty,
                                CategoryFolderImage = String.Empty,
                                CategoryOpenFolderImage = String.Empty,
                                CategoryPageImage = String.Empty,
                                CreatedByUser = 1
                            };
                            db.Categories.Add(category);
                            db.SaveChanges();
                        }

                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == category.CategoryID && p.ProductNumber == product.ProductNumber);
                        if (product2 == null)
                        {
                            product2 = new Product
                            {
                                CategoryID = category.CategoryID,
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
                            product2.URL = product.URL;
                            product2.ProductCost = product.UnitCost;
                            product2.ProductImage = product.Image;
                            product2.OrderQuant = "0";

                            db.SaveChanges();
                        }
                    }
                    rc = true;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            return rc;
        }
    }
}
