using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ImportProducts
{
    class ImportHotels
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

        public static bool DoImport(string _URL, out string message)
        {
            bool rc = false;
            message = String.Empty;
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
                        Console.WriteLine(hotel.Name); // debug print

                        // create advanced categories
                        // todo: send PortalId as command line parameter
                        var hotel1 = hotel;
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
                                    AdvCatName = hotel.City,
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
                        var product = db.Products.SingleOrDefault(p => p.CategoryID == 3 && p.ProductName == hotel.Name);
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
                                ProductImage productImage = new ProductImage();
                                productImage.ImageFile = image.Value;
                                product.ProductImages.Add(productImage);
                            }

                            // add product to product set
                            db.Products.Add(product);
                            // store  changes
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
                        break;
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
