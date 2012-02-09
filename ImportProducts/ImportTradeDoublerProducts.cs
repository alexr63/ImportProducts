using System;
using System.Collections.Generic;
using System.Linq;
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

        public static bool DoImport(string _URL, out string message)
        {
            bool rc = false;
            message = String.Empty;
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

                        var product2 = db.Products.SingleOrDefault(p => p.CategoryID == category.CategoryID && p.ProductName == product.Name);
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

                            db.Products.Add(product2);
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
