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
            Form1.activeStep = "Validating input..";
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
                               URL = (string) el.Element("hotel_link"),
                               Star = (string) el.Element("hotel_star"),
                               CustomerRating = (string) el.Element("customerrating"),
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
                        Hotel hotel =
                            db.Products.SingleOrDefault(
                                p => p.ProductTypeId == (int)Enums.ProductTypeEnum.Hotels && p.Categories.Any(c => c.Id == categoryId) && p.Name == xmlProduct1.Name && p.Number == xmlProduct1.ProductNumber) as Hotel;
                        if (hotel == null)
                        {
                            hotel = new Hotel();
                            hotel.ProductTypeId = (int) Enums.ProductTypeEnum.Hotels;
                            hotel.Name = xmlProduct1.Name;
                            hotel.Number = xmlProduct1.ProductNumber;
                            hotel.UnitCost = xmlProduct1.UnitCost;
                            hotel.Description = xmlProduct1.Description;
                            hotel.URL = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            hotel.Image = (string) xmlProduct1.Images.Element("url");
                            int star = 0;
                            string strStar = new string(xmlProduct1.Star.TakeWhile(char.IsDigit).ToArray());
                            if (strStar.Length > 0)
                            {
                                star = int.Parse(strStar);
                            }
                            hotel.Star = star;
                            if (!String.IsNullOrEmpty(xmlProduct1.CustomerRating))
                            {
                                hotel.CustomerRating = int.Parse(xmlProduct1.CustomerRating);
                            }
                            hotel.CreatedByUser = vendorId;
                            hotel.IsDeleted = false;

                            hotel.Rooms = null;

                            int? parentId = null;
                            Location country =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.Country &&
                                    c.ParentId == null);
                            if (country != null)
                            {
                                hotel.LocationId = country.Id;
                                parentId = country.Id;
                            }
                            Location county =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.County &&
                                    c.ParentId == parentId);
                            if (county != null)
                            {
                                hotel.LocationId = county.Id;
                                parentId = county.Id;
                            }
                            Location city =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.City &&
                                    c.ParentId == parentId);
                            if (city != null)
                            {
                                hotel.LocationId = city.Id;
                            }

                            Category category = db.Categories.SingleOrDefault(c => c.Id == categoryId);
                            if (category != null)
                            {
                                hotel.Categories.Add(category);
                            }

                            db.Products.Add(hotel);

                            db.SaveChanges();

                            i++;
                            UpdateSteps(stepImport: i);
                        }
                        else
                        {
                            // no need to check for null vallue because of previous if
                            bool isChanged = false;
                            if (hotel.UnitCost != xmlProduct1.UnitCost)
                            {
                                hotel.UnitCost = xmlProduct1.UnitCost;
                                isChanged = true;
                            }
                            if (hotel.Description != xmlProduct1.Description)
                            {
                                hotel.Description = xmlProduct1.Description;
                                isChanged = true;
                            }
                            if (hotel.URL != xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                            {
                                hotel.URL = xmlProduct1.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                isChanged = true;
                            }
                            if (hotel.Image != (string) xmlProduct1.Images.Element("url"))
                            {
                                hotel.Image = (string) xmlProduct1.Images.Element("url");
                                isChanged = true;
                            }
                            int star = 0;
                            string strStar = new string(xmlProduct1.Star.TakeWhile(char.IsDigit).ToArray());
                            if (strStar.Length > 0)
                            {
                                star = int.Parse(strStar);
                            }
                            if (hotel.Star != star)
                            {
                                hotel.Star = star;
                                isChanged = true;
                            }
                            if (hotel.CustomerRating.ToString() != xmlProduct1.CustomerRating)
                            {
                                hotel.CustomerRating = int.Parse(xmlProduct1.CustomerRating);
                                isChanged = true;
                            }

                            int? parentId = null;
                            Location country =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.Country &&
                                    c.ParentId == null);
                            if (country != null)
                            {
                                hotel.LocationId = country.Id;
                                parentId = country.Id;
                                isChanged = true;
                            }
                            Location county =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.County &&
                                    c.ParentId == parentId);
                            if (county != null)
                            {
                                hotel.LocationId = county.Id;
                                parentId = county.Id;
                                isChanged = true;
                            }
                            Location city =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == xmlProduct1.City &&
                                    c.ParentId == parentId);
                            if (city != null)
                            {
                                hotel.LocationId = city.Id;
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
                                p.ProductTypeId == (int)Enums.ProductTypeEnum.Hotels && p.Categories.Any(c => c.Id == categoryId) && p.Name == xmlProduct1.Name && p.Number == xmlProduct1.ProductNumber);
                        if (tempProduct == null)
                        {
                            i++;
                            continue;
                        }
                        var productId = tempProduct.Id;

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("DELETE");
                        sb.AppendLine("FROM         Cowrie_ProductImages");
                        sb.AppendLine("WHERE     ProductID = @ProductID");
                        SqlCommand commandDelete =
                            new SqlCommand(
                                sb.ToString(),
                                destinationConnection);
                        commandDelete.Parameters.Add("@ProductID", SqlDbType.Int);
                        commandDelete.Parameters["@ProductID"].Value = productId;
                        commandDelete.ExecuteNonQuery();

                        bool isChanged = false;
                        foreach (var image in xmlProduct1.Images.Elements("url"))
                        {
                            if (!image.Value.Contains("/thumbnail/") && !image.Value.Contains("/detail/"))
                            {
                                ProductImage productImage = new ProductImage();
                                productImage.URL = image.Value;
                                tempProduct.ProductImages.Add(productImage);
                                isChanged = true;
                            }
                        }
                        if (isChanged)
                        {
                            db.SaveChanges();
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
            using (var context = new SelectedHotelsEntities())
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
