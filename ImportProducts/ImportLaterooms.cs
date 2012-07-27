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
                                p => p.CategoryID == categoryId && p.ProductNumber == xmlProduct1.ProductNumber && p.CreatedByUser == vendorId) == null)
                        {
                            DataRow dataRow = dataTable.NewRow();
                            dataRow["CategoryID"] = categoryId;
                            dataRow["Category2ID"] = 0;
                            dataRow["Category3"] = String.Empty;
                            dataRow["ProductName"] = xmlProduct1.Name;
                            dataRow["ProductNumber"] = xmlProduct1.ProductNumber;
                            dataRow["UnitCost"] = xmlProduct1.UnitCost;
                            dataRow["UnitCost2"] = xmlProduct1.UnitCost;
                            dataRow["UnitCost3"] = xmlProduct1.UnitCost;
                            dataRow["UnitCost4"] = xmlProduct1.UnitCost;
                            dataRow["UnitCost5"] = xmlProduct1.UnitCost;
                            dataRow["UnitCost6"] = xmlProduct1.UnitCost;
                            dataRow["Description"] = xmlProduct1.Description;
                            dataRow["DescriptionHTML"] = xmlProduct1.DescriptionHTML;
                            dataRow["URL"] = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            dataRow["ProductCost"] = xmlProduct1.UnitCost;
                            dataRow["ProductImage"] = (string) xmlProduct1.Images.Element("url");
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
                                i += dataTable.Rows.Count;
                                dataTable.Rows.Clear();
                                UpdateSteps(stepImport: i);
                            }
                        }
                        else
                        {
                            var product =
                                db.Products.SingleOrDefault(
                                    p => p.CategoryID == categoryId && p.ProductNumber == xmlProduct1.ProductNumber && p.CreatedByUser == vendorId);
                            // no need to check for null vallue because of previous if
                            bool isChanged = false;
                            if (product.CategoryID != categoryId)
                            {
                                product.CategoryID = categoryId;
                                isChanged = true;
                            }
                            if (product.Category2ID != 0)
                            {
                                product.Category2ID = 0;
                                isChanged = true;
                            }
                            if (product.Category3 != String.Empty)
                            {
                                product.Category3 = String.Empty;
                                isChanged = true;
                            }
                            if (product.ProductName != xmlProduct1.Name)
                            {
                                product.ProductName = xmlProduct1.Name;
                                isChanged = true;
                            }
                            if (product.ProductNumber != xmlProduct1.ProductNumber)
                            {
                                product.ProductNumber = xmlProduct1.ProductNumber;
                                isChanged = true;
                            }
                            if (product.UnitCost != xmlProduct1.UnitCost)
                            {
                                product.UnitCost = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.UnitCost2 != xmlProduct1.UnitCost)
                            {
                                product.UnitCost2 = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.UnitCost3 != xmlProduct1.UnitCost)
                            {
                                product.UnitCost3 = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.UnitCost4 != xmlProduct1.UnitCost)
                            {
                                product.UnitCost4 = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.UnitCost5 != xmlProduct1.UnitCost)
                            {
                                product.UnitCost5 = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.UnitCost6 != xmlProduct1.UnitCost)
                            {
                                product.UnitCost6 = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.Description != xmlProduct1.Description)
                            {
                                product.Description = xmlProduct1.Description;
                                isChanged = true;
                            }
                            if (product.DescriptionHTML != xmlProduct1.DescriptionHTML)
                            {
                                product.DescriptionHTML = xmlProduct1.DescriptionHTML;
                                isChanged = true;
                            }
                            if (product.URL != xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                            {
                                product.URL = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                isChanged = true;
                            }
                            if (product.ProductCost != xmlProduct1.UnitCost)
                            {
                                product.ProductCost = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (product.ProductImage != (string) xmlProduct1.Images.Element("url"))
                            {
                                product.ProductImage = (string) xmlProduct1.Images.Element("url");
                                isChanged = true;
                            }
                            if (product.OrderQuant != "0")
                            {
                                product.OrderQuant = "0";
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

                        if (!String.IsNullOrEmpty(xmlProduct1.Country))
                        {
                            AdvCat advCatCountry;
                            if (parentID.HasValue)
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == xmlProduct1.Country &&
                                        ac.Level == level && ac.ParentId == parentID.Value);
                            }
                            else
                            {
                                advCatCountry =
                                    db.AdvCats.SingleOrDefault(
                                        ac =>
                                        ac.PortalID == portalId && ac.AdvCatName == xmlProduct1.Country &&
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
                                                        AdvCatName = xmlProduct1.Country,
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

                        if (!String.IsNullOrEmpty(xmlProduct1.County))
                        {
                            var advCatCounty =
                                db.AdvCats.SingleOrDefault(
                                    ac =>
                                    ac.PortalID == portalId && ac.AdvCatName == xmlProduct1.County && ac.Level == level);
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
                                                       AdvCatName = xmlProduct1.County,
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

                        if (!String.IsNullOrEmpty(xmlProduct1.City))
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
                                p => p.CategoryID == categoryId && p.ProductNumber == xmlProduct1.ProductNumber);
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
                                p => p.CategoryID == categoryId && p.ProductNumber == xmlProduct1.ProductNumber);
                        if (tempProduct == null)
                        {
                            i++;
                            continue;
                        }
                        var productId = tempProduct.ProductID;

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("DELETE");
                        sb.AppendLine("FROM         CAT_ProductImages");
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
