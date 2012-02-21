using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ionic.Zip;

namespace ImportProducts
{
    class ImportHotels
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


        public static bool SaveFileFromURL(string url, string destinationFileName, int timeoutInSeconds)
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

#if !DEBUG
            // unzip file to temp folder if needed
            if (_URL.EndsWith(".zip"))
            {
                string zipFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath,
                                                   "Hotels_Standard.zip");
                SaveFileFromURL(_URL, zipFileName, 60);
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
#else
            _URL = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath, "Hotels_Standard.xml");
#endif

            // read hotels from XML
            // use XmlReader to avoid huge file size dependence
            var hotels =
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

            try
            {
                using (DNN_6_0_0Entities db = new DNN_6_0_0Entities())
                {
                    foreach (var hotel in hotels)
                    {
#if DEBUG
                        if (hotel.Country != "England")
                        {
                            continue;
                        }
#endif

                        Console.WriteLine(hotel.Name); // debug print

                        // create advanced categories
                        // todo: send PortalId as command line parameter
                        var hotel1 = hotel;
                        string hotelCity = hotel1.City;
                        if (hotel1.City.Length > 50)
                        {
                            hotelCity = hotel1.City.Substring(0, 47).PadRight(50, '.');
                        }
                        int? parentID = null;
                        int? catCountryID = null;
                        int? catCountyID = null;
                        int? catCityID = null;
                        int level = 0;
                        int maxOrder = 0;
                        if (!String.IsNullOrEmpty(hotel1.Country))
                        {
                            var advCatCountry =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == 0 && ac.AdvCatName == hotel1.Country && ac.Level == level);
                            if (advCatCountry == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCountry = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = 0,
                                    AdvCatName = hotel.Country,
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
                                db.AdvCats.Add(advCatCountry);
                                db.SaveChanges();

                                AddAdvCatDefaultPermissions(db, advCatCountry.AdvCatID);
                            }
                            parentID = advCatCountry.AdvCatID;
                            catCountryID = advCatCountry.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(hotel1.County))
                        {
                            var advCatCounty =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == 0 && ac.AdvCatName == hotel1.County && ac.Level == level);
                            if (advCatCounty == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCounty = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = 0,
                                    AdvCatName = hotel.County,
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

                                AddAdvCatDefaultPermissions(db, advCatCounty.AdvCatID);
                            }
                            parentID = advCatCounty.AdvCatID;
                            catCountyID = advCatCounty.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(hotel1.City))
                        {
                            var advCatCity =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == 0 && ac.AdvCatName == hotel1.City && ac.Level == level);
                            if (advCatCity == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCity = new AdvCat
                                {
                                    AdvCatOrder = maxOrder + 1,
                                    PortalID = 0,
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

                                AddAdvCatDefaultPermissions(db, advCatCity.AdvCatID);
                            }
                            parentID = advCatCity.AdvCatID;
                            catCityID = advCatCity.AdvCatID;
                            level++;
                        }

                        // create new product record
                        var product = db.Products.SingleOrDefault(p => p.CategoryID == 3 && p.ProductNumber == hotel.ProductNumber);
                        if (product == null)
                        {
                            product = new Product
                            {
                                CategoryID = 3,
                                // Hotels category ID
                                Category2ID = 0,
                                Category3 = String.Empty,
                                ProductName = hotel.Name,
                                ProductNumber = hotel.ProductNumber,
                                UnitCost = hotel.UnitCost,
                                UnitCost2 = hotel.UnitCost,
                                UnitCost3 = hotel.UnitCost,
                                UnitCost4 = hotel.UnitCost,
                                UnitCost5 = hotel.UnitCost,
                                UnitCost6 = hotel.UnitCost,
                                Description = hotel.Description,
                                DescriptionHTML = hotel.DescriptionHTML,
                                URL = hotel.URL,
                                ProductCost = hotel.UnitCost,
                                DateCreated = DateTime.Now
                            };

                            // add additional product images
                            product.ProductImage = (string)hotel.Images.Element("url");
                            foreach (var image in hotel.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    ProductImage productImage = new ProductImage();
                                    productImage.ImageFile = image.Value;
                                    product.ProductImages.Add(productImage);
                                }
                            }
                            // trick to hide Add To Cart button
                            product.OrderQuant = "0";

                            // add product to product set
                            db.Products.Add(product);
                            // store  changes
                            db.SaveChanges();
                        }
                        else
                        {
                            product.ProductName = hotel.Name;
                            product.UnitCost = hotel.UnitCost;
                            product.UnitCost2 = hotel.UnitCost;
                            product.UnitCost3 = hotel.UnitCost;
                            product.UnitCost4 = hotel.UnitCost;
                            product.UnitCost5 = hotel.UnitCost;
                            product.UnitCost6 = hotel.UnitCost;
                            product.Description = hotel.Description;
                            product.DescriptionHTML = hotel.DescriptionHTML;
                            product.URL = hotel.URL;
                            product.ProductCost = hotel.UnitCost;

                            product.ProductImage = (string)hotel.Images.Element("url");

                            foreach (var productImage in product.ProductImages)
                            {
                                bool imageExists = false;
                                foreach (var image in hotel1.Images.Elements("url"))
                                {
                                    if (productImage.ImageFile == image.Value)
                                    {
                                        imageExists = true;
                                        break;
                                    }
                                }
                                if (!imageExists)
                                {
                                    product.ProductImages.Remove(productImage);
                                }
                            }
                            foreach (var image in hotel1.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    if (!product.ProductImages.Any(pi => pi.ImageFile == image.Value))
                                    {
                                        ProductImage productImage = new ProductImage();
                                        productImage.ImageFile = image.Value;
                                        product.ProductImages.Add(productImage);
                                    }
                                }
                            }

                            product.OrderQuant = "0";

                            db.SaveChanges();
                        }

                        // add product to advanced categories
                        if (catCountryID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCountryID.Value && act.ProductID == product.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCountryID.Value,
                                ProductID = product.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCountyID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCountyID.Value && act.ProductID == product.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCountyID.Value,
                                ProductID = product.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }
                        if (catCityID.HasValue && db.AdvCatProducts.SingleOrDefault(act => act.AdvCatID == catCityID.Value && act.ProductID == product.ProductID) == null)
                        {
                            AdvCatProduct advCatProduct = new AdvCatProduct
                            {
                                AdvCatID = catCityID.Value,
                                ProductID = product.ProductID,
                                AddAdvCatToProductDisplay = false
                            };
                            db.AdvCatProducts.Add(advCatProduct);
                            db.SaveChanges();
                        }

                        // break application after first record - enough for debiggung/discussing
                        //break;
                    }
                    rc = true;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                log.Error("Error error logging", ex);
            }
            return rc;
        }

        private static void AddAdvCatDefaultPermissions(DNN_6_0_0Entities db, int advCatID)
        {
            var advCatPermission = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 1,
                RoleID = 0,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission);
            var advCatPermission2 = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 2,
                RoleID = 0,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission2);
            var advCatPermission3 = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 1,
                RoleID = -1,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission3);
            db.SaveChanges();
        }
    }
}
