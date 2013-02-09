using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
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


    class ImportLaterooms
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
                log.Error("Error error logging", err);
            }
        }

        public static void DoImport(object sender, DoWorkEventArgs e)               // static bool    (string _URL, out string message)
        {
            //log.Error("This is my test error");

            //bool rc = false;
            //message = String.Empty;
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters) e.Argument;
            string _URL = param.Url;
            int categoryId = param.CategoryId;
            int portalId = param.PortalId;
            int vendorId = param.VendorId;
            string advancedCategoryRoot = param.AdvancedCategoryRoot;
            string countryFilter = param.CountryFilter;
            string cityFilter = param.CityFilter;
            int? stepImport = param.StepImport;
            int? stepAddToCategories = param.StepAddToCategories;
            int? stepAddImages = param.StepAddImages;

            if (!File.Exists(_URL))
            {
                // unzip file to temp folder if needed
                if (_URL.EndsWith(".zip"))
                {
                    string zipFileName = String.Format("{0}\\{1}", Properties.Settings.Default.TempPath,
                                                       "Hotels_Standard.zip");
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
                    bw.ReportProgress(0); // start new step of background process

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
            }

            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add("", "Hotels_Standard.xsd");
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
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(50);

            // read hotels from XML
            // use XmlReader to avoid huge file size dependence
            var xmlProducts =
                from el in StreamRootChildDoc(_URL)
                select new
                           {
                               Country = (string) el.Element("hotel_country"),
                               County = (string) el.Element("hotel_county"),
                               City = (string) el.Element("hotel_city"),
                               ProductNumber = (string) el.Element("hotel_ref"),
                               Name = (string) el.Element("hotel_name"),
                               Images = el.Element("images"),
                               UnitCost = (decimal) el.Element("PricesFrom"),
                               Description = (string) el.Element("hotel_description"),
                               DescriptionHTML = (string) el.Element("alternate_description"),
                               URL = (string) el.Element("hotel_link")
                           };

            if (!String.IsNullOrEmpty(countryFilter))
            {
                xmlProducts = xmlProducts.Where(p => p.Country == countryFilter);
            }

            if (!String.IsNullOrEmpty(cityFilter))
            {
                xmlProducts = xmlProducts.Where(p => p.City == cityFilter);
            }

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(100);
            Thread.Sleep(100); // a little bit slow working for visualisation Progress

            // Prepare dataTable
            DataTable dataTable = new DataTable();
            DataColumn dataColumn = new DataColumn("ProductName", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("CategoryID", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Category2ID", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Category3", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ProductNumber", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost2", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost3", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost4", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost5", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UnitCost6", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Description", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DescriptionHTML", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("URL", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ProductCost", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ProductImage", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("OrderQuant", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("CreatedByUser", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DateCreated", typeof (System.DateTime));
            dataTable.Columns.Add(dataColumn);

            // default columns
            dataColumn = new DataColumn("EAN", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ISBN", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free1", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free2", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free3", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("KeyWords", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Stock", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Weight", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Volume", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Length", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Width", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Height", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("FreightCosts1", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("FreightCosts2", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Featured", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SalePrice", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SaleStart", typeof (System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SaleEnd", typeof (System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DownLoad", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ZIPPassWord", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DownLoadFile", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Archive", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit1", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit2", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit3", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit4", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit5", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RoleID", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SubscriptionPeriod", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RecurringBilling", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("TaxExempt", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ShipExempt", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("InsuredValue", typeof (System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PublicationStart", typeof (System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PublicationEnd", typeof (System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Status", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DonationItem", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PayPalSubscription", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UseRoleFees", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RoleExpiryType", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ItemDeliveryType", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ReorderPoint", typeof (System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("OrderQuantValidExpr", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ShippingAddress", typeof (System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("AuctionFinished", typeof (System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("TaxCode", typeof (System.String));
            dataTable.Columns.Add(dataColumn);

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0); // start new step of background process
            int productCount = xmlProducts.Count();
            try
            {
                int initialStep = 0;
                if (stepImport.HasValue)
                {
                    initialStep = stepImport.Value;
                }
                else if (stepAddToCategories.HasValue)
                {
                    initialStep = stepAddToCategories.Value;
                    goto UpdateAdvCats;
                }
                else if (stepAddImages.HasValue)
                {
                    initialStep = stepAddImages.Value;
                    goto UpdateImages;
                }

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    bool isVendorProductsEmpty = db.Products.Count(p => p.CreatedByUser == vendorId) == 0;

                    int i = 0;
                    foreach (var xmlProduct in xmlProducts)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

                        var xmlProduct1 = xmlProduct;
                        Console.WriteLine(i + " - " + xmlProduct1.Name); // debug print

                        // create new product record
                        int batchLimit = 500;
                        if (isVendorProductsEmpty ||
                            db.Products.SingleOrDefault(
                                p => p.Categories.Any(c => c.Id == categoryId && p.Number == xmlProduct1.ProductNumber && p.CreatedByUser == vendorId)) == null)
                        {
                            DataRow dataRow = dataTable.NewRow();
                            dataRow["Name"] = xmlProduct1.Name;
                            dataRow["Number"] = xmlProduct1.ProductNumber;
                            dataRow["UnitCost"] = xmlProduct1.UnitCost;
                            dataRow["Description"] = xmlProduct1.Description;
                            dataRow["URL"] = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            dataRow["Image"] = (string) xmlProduct1.Images.Element("url");
                            dataRow["CreatedByUser"] = vendorId;
                            dataRow["IsDeleted"] = false;

                            dataTable.Rows.Add(dataRow);

                            if (dataTable.Rows.Count >= batchLimit || i >= productCount - batchLimit)
                            {
                                // Set up the bulk copy object. 
                                // Note that the column positions in the source
                                // data reader match the column positions in 
                                // the destination table so there is no need to
                                // map columns.
                                using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                                {
                                    bulkCopy.BatchSize = 5000;

                                    bulkCopy.DestinationTableName = "dbo.Cowrie_Products";
                                    bulkCopy.ColumnMappings.Add("Name", "Name");
                                    bulkCopy.ColumnMappings.Add("Number", "Number");
                                    bulkCopy.ColumnMappings.Add("Cost", "Cost");
                                    bulkCopy.ColumnMappings.Add("Description", "Description");
                                    bulkCopy.ColumnMappings.Add("URL", "URL");
                                    bulkCopy.ColumnMappings.Add("Image", "Image");
                                    bulkCopy.ColumnMappings.Add("CreatedByUser", "CreatedByUser");
                                    bulkCopy.ColumnMappings.Add("IsDeleted", "IsDeleted");

                                    try
                                    {
                                        // Write from the source to the destination.
                                        bulkCopy.WriteToServer(dataTable);
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("Error error logging", ex);
                                    }
                                    finally
                                    {
                                        // Close the SqlDataReader. The SqlBulkCopy
                                        // object is automatically closed at the end
                                        // of the using block.
                                        //reader.Close();
                                    }
                                }
                                i += dataTable.Rows.Count;
                                dataTable.Rows.Clear();
                                UpdateSteps(stepImport: i);
                            }
                        }
                        else
                        {
                            var product =
                                db.Products.SingleOrDefault(
                                    p => p.Categories.Any(c => c.Id == categoryId) && p.Number == xmlProduct1.ProductNumber && p.CreatedByUser == vendorId);
                            // no need to check for null vallue because of previous if
                            bool isChanged = false;
                            if (product.Name != xmlProduct1.Name)
                            {
                                product.Name = xmlProduct1.Name;
                                isChanged = true;
                            }
                            if (product.Number != xmlProduct1.ProductNumber)
                            {
                                product.Number = xmlProduct1.ProductNumber;
                                isChanged = true;
                            }
                            if (product.UnitCost != xmlProduct1.UnitCost)
                            {
                                product.UnitCost = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.Description != xmlProduct1.Description)
                            {
                                product.Description = xmlProduct1.Description;
                                isChanged = true;
                            }
                            if (product.URL != xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                            {
                                product.URL = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                isChanged = true;
                            }
                            if (product.Image != (string) xmlProduct1.Images.Element("url"))
                            {
                                product.Image = (string) xmlProduct1.Images.Element("url");
                                isChanged = true;
                            }

                            if (isChanged)
                            {
                                db.SaveChanges();
                            }

                            i++;
                            UpdateSteps(stepImport: i);
                        }

                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            goto Cancelled;
                        }
                        else if (bw.WorkerReportsProgress && i % 100 == 0)
                        {
                            bw.ReportProgress((int) (100*i/productCount));
                        }
                    }
                }

                initialStep = 0;

            UpdateAdvCats:
                // Set step for backgroundWorker
                Form1.activeStep = "Update advanced categories..";
                bw.ReportProgress(0); // start new step of background process

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    int i = 0;
                    foreach (var xmlProduct in xmlProducts)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

                        var xmlProduct1 = xmlProduct;
                        Console.WriteLine(i + " - " + xmlProduct1.Name); // debug print

                        // create advanced categories
                        string hotelCity = xmlProduct1.City;
                        if (xmlProduct1.City.Length > 50)
                        {
                            hotelCity = xmlProduct1.City.Substring(0, 47).PadRight(50, '.');
                        }
                        int? parentID = null;
                        int? catRootID = null;
                        Category rootCategory = null;
                        int? catCountryID = null;
                        Category countryCategory = null;
                        int? catCountyID = null;
                        Category countyCategory = null;
                        int? catCityID = null;
                        Category cityCategory = null;
                        int level = 0;
                        int maxOrder = 0;

                        if (!String.IsNullOrEmpty(advancedCategoryRoot))
                        {
                            rootCategory =
                                db.Categories.SingleOrDefault(
                                    c =>
                                    c.PortalId == portalId && c.Name == advancedCategoryRoot &&
                                    c.ParentCategory == null);
                            if (rootCategory == null)
                            {
                                rootCategory = new Category
                                                 {
                                                     PortalId = portalId,
                                                     Name = advancedCategoryRoot,
                                                     IsDeleted = false
                                                 };
                                db.Categories.Add(rootCategory);
                                db.SaveChanges();
                            }
                            parentID = rootCategory.Id;
                            catRootID = rootCategory.Id;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(xmlProduct1.Country))
                        {
                            if (parentID.HasValue)
                            {
                                countryCategory =
                                    db.Categories.SingleOrDefault(
                                        c =>
                                        c.PortalId == portalId && c.Name == xmlProduct1.Country &&
                                        c.ParentId == parentID.Value);
                            }
                            else
                            {
                                countryCategory =
                                    db.Categories.SingleOrDefault(
                                        c =>
                                        c.PortalId == portalId && c.Name == xmlProduct1.Country &&
                                        c.ParentId == null);
                            }
                            if (countryCategory == null)
                            {
                                countryCategory = new Category
                                                    {
                                                        PortalId = portalId,
                                                        Name = xmlProduct1.Country,
                                                        IsDeleted = false
                                                    };
                                if (parentID.HasValue)
                                {
                                    countryCategory.ParentId = parentID.Value;
                                }
                                db.Categories.Add(countryCategory);
                                db.SaveChanges();
                            }
                            parentID = countryCategory.Id;
                            catCountryID = countryCategory.Id;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(xmlProduct1.County))
                        {
                            countyCategory =
                                db.Categories.SingleOrDefault(
                                    c =>
                                    c.PortalId == portalId && c.Name == xmlProduct1.County &&
                                    c.ParentId == parentID);
                            if (countyCategory == null)
                            {
                                countyCategory = new Category
                                                   {
                                                       PortalId = portalId,
                                                       Name = xmlProduct1.County,
                                                       ParentId = parentID.Value,
                                                       IsDeleted = false
                                                   };
                                db.Categories.Add(countyCategory);
                                db.SaveChanges();
                            }
                            parentID = countyCategory.Id;
                            catCountyID = countyCategory.Id;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(xmlProduct1.City))
                        {
                            cityCategory =
                                db.Categories.SingleOrDefault(
                                    c =>
                                    c.PortalId == portalId && c.Name == hotelCity &&
                                    c.ParentId == parentID);
                            if (cityCategory == null)
                            {
                                cityCategory = new Category
                                                 {
                                                     PortalId = portalId,
                                                     Name = hotelCity,
                                                     ParentId = parentID.Value,
                                                     IsDeleted = false
                                                 };
                                db.Categories.Add(cityCategory);
                                db.SaveChanges();
                            }
                            parentID = cityCategory.Id;
                            catCityID = cityCategory.Id;
                            level++;
                        }

                        // add product to advanced categories
                        var tempProduct =
                            db.Products.SingleOrDefault(
                                p =>
                                p.Categories.Any(c => c.Id == categoryId) && p.Number == xmlProduct1.ProductNumber &&
                                p.CreatedByUser == vendorId);
                        if (tempProduct == null)
                        {
                            continue;
                        }
                        var productId = tempProduct.Id;
                        if (rootCategory != null)
                        {
                            if (!tempProduct.Categories.Any(c => c == rootCategory))
                            {
                                tempProduct.Categories.Add(rootCategory);
                            }
                        }
                        if (countyCategory != null)
                        {
                            if (!tempProduct.Categories.Any(c => c == countyCategory))
                            {
                                tempProduct.Categories.Add(countyCategory);
                            }
                        }
                        if (countyCategory != null)
                        {
                            if (!tempProduct.Categories.Any(c => c == countyCategory))
                            {
                                tempProduct.Categories.Add(countyCategory);
                            }
                        }
                        if (cityCategory != null)
                        {
                            if (!tempProduct.Categories.Any(c => c == cityCategory))
                            {
                                tempProduct.Categories.Add(rootCategory);
                            }
                        }
                        db.SaveChanges();

                        i++;
                        UpdateSteps(stepAddToCategories: i);

                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            goto Cancelled;
                        }
                        else if (bw.WorkerReportsProgress && i % 100 == 0)
                        {
                            bw.ReportProgress((int) (100*i/productCount));
                        }
                    }
                }

                initialStep = 0;

            UpdateImages:
                // Set step for backgroundWorker
                Form1.activeStep = "Update images..";
                bw.ReportProgress(0); // start new step of background process

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    int i = 0;
                    foreach (var xmlProduct in xmlProducts)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

                        var xmlProduct1 = xmlProduct;
                        Console.WriteLine(i + " - " + xmlProduct1.Name);

                        var tempProduct =
                            db.Products.SingleOrDefault(
                                p =>
                                p.Categories.Any(c => c.Id == categoryId) && p.Number == xmlProduct1.ProductNumber &&
                                p.CreatedByUser == vendorId);
                        if (tempProduct == null)
                        {
                            i++;
                            continue;
                        }
                        var productId = tempProduct.Id;

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("DELETE");
                        sb.AppendLine("FROM         ProductImages");
                        sb.AppendLine("WHERE     ProductID = @ProductID");
                        SqlCommand commandDelete =
                            new SqlCommand(
                                sb.ToString(),
                                destinationConnection);
                        commandDelete.Parameters.Add("@ProductID", SqlDbType.Int);
                        commandDelete.Parameters["@ProductID"].Value = productId;
                        commandDelete.ExecuteNonQuery();

                        foreach (var image in xmlProduct1.Images.Elements("url"))
                        {
                            if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = image.Value;
                                tempProduct.ProductImages.Add(productImage);
                            }
                        }

                        initialStep++;
                        UpdateSteps(stepAddImages: i);

                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            goto Cancelled;
                        }
                        else if (bw.WorkerReportsProgress && i % 100 == 0)
                        {
                            bw.ReportProgress((int) (100*i/productCount));
                        }
                    }
                }
                if (!e.Cancel)
                {
                    UpdateSteps();
                }
            Cancelled:
                int q = 0;
            }
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                log.Error("Error error logging", ex);
            }
        }

        private static void UpdateSteps(int? stepImport = null, int? stepAddToCategories = null, int? stepAddImages = null)
        {
            using (var context = new ImportProductsEntities())
            {
                Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                feed.StepImport = stepImport;
                feed.StepAddToCategories = stepAddToCategories;
                feed.StepAddImages = stepAddImages;
                context.SaveChanges();
            }
        }
    }
}
