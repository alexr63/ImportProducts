using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using SelectedHotelsModel;

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

#if VALIDATE
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
#endif

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0);           // start new step of background process

            var xmlProducts =
                from el in StreamRootChildDoc(_URL)
                select new
                {
                    Category = (string)el.Element("TDCategoryName"),
                    MerchantCategory = (string)el.Element("merchantCategoryName"),
                    ProductNumber = (string)el.Element("TDProductId"),
                    Name = (string)el.Element("name"),
                    Image = (string)el.Element("imageUrl"),
                    UnitCost = (decimal)el.Element("price"),
                    CurrencyCode = (string)el.Element("currency"),
                    Description = (string)el.Element("description"),
                    DescriptionHTML = (string)el.Element("description"),
                    URL = (string)el.Element("productUrl"),
                    Country = (string)el.Element("fields").Element("country"),
                    City = (string)el.Element("fields").Element("city"),
                    Weight = (string)el.Element("weight"),
                    Size = (string)el.Element("size"),
                    Brand = (string)el.Element("brand"),
                    Model = (string)el.Element("model"),
                    Manufacturer = (string)el.Element("manufacturer"),
                    Colours = (string)el.Element("fields").Element("Colours"),
                    Department = (string)el.Element("fields").Element("Department"),
                    Gender = (string)el.Element("fields").Element("Gender"),
                    Image1 = (string)el.Element("fields").Element("Image1_Large"),
                    Image2 = (string)el.Element("fields").Element("Image2_Large"),
                    Image3 = (string)el.Element("fields").Element("Image3_Large"),
                    Image4 = (string)el.Element("fields").Element("Image4_Large"),
                    Style = (string)el.Element("fields").Element("Product_Style"),
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
                xmlProducts = xmlProducts.Where(p => p.Country == countryFilter);
            }

            if (!String.IsNullOrEmpty(cityFilter))
            {
                xmlProducts = xmlProducts.Where(p => p.City == cityFilter);
            }

            long countProducts = xmlProducts.Count();
            long currentProduct = 0;

            try
            {
                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    var category = db.Categories.Find(categoryId);

                    foreach (var xmlProduct in xmlProducts)
                    {
                        string productName = xmlProduct.Name.Replace("&apos;", "'");
#if DEBUG
                        Console.WriteLine(productName); // debug print
#endif
#if !DEBUG
                        Category subCategory = db.Categories.SingleOrDefault(
                            c =>
                            c.Name == xmlProduct.Category &&
                            c.ParentId == categoryId);
                        if (subCategory == null)
                        {
                            subCategory = new Category();
                            subCategory.Name = xmlProduct.Category;
                            subCategory.ParentId = categoryId;
                            subCategory.IsDeleted = false;
                            db.Categories.Add(subCategory);
                            db.SaveChanges();
                        }

                        MerchantCategory merchantCategory = null;
                        if (!String.IsNullOrEmpty(xmlProduct.MerchantCategory))
                        {
                            merchantCategory =
                                db.MerchantCategories.SingleOrDefault(mc => mc.Name == xmlProduct.MerchantCategory);
                            if (merchantCategory == null)
                            {
                                merchantCategory = new MerchantCategory();
                                merchantCategory.Name = xmlProduct.MerchantCategory;
                                db.MerchantCategories.Add(merchantCategory);
                                db.SaveChanges();
                            }
                        }

                        Brand brand = null;
                        if (!String.IsNullOrEmpty(xmlProduct.Brand))
                        {
                            brand =
                                db.Brands.SingleOrDefault(b => b.Name == xmlProduct.Brand);
                            if (brand == null)
                            {
                                brand = new Brand();
                                brand.Name = xmlProduct.Brand;
                                db.Brands.Add(brand);
                                db.SaveChanges();
                            }
                        }
#endif

                        if (category.Name == "Clothes")
                        {
                            Cloth product =
                                db.Products.OfType<Cloth>().SingleOrDefault(
                                    p =>
                                        p.Name == productName &&
                                        p.Number == xmlProduct.ProductNumber);
                            if (product == null)
                            {
#if !DEBUG
                                product = new Cloth
                                {
                                    Name = xmlProduct.Name.Replace("&apos;", "'"),
                                    ProductTypeId = (int) Enums.ProductTypeEnum.Clothes,
                                    Number = xmlProduct.ProductNumber,
                                    UnitCost = xmlProduct.UnitCost,
                                    CurrencyCode = xmlProduct.CurrencyCode,
                                    Description = xmlProduct.Description,
                                    URL = xmlProduct.URL,
                                    Image = xmlProduct.Image,
                                    CreatedByUser = vendorId,
                                    CreatedDate = DateTime.Now,
                                    Colour = xmlProduct.Colours,
                                    Gender = xmlProduct.Gender,
                                    IsDeleted = false
                                };

                                product.Categories.Add(subCategory);
                                product.MerchantCategory = merchantCategory;
                                product.Brand = brand;
                                db.Products.Add(product);
                                db.SaveChanges();

                                List<string> imageURLList = new List<string>
                                {
                                    xmlProduct.Image1,
                                    xmlProduct.Image2,
                                    xmlProduct.Image3,
                                    xmlProduct.Image4
                                };
                                foreach (var imageURL in imageURLList)
                                {
                                    if (!String.IsNullOrEmpty(imageURL))
                                    {
                                        ProductImage productImage = new ProductImage {URL = imageURL};
                                        product.ProductImages.Add(productImage);
                                    }
                                }
                                db.SaveChanges();

                                if (!String.IsNullOrEmpty(xmlProduct.Style))
                                {
                                    List<string> styleList = new List<string>();
                                    if (xmlProduct.Style.Contains(","))
                                    {
                                        foreach (var style in xmlProduct.Style.Split(','))
                                        {
                                            if (!String.IsNullOrEmpty(style))
                                            {
                                                styleList.Add(style);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        styleList.Add(xmlProduct.Style);
                                    }

                                    foreach (var styleName in styleList)
                                    {
                                        Style style = db.Styles.SingleOrDefault(s => s.Name == styleName);
                                        if (style == null)
                                        {
                                            style = new Style {Name = styleName};
                                        }
                                        product.Styles.Add(style);
                                    }
                                }

                                if (!String.IsNullOrEmpty(xmlProduct.Department))
                                {
                                    List<string> departmentList = new List<string>();
                                    if (xmlProduct.Department.Contains(","))
                                    {
                                        foreach (var department in xmlProduct.Department.Split(','))
                                        {
                                            if (!String.IsNullOrEmpty(department))
                                            {
                                                departmentList.Add(department);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        departmentList.Add(xmlProduct.Department);
                                    }

                                    foreach (var departmentName in departmentList)
                                    {
                                        Department department = db.Departments.SingleOrDefault(d => d.Name == departmentName);
                                        if (department == null)
                                        {
                                            department = new Department{ Name = departmentName };
                                        }
                                        product.Departments.Add(department);
                                    }
                                }

                                List<string> clothSizeList = new List<string>();
                                if (xmlProduct.Size.Contains(","))
                                {
                                    foreach (var size in xmlProduct.Size.Split(','))
                                    {
                                        if (!String.IsNullOrEmpty(size))
                                        {
                                            clothSizeList.Add(size);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!String.IsNullOrEmpty(xmlProduct.Size))
                                    {
                                        clothSizeList.Add(xmlProduct.Size);
                                    }
                                }

                                foreach (var size in clothSizeList)
                                {
                                    ClothSize clothSize = new ClothSize { Size = size };
                                    product.ClothSizes.Add(clothSize);
                                }
                                db.SaveChanges();
#endif
                            }
                            else
                            {
#if !DEBUG
                                product.UnitCost = xmlProduct.UnitCost;
                                product.CurrencyCode = xmlProduct.CurrencyCode;
                                product.Description = xmlProduct.Description;
                                product.URL = xmlProduct.URL;
                                product.Image = xmlProduct.Image;
                                product.Colour = xmlProduct.Colours;
                                product.ProductTypeId = (int) Enums.ProductTypeEnum.Clothes;
                                product.MerchantCategory = merchantCategory;
                                product.Brand = brand;
                                product.Gender = xmlProduct.Gender;
                                db.SaveChanges();

                                if (!product.Categories.Contains(subCategory))
                                {
                                    product.Categories.Add(subCategory);
                                }
                                var productCategoriesToRemove = product.Categories.Where(pc => pc.Name != subCategory.Name);
                                foreach (Category productCategoryToRemove in productCategoriesToRemove)
                                {
                                    product.Categories.Remove(productCategoryToRemove);
                                }
                                db.SaveChanges();

                                List<string> imageURLList = new List<string>
                                {
                                    xmlProduct.Image1,
                                    xmlProduct.Image2,
                                    xmlProduct.Image3,
                                    xmlProduct.Image4
                                };
                                foreach (var imageURL in imageURLList)
                                {
                                    if (!String.IsNullOrEmpty(imageURL))
                                    {
                                        ProductImage productImage =
                                            product.ProductImages.FirstOrDefault(pi => pi.URL == imageURL);
                                        if (productImage == null)
                                        {
                                            productImage = new ProductImage {URL = imageURL};
                                            product.ProductImages.Add(productImage);
                                        }
                                    }
                                }
                                db.SaveChanges();

                                var productImagesToRemove = db.ProductImages.Where(pi => pi.ProductId == product.Id &&
                                                                                         imageURLList.All(
                                                                                             xe => xe != pi.URL));
                                if (productImagesToRemove.Any())
                                {
                                    db.ProductImages.RemoveRange(productImagesToRemove);
                                }
                                db.SaveChanges();

                                if (!String.IsNullOrEmpty(xmlProduct.Style))
                                {
                                    List<string> styleList = new List<string>();
                                    if (xmlProduct.Style.Contains(","))
                                    {
                                        foreach (var style in xmlProduct.Style.Split(','))
                                        {
                                            if (!String.IsNullOrEmpty(style))
                                            {
                                                styleList.Add(style);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        styleList.Add(xmlProduct.Style);
                                    }

                                    foreach (var styleName in styleList)
                                    {
                                        if (!String.IsNullOrEmpty(styleName))
                                        {
                                            Style style = db.Styles.SingleOrDefault(s => s.Name == styleName);
                                            if (style == null)
                                            {
                                                style = new Style {Name = styleName};
                                            }
                                            product.Styles.Add(style);
                                        }
                                    }
                                    db.SaveChanges();

                                    var stylesToRemove = product.Styles.Where(s => !styleList.Any(sl => sl == s.Name));
                                    if (stylesToRemove.Any())
                                    {
                                        foreach (var styleToRemove in stylesToRemove.ToList())
                                        {
                                            product.Styles.Remove(styleToRemove);
                                        }
                                        db.SaveChanges();
                                    }
                                }
#endif
                                if (!String.IsNullOrEmpty(xmlProduct.Department))
                                {
                                    List<string> departmentList = new List<string>();
                                    if (xmlProduct.Department.Contains(","))
                                    {
                                        foreach (var department in xmlProduct.Department.Split(','))
                                        {
                                            if (!String.IsNullOrEmpty(department))
                                            {
                                                departmentList.Add(department);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        departmentList.Add(xmlProduct.Department);
                                    }

                                    foreach (var departmentName in departmentList)
                                    {
                                        if (!String.IsNullOrEmpty(departmentName))
                                        {
                                            Department department = db.Departments.SingleOrDefault(d => d.Name == departmentName);
                                            if (department == null)
                                            {
                                                department = new Department { Name = departmentName };
                                            }
                                            product.Departments.Add(department);
                                        }
                                    }
                                    db.SaveChanges();

                                    var departmentsToRemove = product.Departments.Where(d => !departmentList.Any(dl => dl == d.Name));
                                    if (departmentsToRemove.Any())
                                    {
                                        foreach (var departmentToRemove in departmentsToRemove.ToList())
                                        {
                                            product.Departments.Remove(departmentToRemove);
                                        }
                                        db.SaveChanges();
                                    }
                                }

#if !DEBUG
                                List<string> clothSizeList = new List<string>();
                                if (xmlProduct.Size.Contains(","))
                                {
                                    foreach (var size in xmlProduct.Size.Split(','))
                                    {
                                        if (!String.IsNullOrEmpty(size))
                                        {
                                            clothSizeList.Add(size);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!String.IsNullOrEmpty(xmlProduct.Size))
                                    {
                                        clothSizeList.Add(xmlProduct.Size);
                                    }
                                }

                                foreach (var size in clothSizeList)
                                {
                                    if (!String.IsNullOrEmpty(size))
                                    {
                                        ClothSize clothSize =
                                            product.ClothSizes.SingleOrDefault(cs => cs.Size == size);
                                        if (clothSize == null)
                                        {
                                            clothSize = new ClothSize { Size = size };
                                            product.ClothSizes.Add(clothSize);
                                        }

                                    }
                                }
                                db.SaveChanges();

                                var clothSizesToRemove = db.ClothSizes.Where(cs => cs.ClothId == product.Id &&
                                                                                         clothSizeList.All(
                                                                                             csl => csl != cs.Size));
                                if (clothSizesToRemove.Any())
                                {
                                    db.ClothSizes.RemoveRange(clothSizesToRemove);
                                    db.SaveChanges();
                                }
#endif
                            }
                        }
#if HOMEANDGARDENS
                        else if (categoryId == (int) Enums.ProductTypeEnum.HomeAndGardens)
                        {
                            HomeAndGarden product =
                                db.Products.OfType<HomeAndGarden>().SingleOrDefault(
                                    p =>
                                        p.Name == productName &&
                                        p.Number == xmlProduct.ProductNumber);
                            if (product == null)
                            {
                                product = new HomeAndGarden
                                {
                                    Name = xmlProduct.Name.Replace("&apos;", "'"),
                                    ProductTypeId = (int) Enums.ProductTypeEnum.HomeAndGardens,
                                    Number = xmlProduct.ProductNumber,
                                    UnitCost = xmlProduct.UnitCost,
                                    Description = xmlProduct.Description,
                                    URL = xmlProduct.URL,
                                    Image = xmlProduct.Image,
                                    CreatedByUser = vendorId,
                                    Weight = xmlProduct.Weight,
                                    Size = xmlProduct.Size,
                                    Brand = xmlProduct.Brand,
                                    Model = xmlProduct.Model,
                                    Manufacturer = xmlProduct.Manufacturer,
                                };

                                product.Categories.Add(subCategory);
                                db.Products.Add(product);
                                db.SaveChanges();
                            }
                            else
                            {
                                product.UnitCost = xmlProduct.UnitCost;
                                product.Description = xmlProduct.Description;
                                product.URL = xmlProduct.URL;
                                product.Categories.Add(subCategory);
                                db.SaveChanges();
                            }

                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("DELETE");
                            sb.AppendLine("FROM         Cowrie_ProductImages");
                            sb.AppendLine("WHERE     ProductID = @ProductID");
                            SqlCommand commandDelete =
                                new SqlCommand(
                                    sb.ToString(),
                                    destinationConnection);
                            commandDelete.Parameters.Add("@ProductID", SqlDbType.Int);
                            commandDelete.Parameters["@ProductID"].Value = product.Id;
                            commandDelete.ExecuteNonQuery();

                            bool isChanged = false;
                            if (xmlProduct.Image1 != null)
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = xmlProduct.Image1;
                                product.ProductImages.Add(productImage);
                                isChanged = true;
                            }
                            if (xmlProduct.Image2 != null)
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = xmlProduct.Image2;
                                product.ProductImages.Add(productImage);
                                isChanged = true;
                            }
                            if (xmlProduct.Image3 != null)
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = xmlProduct.Image3;
                                product.ProductImages.Add(productImage);
                                isChanged = true;
                            }
                            if (xmlProduct.Image4 != null)
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = xmlProduct.Image4;
                                product.ProductImages.Add(productImage);
                                isChanged = true;
                            }
                            if (isChanged)
                            {
                                db.SaveChanges();
                            }
                        }
#endif
                        currentProduct++;
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        else if (bw.WorkerReportsProgress && currentProduct%100 == 0)
                            bw.ReportProgress((int) (100*currentProduct/countProducts));
                    }
                }
            }
            catch (DbEntityValidationException dbEx)
            {
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        Trace.TraceInformation("Property: {0} Error: {1}", validationError.PropertyName,
                                               validationError.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                //message = ex.Message;
                log.Error("Error error logging", ex);
            }
        }
    }
}
