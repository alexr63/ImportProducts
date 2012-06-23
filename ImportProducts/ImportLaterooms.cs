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

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(50);

            // read hotels from XML
            // use XmlReader to avoid huge file size dependence
            var products =
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
                products = products.Where(p => p.Country == countryFilter);
            }

            if (!String.IsNullOrEmpty(cityFilter))
            {
                products = products.Where(p => p.City == cityFilter);
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
            dataColumn = new DataColumn("ISBN", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free1", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free2", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Free3", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("KeyWords", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Stock", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Weight", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Volume", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Length", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Width", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Height", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("FreightCosts1", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("FreightCosts2", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Featured", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SalePrice", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SaleStart", typeof(System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SaleEnd", typeof(System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DownLoad", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ZIPPassWord", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DownLoadFile", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Archive", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit1", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit2", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit3", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit4", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("BulkPriceLimit5", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RoleID", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("SubscriptionPeriod", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RecurringBilling", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("TaxExempt", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ShipExempt", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("InsuredValue", typeof(System.Decimal));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PublicationStart", typeof(System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PublicationEnd", typeof(System.DateTime));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("Status", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("DonationItem", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("PayPalSubscription", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("UseRoleFees", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("RoleExpiryType", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ItemDeliveryType", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ReorderPoint", typeof(System.Int32));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("OrderQuantValidExpr", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("ShippingAddress", typeof(System.String));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("AuctionFinished", typeof(System.Boolean));
            dataTable.Columns.Add(dataColumn);
            dataColumn = new DataColumn("TaxCode", typeof(System.String));
            dataTable.Columns.Add(dataColumn);

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0); // start new step of background process
            int countProducts = products.Count();
            try
            {
                int currentProduct = 0;
                int updatedRows = 0;
                if (stepImport.HasValue)
                {
                    currentProduct = stepImport.Value;
                }
                else if (stepAddToCategories.HasValue)
                {
                    currentProduct = stepAddToCategories.Value;
                    goto UpdateAdvCats;
                }
                else if (stepAddImages.HasValue)
                {
                    currentProduct = stepAddImages.Value;
                    goto UpdateImages;
                }

                int currentStep = 0;
                foreach (var product in products)
                {
                    if (currentStep++ < currentProduct)
                    {
                        currentProduct++;
                        continue;
                    }

                    using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                    {
                        bool isVendorProductsEmpty = db.Products.Count(p => p.CreatedByUser == vendorId) == 0;

                        Console.WriteLine(currentProduct.ToString() + " - " + product.Name); // debug print

                        // create new product record
                        int batchLimit = 500;
                        if (isVendorProductsEmpty)
                        {
                            DataRow dataRow = dataTable.NewRow();
                            dataRow["CategoryID"] = categoryId;
                            dataRow["Category2ID"] = 0;
                            dataRow["Category3"] = String.Empty;
                            dataRow["ProductName"] = product.Name;
                            dataRow["ProductNumber"] = product.ProductNumber;
                            dataRow["UnitCost"] = product.UnitCost;
                            dataRow["UnitCost2"] = product.UnitCost;
                            dataRow["UnitCost3"] = product.UnitCost;
                            dataRow["UnitCost4"] = product.UnitCost;
                            dataRow["UnitCost5"] = product.UnitCost;
                            dataRow["UnitCost6"] = product.UnitCost;
                            dataRow["Description"] = product.Description;
                            dataRow["DescriptionHTML"] = product.DescriptionHTML;
                            dataRow["URL"] = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            dataRow["ProductCost"] = product.UnitCost;
                            dataRow["ProductImage"] = (string) product.Images.Element("url");
                            dataRow["OrderQuant"] = "0";
                            dataRow["CreatedByUser"] = vendorId;
                            dataRow["DateCreated"] = DateTime.Now;

                            // default columns
                            dataRow["EAN"] = "";
                            dataRow["ISBN"] = "";
                            dataRow["Free1"] = "";
                            dataRow["Free2"] = "";
                            dataRow["Free3"] = "";
                            dataRow["KeyWords"] = "";
                            dataRow["Stock"] = 0;
                            dataRow["Weight"] = 0.0000m;
                            dataRow["Volume"] = 0.00m;
                            dataRow["Length"] = 0.00m;
                            dataRow["Width"] = 0.00m;
                            dataRow["Height"] = 0.00m;
                            dataRow["FreightCosts1"] = 0.0000m;
                            dataRow["FreightCosts2"] = 0.0000m;
                            dataRow["Featured"] = false;
                            dataRow["SalePrice"] = 0.0000m;
                            dataRow["SaleStart"] = new DateTime(646602048000000000, DateTimeKind.Unspecified);
                            dataRow["SaleEnd"] = new DateTime(599266080000000000, DateTimeKind.Unspecified);
                            dataRow["DownLoad"] = false;
                            dataRow["ZIPPassWord"] = "";
                            dataRow["DownLoadFile"] = "";
                            dataRow["Archive"] = false;
                            dataRow["BulkPriceLimit1"] = 0;
                            dataRow["BulkPriceLimit2"] = 0;
                            dataRow["BulkPriceLimit3"] = 0;
                            dataRow["BulkPriceLimit4"] = 0;
                            dataRow["BulkPriceLimit5"] = 0;
                            dataRow["RoleID"] = -1;
                            dataRow["SubscriptionPeriod"] = 0;
                            dataRow["RecurringBilling"] = false;
                            dataRow["TaxExempt"] = false;
                            dataRow["ShipExempt"] = false;
                            dataRow["InsuredValue"] = 0.0000m;
                            dataRow["PublicationStart"] = new DateTime(634538880000000000, DateTimeKind.Unspecified);
                            dataRow["PublicationEnd"] = new DateTime(650318976000000000, DateTimeKind.Unspecified);
                            dataRow["Status"] = "0";
                            dataRow["DonationItem"] = false;
                            dataRow["PayPalSubscription"] = false;
                            dataRow["UseRoleFees"] = false;
                            dataRow["RoleExpiryType"] = "0";
                            dataRow["ItemDeliveryType"] = "0";
                            dataRow["ReorderPoint"] = 0;
                            dataRow["OrderQuantValidExpr"] = "";
                            dataRow["ShippingAddress"] = "0";
                            dataRow["AuctionFinished"] = false;
                            dataRow["TaxCode"] = "";

                            dataTable.Rows.Add(dataRow);

                            if (dataTable.Rows.Count >= batchLimit)
                            {
                                using (
                                    SqlConnection destinationConnection =
                                        new SqlConnection(db.Database.Connection.ConnectionString))
                                {
                                    destinationConnection.Open();

                                    // Set up the bulk copy object. 
                                    // Note that the column positions in the source
                                    // data reader match the column positions in 
                                    // the destination table so there is no need to
                                    // map columns.
                                    using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                                    {
                                        bulkCopy.BatchSize = 5000;

                                        bulkCopy.DestinationTableName = "dbo.CAT_Products";
                                        bulkCopy.ColumnMappings.Add("CategoryID", "CategoryID");
                                        bulkCopy.ColumnMappings.Add("Category2ID", "Category2ID");
                                        bulkCopy.ColumnMappings.Add("Category3", "Category3");
                                        bulkCopy.ColumnMappings.Add("ProductName", "ProductName");
                                        bulkCopy.ColumnMappings.Add("ProductNumber", "ProductNumber");
                                        bulkCopy.ColumnMappings.Add("UnitCost", "UnitCost");
                                        bulkCopy.ColumnMappings.Add("UnitCost2", "UnitCost2");
                                        bulkCopy.ColumnMappings.Add("UnitCost3", "UnitCost3");
                                        bulkCopy.ColumnMappings.Add("UnitCost4", "UnitCost4");
                                        bulkCopy.ColumnMappings.Add("UnitCost5", "UnitCost5");
                                        bulkCopy.ColumnMappings.Add("UnitCost6", "UnitCost6");
                                        bulkCopy.ColumnMappings.Add("Description", "Description");
                                        bulkCopy.ColumnMappings.Add("DescriptionHTML", "DescriptionHTML");
                                        bulkCopy.ColumnMappings.Add("URL", "URL");
                                        bulkCopy.ColumnMappings.Add("ProductCost", "ProductCost");
                                        bulkCopy.ColumnMappings.Add("ProductImage", "ProductImage");
                                        bulkCopy.ColumnMappings.Add("OrderQuant", "OrderQuant");
                                        bulkCopy.ColumnMappings.Add("CreatedByUser", "CreatedByUser");
                                        bulkCopy.ColumnMappings.Add("DateCreated", "DateCreated");

                                        // default columns
                                        bulkCopy.ColumnMappings.Add("EAN", "EAN");
                                        bulkCopy.ColumnMappings.Add("ISBN", "ISBN");
                                        bulkCopy.ColumnMappings.Add("Free1", "Free1");
                                        bulkCopy.ColumnMappings.Add("Free2", "Free2");
                                        bulkCopy.ColumnMappings.Add("Free3", "Free3");
                                        bulkCopy.ColumnMappings.Add("KeyWords", "KeyWords");
                                        bulkCopy.ColumnMappings.Add("Stock", "Stock");
                                        bulkCopy.ColumnMappings.Add("Weight", "Weight");
                                        bulkCopy.ColumnMappings.Add("Volume", "Volume");
                                        bulkCopy.ColumnMappings.Add("Length", "Length");
                                        bulkCopy.ColumnMappings.Add("Width", "Width");
                                        bulkCopy.ColumnMappings.Add("Height", "Height");
                                        bulkCopy.ColumnMappings.Add("FreightCosts1", "FreightCosts1");
                                        bulkCopy.ColumnMappings.Add("FreightCosts2", "FreightCosts2");
                                        bulkCopy.ColumnMappings.Add("Featured", "Featured");
                                        bulkCopy.ColumnMappings.Add("SalePrice", "SalePrice");
                                        bulkCopy.ColumnMappings.Add("SaleStart", "SaleStart");
                                        bulkCopy.ColumnMappings.Add("SaleEnd", "SaleEnd");
                                        bulkCopy.ColumnMappings.Add("DownLoad", "DownLoad");
                                        bulkCopy.ColumnMappings.Add("ZIPPassWord", "ZIPPassWord");
                                        bulkCopy.ColumnMappings.Add("DownLoadFile", "DownLoadFile");
                                        bulkCopy.ColumnMappings.Add("Archive", "Archive");
                                        bulkCopy.ColumnMappings.Add("BulkPriceLimit1", "BulkPriceLimit1");
                                        bulkCopy.ColumnMappings.Add("BulkPriceLimit2", "BulkPriceLimit2");
                                        bulkCopy.ColumnMappings.Add("BulkPriceLimit3", "BulkPriceLimit3");
                                        bulkCopy.ColumnMappings.Add("BulkPriceLimit4", "BulkPriceLimit4");
                                        bulkCopy.ColumnMappings.Add("BulkPriceLimit5", "BulkPriceLimit5");
                                        bulkCopy.ColumnMappings.Add("RoleID", "RoleID");
                                        bulkCopy.ColumnMappings.Add("SubscriptionPeriod", "SubscriptionPeriod");
                                        bulkCopy.ColumnMappings.Add("RecurringBilling", "RecurringBilling");
                                        bulkCopy.ColumnMappings.Add("TaxExempt", "TaxExempt");
                                        bulkCopy.ColumnMappings.Add("ShipExempt", "ShipExempt");
                                        bulkCopy.ColumnMappings.Add("InsuredValue", "InsuredValue");
                                        bulkCopy.ColumnMappings.Add("PublicationStart", "PublicationStart");
                                        bulkCopy.ColumnMappings.Add("PublicationEnd", "PublicationEnd");
                                        bulkCopy.ColumnMappings.Add("Status", "Status");
                                        bulkCopy.ColumnMappings.Add("DonationItem", "DonationItem");
                                        bulkCopy.ColumnMappings.Add("PayPalSubscription", "PayPalSubscription");
                                        bulkCopy.ColumnMappings.Add("UseRoleFees", "UseRoleFees");
                                        bulkCopy.ColumnMappings.Add("RoleExpiryType", "RoleExpiryType");
                                        bulkCopy.ColumnMappings.Add("ItemDeliveryType", "ItemDeliveryType");
                                        bulkCopy.ColumnMappings.Add("ReorderPoint", "ReorderPoint");
                                        bulkCopy.ColumnMappings.Add("OrderQuantValidExpr", "OrderQuantValidExpr");
                                        bulkCopy.ColumnMappings.Add("ShippingAddress", "ShippingAddress");
                                        bulkCopy.ColumnMappings.Add("AuctionFinished", "AuctionFinished");
                                        bulkCopy.ColumnMappings.Add("TaxCode", "TaxCode");

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
                                }
                                dataTable.Rows.Clear();
                                updatedRows = +batchLimit;

                                using (var context = new ImportProductsEntities())
                                {
                                    Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                                    feed.StepImport = currentProduct;
                                    feed.StepAddToCategories = null;
                                    feed.StepAddImages = null;
                                    context.SaveChanges();
                                }
                            }
                        }
                        else
                        {
                            var product2 =
                                db.Products.SingleOrDefault(
                                    p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber);
                            if (product2 == null)
                            {
                                product2 = new Product
                                               {
                                                   CategoryID = categoryId,
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
                                                   URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' '),
                                                   ProductCost = product.UnitCost,
                                                   CreatedByUser = vendorId,
                                                   DateCreated = DateTime.Now
                                               };

                                product2.ProductImage = (string) product.Images.Element("url");
                                // trick to hide Add To Cart button
                                product2.OrderQuant = "0";

#if ADDIMAGES
    // add additional product images
                            foreach (var image in product.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    ProductImage productImage = new ProductImage();
                                    productImage.ImageFile = image.Value;
                                    product2.ProductImages.Add(productImage);
                                }
                            }
#endif

                                // add product to product set
                                //db.Products.Add(product2);
                                // store  changes
                                //SaveChanges7(db);

                                DataRow dataRow = dataTable.NewRow();
                                dataRow["CategoryID"] = categoryId;
                                dataRow["Category2ID"] = 0;
                                dataRow["Category3"] = String.Empty;
                                dataRow["ProductName"] = product.Name;
                                dataRow["ProductNumber"] = product.ProductNumber;
                                dataRow["UnitCost"] = product.UnitCost;
                                dataRow["UnitCost2"] = product.UnitCost;
                                dataRow["UnitCost3"] = product.UnitCost;
                                dataRow["UnitCost4"] = product.UnitCost;
                                dataRow["UnitCost5"] = product.UnitCost;
                                dataRow["UnitCost6"] = product.UnitCost;
                                dataRow["Description"] = product.Description;
                                dataRow["DescriptionHTML"] = product.DescriptionHTML;
                                dataRow["URL"] = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                dataRow["ProductCost"] = product.UnitCost;
                                dataRow["ProductImage"] = (string) product.Images.Element("url");
                                dataRow["OrderQuant"] = "0";
                                dataRow["CreatedByUser"] = vendorId;
                                dataRow["DateCreated"] = DateTime.Now;

                                // default columns
                                dataRow["EAN"] = "";
                                dataRow["ISBN"] = "";
                                dataRow["Free1"] = "";
                                dataRow["Free2"] = "";
                                dataRow["Free3"] = "";
                                dataRow["KeyWords"] = "";
                                dataRow["Stock"] = 0;
                                dataRow["Weight"] = 0.0000m;
                                dataRow["Volume"] = 0.00m;
                                dataRow["Length"] = 0.00m;
                                dataRow["Width"] = 0.00m;
                                dataRow["Height"] = 0.00m;
                                dataRow["FreightCosts1"] = 0.0000m;
                                dataRow["FreightCosts2"] = 0.0000m;
                                dataRow["Featured"] = false;
                                dataRow["SalePrice"] = 0.0000m;
                                dataRow["SaleStart"] = new DateTime(646602048000000000, DateTimeKind.Unspecified);
                                dataRow["SaleEnd"] = new DateTime(599266080000000000, DateTimeKind.Unspecified);
                                dataRow["DownLoad"] = false;
                                dataRow["ZIPPassWord"] = "";
                                dataRow["DownLoadFile"] = "";
                                dataRow["Archive"] = false;
                                dataRow["BulkPriceLimit1"] = 0;
                                dataRow["BulkPriceLimit2"] = 0;
                                dataRow["BulkPriceLimit3"] = 0;
                                dataRow["BulkPriceLimit4"] = 0;
                                dataRow["BulkPriceLimit5"] = 0;
                                dataRow["RoleID"] = -1;
                                dataRow["SubscriptionPeriod"] = 0;
                                dataRow["RecurringBilling"] = false;
                                dataRow["TaxExempt"] = false;
                                dataRow["ShipExempt"] = false;
                                dataRow["InsuredValue"] = 0.0000m;
                                dataRow["PublicationStart"] = new DateTime(634538880000000000, DateTimeKind.Unspecified);
                                dataRow["PublicationEnd"] = new DateTime(650318976000000000, DateTimeKind.Unspecified);
                                dataRow["Status"] = "0";
                                dataRow["DonationItem"] = false;
                                dataRow["PayPalSubscription"] = false;
                                dataRow["UseRoleFees"] = false;
                                dataRow["RoleExpiryType"] = "0";
                                dataRow["ItemDeliveryType"] = "0";
                                dataRow["ReorderPoint"] = 0;
                                dataRow["OrderQuantValidExpr"] = "";
                                dataRow["ShippingAddress"] = "0";
                                dataRow["AuctionFinished"] = false;
                                dataRow["TaxCode"] = "";

                                dataTable.Rows.Add(dataRow);

                                if (dataTable.Rows.Count >= batchLimit)
                                {
                                    using (
                                        SqlConnection destinationConnection =
                                            new SqlConnection(db.Database.Connection.ConnectionString))
                                    {
                                        destinationConnection.Open();

                                        // Set up the bulk copy object. 
                                        // Note that the column positions in the source
                                        // data reader match the column positions in 
                                        // the destination table so there is no need to
                                        // map columns.
                                        using (SqlBulkCopy bulkCopy = new SqlBulkCopy(destinationConnection))
                                        {
                                            bulkCopy.BatchSize = 5000;

                                            bulkCopy.DestinationTableName = "dbo.CAT_Products";
                                            bulkCopy.ColumnMappings.Add("CategoryID", "CategoryID");
                                            bulkCopy.ColumnMappings.Add("Category2ID", "Category2ID");
                                            bulkCopy.ColumnMappings.Add("Category3", "Category3");
                                            bulkCopy.ColumnMappings.Add("ProductName", "ProductName");
                                            bulkCopy.ColumnMappings.Add("ProductNumber", "ProductNumber");
                                            bulkCopy.ColumnMappings.Add("UnitCost", "UnitCost");
                                            bulkCopy.ColumnMappings.Add("UnitCost2", "UnitCost2");
                                            bulkCopy.ColumnMappings.Add("UnitCost3", "UnitCost3");
                                            bulkCopy.ColumnMappings.Add("UnitCost4", "UnitCost4");
                                            bulkCopy.ColumnMappings.Add("UnitCost5", "UnitCost5");
                                            bulkCopy.ColumnMappings.Add("UnitCost6", "UnitCost6");
                                            bulkCopy.ColumnMappings.Add("Description", "Description");
                                            bulkCopy.ColumnMappings.Add("DescriptionHTML", "DescriptionHTML");
                                            bulkCopy.ColumnMappings.Add("URL", "URL");
                                            bulkCopy.ColumnMappings.Add("ProductCost", "ProductCost");
                                            bulkCopy.ColumnMappings.Add("ProductImage", "ProductImage");
                                            bulkCopy.ColumnMappings.Add("OrderQuant", "OrderQuant");
                                            bulkCopy.ColumnMappings.Add("CreatedByUser", "CreatedByUser");
                                            bulkCopy.ColumnMappings.Add("DateCreated", "DateCreated");

                                            // default columns
                                            bulkCopy.ColumnMappings.Add("EAN", "EAN");
                                            bulkCopy.ColumnMappings.Add("ISBN", "ISBN");
                                            bulkCopy.ColumnMappings.Add("Free1", "Free1");
                                            bulkCopy.ColumnMappings.Add("Free2", "Free2");
                                            bulkCopy.ColumnMappings.Add("Free3", "Free3");
                                            bulkCopy.ColumnMappings.Add("KeyWords", "KeyWords");
                                            bulkCopy.ColumnMappings.Add("Stock", "Stock");
                                            bulkCopy.ColumnMappings.Add("Weight", "Weight");
                                            bulkCopy.ColumnMappings.Add("Volume", "Volume");
                                            bulkCopy.ColumnMappings.Add("Length", "Length");
                                            bulkCopy.ColumnMappings.Add("Width", "Width");
                                            bulkCopy.ColumnMappings.Add("Height", "Height");
                                            bulkCopy.ColumnMappings.Add("FreightCosts1", "FreightCosts1");
                                            bulkCopy.ColumnMappings.Add("FreightCosts2", "FreightCosts2");
                                            bulkCopy.ColumnMappings.Add("Featured", "Featured");
                                            bulkCopy.ColumnMappings.Add("SalePrice", "SalePrice");
                                            bulkCopy.ColumnMappings.Add("SaleStart", "SaleStart");
                                            bulkCopy.ColumnMappings.Add("SaleEnd", "SaleEnd");
                                            bulkCopy.ColumnMappings.Add("DownLoad", "DownLoad");
                                            bulkCopy.ColumnMappings.Add("ZIPPassWord", "ZIPPassWord");
                                            bulkCopy.ColumnMappings.Add("DownLoadFile", "DownLoadFile");
                                            bulkCopy.ColumnMappings.Add("Archive", "Archive");
                                            bulkCopy.ColumnMappings.Add("BulkPriceLimit1", "BulkPriceLimit1");
                                            bulkCopy.ColumnMappings.Add("BulkPriceLimit2", "BulkPriceLimit2");
                                            bulkCopy.ColumnMappings.Add("BulkPriceLimit3", "BulkPriceLimit3");
                                            bulkCopy.ColumnMappings.Add("BulkPriceLimit4", "BulkPriceLimit4");
                                            bulkCopy.ColumnMappings.Add("BulkPriceLimit5", "BulkPriceLimit5");
                                            bulkCopy.ColumnMappings.Add("RoleID", "RoleID");
                                            bulkCopy.ColumnMappings.Add("SubscriptionPeriod", "SubscriptionPeriod");
                                            bulkCopy.ColumnMappings.Add("RecurringBilling", "RecurringBilling");
                                            bulkCopy.ColumnMappings.Add("TaxExempt", "TaxExempt");
                                            bulkCopy.ColumnMappings.Add("ShipExempt", "ShipExempt");
                                            bulkCopy.ColumnMappings.Add("InsuredValue", "InsuredValue");
                                            bulkCopy.ColumnMappings.Add("PublicationStart", "PublicationStart");
                                            bulkCopy.ColumnMappings.Add("PublicationEnd", "PublicationEnd");
                                            bulkCopy.ColumnMappings.Add("Status", "Status");
                                            bulkCopy.ColumnMappings.Add("DonationItem", "DonationItem");
                                            bulkCopy.ColumnMappings.Add("PayPalSubscription", "PayPalSubscription");
                                            bulkCopy.ColumnMappings.Add("UseRoleFees", "UseRoleFees");
                                            bulkCopy.ColumnMappings.Add("RoleExpiryType", "RoleExpiryType");
                                            bulkCopy.ColumnMappings.Add("ItemDeliveryType", "ItemDeliveryType");
                                            bulkCopy.ColumnMappings.Add("ReorderPoint", "ReorderPoint");
                                            bulkCopy.ColumnMappings.Add("OrderQuantValidExpr", "OrderQuantValidExpr");
                                            bulkCopy.ColumnMappings.Add("ShippingAddress", "ShippingAddress");
                                            bulkCopy.ColumnMappings.Add("AuctionFinished", "AuctionFinished");
                                            bulkCopy.ColumnMappings.Add("TaxCode", "TaxCode");

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
                                    }
                                    dataTable.Rows.Clear();
                                    updatedRows += batchLimit;

                                    using (var context = new ImportProductsEntities())
                                    {
                                        Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                                        feed.StepImport = currentProduct;
                                        feed.StepAddToCategories = null;
                                        feed.StepAddImages = null;
                                        context.SaveChanges();
                                    }
                                }
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
                                if (product2.Category3 != String.Empty)
                                {
                                    product2.Category3 = String.Empty;
                                    isChanged = true;
                                }
                                if (product2.ProductName != product.Name)
                                {
                                    product2.ProductName = product.Name;
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
                                if (product2.URL != product.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                                {
                                    product2.URL = product.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                    isChanged = true;
                                }
                                if (product2.ProductCost != product.UnitCost)
                                {
                                    product2.ProductCost = product.UnitCost;
                                    isChanged = true;
                                }
                                if (product2.ProductImage != (string) product.Images.Element("url"))
                                {
                                    product2.ProductImage = (string) product.Images.Element("url");
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

                                updatedRows++;
                                if (updatedRows >= batchLimit)
                                {
                                    using (var context = new ImportProductsEntities())
                                    {
                                        Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                                        feed.StepImport = currentProduct;
                                        feed.StepAddToCategories = null;
                                        feed.StepAddImages = null;
                                        context.SaveChanges();
                                    }
                                }

#if ADDIMAGES
                            foreach (var productImage in product2.ProductImages)
                            {
                                if (!product1.Images.Elements("url").Any(x => x.Value == productImage.ImageFile))
                                {
                                    productImage.ImageFile = String.Empty;
                                }
                            }
                            var oldImages = product2.ProductImages.Where(pi => pi.ImageFile == String.Empty).ToList();
                            foreach (var oldImage in oldImages)
                            {
                                db.ProductImages.Remove(oldImage);
                            }
                            foreach (var image in product1.Images.Elements("url"))
                            {
                                if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                                {
                                    if (!product2.ProductImages.Any(pi => pi.ImageFile == image.Value))
                                    {
                                        ProductImage productImage = new ProductImage();
                                        productImage.ImageFile = image.Value;
                                        product2.ProductImages.Add(productImage);
                                    }
                                }
                            }

                            SaveChanges81(db);
#endif
                            }
                        }

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

                currentProduct = 0;

#if !ADVCATS
UpdateAdvCats:
                // Set step for backgroundWorker
                Form1.activeStep = "Update advanced categories..";
                bw.ReportProgress(0); // start new step of background process

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    currentStep = 0;
                    foreach (var product in products)
                    {
                        if (currentStep++ < currentProduct)
                        {
                            continue;
                        }

                        Console.WriteLine(currentProduct.ToString() + " - " + product.Name); // debug print

                        // create advanced categories
                        var product1 = product;
                        string hotelCity = product1.City;
                        if (product1.City.Length > 50)
                        {
                            hotelCity = product1.City.Substring(0, 47).PadRight(50, '.');
                        }
                        int? parentID = null;
                        int? catRootID = null;
                        int? catCountryID = null;
                        int? catCountyID = null;
                        int? catCityID = null;
                        int level = 0;
                        int maxOrder = 0;

                        if (!String.IsNullOrEmpty(advancedCategoryRoot))
                        {
                            var advCatRoot =
                                db.AdvCats.SingleOrDefault(
                                    ac =>
                                    ac.PortalID == portalId && ac.AdvCatName == advancedCategoryRoot &&
                                    ac.Level == level);
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

                        if (!String.IsNullOrEmpty(product1.Country))
                        {
                            AdvCat advCatCountry;
                            if (parentID.HasValue)
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product1.Country &&
                                        ac.Level == level && ac.ParentId == parentID.Value);
                            }
                            else
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == product1.Country &&
                                        ac.Level == level);
                            }
                            if (advCatCountry == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCountry = new AdvCat
                                                    {
                                                        AdvCatOrder = maxOrder + 1,
                                                        PortalID = portalId,
                                                        AdvCatName = product.Country,
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
                                if (parentID.HasValue)
                                {
                                    advCatCountry.ParentId = parentID.Value;
                                }
                                db.AdvCats.Add(advCatCountry);
                                db.SaveChanges();

                                Common.AddAdvCatDefaultPermissions(db, advCatCountry.AdvCatID);
                            }
                            parentID = advCatCountry.AdvCatID;
                            catCountryID = advCatCountry.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product1.County))
                        {
                            var advCatCounty =
                                db.AdvCats.SingleOrDefault(
                                    ac =>
                                    ac.PortalID == portalId && ac.AdvCatName == product1.County && ac.Level == level);
                            if (advCatCounty == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCounty = new AdvCat
                                                   {
                                                       AdvCatOrder = maxOrder + 1,
                                                       PortalID = portalId,
                                                       AdvCatName = product.County,
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

                                Common.AddAdvCatDefaultPermissions(db, advCatCounty.AdvCatID);
                            }
                            parentID = advCatCounty.AdvCatID;
                            catCountyID = advCatCounty.AdvCatID;
                            level++;
                        }

                        if (!String.IsNullOrEmpty(product1.City))
                        {
                            var advCatCity =
                                db.AdvCats.SingleOrDefault(
                                    ac => ac.PortalID == portalId && ac.AdvCatName == hotelCity && ac.Level == level);
                            if (advCatCity == null)
                            {
                                if (db.AdvCats.Count() > 0)
                                {
                                    maxOrder = db.AdvCats.Max(ac => ac.AdvCatOrder);
                                }
                                advCatCity = new AdvCat
                                                 {
                                                     AdvCatOrder = maxOrder + 1,
                                                     PortalID = portalId,
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

                                Common.AddAdvCatDefaultPermissions(db, advCatCity.AdvCatID);
                            }
                            parentID = advCatCity.AdvCatID;
                            catCityID = advCatCity.AdvCatID;
                            level++;
                        }

                        // add product to advanced categories
                        var tempProduct =
                            db.Products.SingleOrDefault(
                                p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber);
                        if (tempProduct == null)
                        {
                            continue;
                        }
                        var productId = tempProduct.ProductID;
                        if (catRootID.HasValue)
                        {
                            SqlCommand commandAdd = new SqlCommand("exec CAT_AddAdvCatProduct @AdvCatID, @ProductID",
                                                                   destinationConnection);
                            commandAdd.Parameters.Add("@AdvCatID", SqlDbType.Int);
                            commandAdd.Parameters["@AdvCatID"].Value = catRootID.Value;
                            commandAdd.Parameters.Add("@ProductID", SqlDbType.Int);
                            commandAdd.Parameters["@ProductID"].Value = productId;
                            commandAdd.ExecuteNonQuery();
                        }

                        if (catCountryID.HasValue)
                        {
                            SqlCommand commandAdd = new SqlCommand("exec CAT_AddAdvCatProduct @AdvCatID, @ProductID",
                                                                   destinationConnection);
                            commandAdd.Parameters.Add("@AdvCatID", SqlDbType.Int);
                            commandAdd.Parameters["@AdvCatID"].Value = catCountryID.Value;
                            commandAdd.Parameters.Add("@ProductID", SqlDbType.Int);
                            commandAdd.Parameters["@ProductID"].Value = productId;
                            commandAdd.ExecuteNonQuery();
                        }
                        if (catCountyID.HasValue)
                        {
                            SqlCommand commandAdd = new SqlCommand("exec CAT_AddAdvCatProduct @AdvCatID, @ProductID",
                                                                   destinationConnection);
                            commandAdd.Parameters.Add("@AdvCatID", SqlDbType.Int);
                            commandAdd.Parameters["@AdvCatID"].Value = catCountyID.Value;
                            commandAdd.Parameters.Add("@ProductID", SqlDbType.Int);
                            commandAdd.Parameters["@ProductID"].Value = productId;
                            commandAdd.ExecuteNonQuery();
                        }
                        if (catCityID.HasValue)
                        {
                            SqlCommand commandAdd = new SqlCommand("exec CAT_AddAdvCatProduct @AdvCatID, @ProductID",
                                                                   destinationConnection);
                            commandAdd.Parameters.Add("@AdvCatID", SqlDbType.Int);
                            commandAdd.Parameters["@AdvCatID"].Value = catCityID.Value;
                            commandAdd.Parameters.Add("@ProductID", SqlDbType.Int);
                            commandAdd.Parameters["@ProductID"].Value = productId;
                            commandAdd.ExecuteNonQuery();
                        }

                        using (var context = new ImportProductsEntities())
                        {
                            Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                            feed.StepImport = null;
                            feed.StepAddToCategories = currentProduct;
                            feed.StepAddImages = null;
                            context.SaveChanges();
                        }

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

                currentProduct = 0;

            UpdateImages:
                // Set step for backgroundWorker
                Form1.activeStep = "Update images..";
                bw.ReportProgress(0); // start new step of background process

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                using (SqlConnection destinationConnection = new SqlConnection(db.Database.Connection.ConnectionString))
                {
                    destinationConnection.Open();

                    while (db.ProductImages.Count(pi => pi.Products.CreatedByUser == vendorId) > 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("DELETE TOP (500)");
                        sb.AppendLine("FROM         CAT_ProductImages");
                        sb.AppendLine("FROM         CAT_ProductImages INNER JOIN");
                        sb.AppendLine("CAT_Products ON CAT_ProductImages.ProductID = CAT_Products.ProductID");
                        sb.AppendLine("WHERE     (CAT_Products.CreatedByUser = @CreatedByUser)");
                        SqlCommand commandDelete =
                            new SqlCommand(
                                sb.ToString(),
                                destinationConnection);
                        commandDelete.Parameters.Add("@CreatedByUser", SqlDbType.Int);
                        commandDelete.Parameters["@CreatedByUser"].Value = vendorId;
                        commandDelete.ExecuteNonQuery();
                    }

                    currentStep = 0;
                    foreach (var product in products)
                    {
                        if (currentStep++ < currentProduct)
                        {
                            continue;
                        }

                        Console.WriteLine(currentProduct.ToString() + " - " + product.Name);

                        var tempProduct =
                            db.Products.SingleOrDefault(
                                p => p.CategoryID == categoryId && p.ProductNumber == product.ProductNumber);
                        if (tempProduct == null)
                        {
                            currentProduct++;
                            continue;
                        }
                        var productId = tempProduct.ProductID;

                        foreach (var image in product.Images.Elements("url"))
                        {
                            if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                            {
                                SqlCommand commandAdd = new SqlCommand("exec CAT_AddProductImage @ProductID, @ImageFile, @Description, @ImageType, @ViewOrder",
                                       destinationConnection);
                                commandAdd.Parameters.Add("@ProductID", SqlDbType.Int);
                                commandAdd.Parameters["@ProductID"].Value = productId;
                                commandAdd.Parameters.Add("@ImageFile", SqlDbType.NVarChar, 255);
                                commandAdd.Parameters["@ImageFile"].Value = image.Value;
                                commandAdd.Parameters.Add("@Description", SqlDbType.NVarChar, 50);
                                commandAdd.Parameters["@Description"].Value = String.Empty;
                                commandAdd.Parameters.Add("@ImageType", SqlDbType.Char, 1);
                                commandAdd.Parameters["@ImageType"].Value = "0";
                                commandAdd.Parameters.Add("@ViewOrder", SqlDbType.Int);
                                commandAdd.Parameters["@ViewOrder"].Value = 0;
                                commandAdd.ExecuteNonQuery();
                            }
                        }

                        using (var context = new ImportProductsEntities())
                        {
                            Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                            feed.StepImport = null;
                            feed.StepAddToCategories = null;
                            feed.StepAddImages = currentProduct;
                            context.SaveChanges();
                        }

                        currentProduct++;
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            break;
                        }
                        else if (bw.WorkerReportsProgress && currentProduct % 100 == 0)
                            bw.ReportProgress((int)(100 * currentProduct / countProducts));
                    }
                }
            }
#endif
                //    rc = true;
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                log.Error("Error error logging", ex);
            }
            //return rc;
        }
    }
}
