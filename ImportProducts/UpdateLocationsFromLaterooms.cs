using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using Ionic.Zip;

namespace ImportProducts
{


    class UpdateLocationsFromLaterooms
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
                //throw new Exception("Error saving file from URL:" + err.Message, err);
            }
        }

        public static void DoImport(object sender, DoWorkEventArgs e)               // static bool    (string _URL, out string message)
        {
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

            long countProducts = xmlProducts.Count();
            using (SelectedHotelsEntities db = new SelectedHotelsEntities())
            {
                long currentProduct = 0;
                foreach (var xmlProduct in xmlProducts)
                {
                    if (!String.IsNullOrEmpty(xmlProduct.Country))
                    {
                        var country = db.Locations.SingleOrDefault(l => l.Name == xmlProduct.Country && l.ParentId == null);
                        if (country == null)
                        {
                            country = new Location { Name = xmlProduct.Country };
                            db.Locations.Add(country);
                            db.SaveChanges();
                        }
                        string hotelCity = xmlProduct.City;
                        if (xmlProduct.City.Length > 50)
                        {
                            hotelCity = xmlProduct.City.Substring(0, 47).PadRight(50, '.');
                        }
                        var city = db.Locations.SingleOrDefault(l => l.Name == hotelCity && l.ParentId == country.Id);
                        if (city == null)
                        {
                            city = new Location {Name = hotelCity, ParentId = country.Id};
                            db.Locations.Add(city);
                            db.SaveChanges();
                        }
                    }
                    currentProduct++;
                    if (bw.CancellationPending)
                    {
                        e.Cancel = true;
                        break;
                    }
                    else if (bw.WorkerReportsProgress && currentProduct % 100 == 0) bw.ReportProgress((int)(100 * currentProduct / countProducts));
                }
            }

            // show progress & catch Cancel
            if (bw.CancellationPending)
            {
                e.Cancel = true;
                return;
            }
            else if (bw.WorkerReportsProgress) bw.ReportProgress(100);
            Thread.Sleep(100); // a little bit slow working for visualisation Progress

            Form1.activeStep = "Update locations...";
            bw.ReportProgress(0); // start new step of background process

            using (SelectedHotelsEntities db = new SelectedHotelsEntities())
            {
                int i = 0;
                foreach (var xmlProduct in xmlProducts)
                {
                    var xmlProduct1 = xmlProduct;
                    Console.WriteLine(i + " - " + xmlProduct1.Name); // debug print

                    // create locations
                    string hotelCity = xmlProduct1.City;
                    if (xmlProduct1.City.Length > 50)
                    {
                        hotelCity = xmlProduct1.City.Substring(0, 47).PadRight(50, '.');
                    }
                    int? parentId = null;
                    int? countryId = null;
                    Location country = null;
                    int? countyId = null;
                    Location county = null;
                    int? cityId = null;
                    Location city = null;
                    int level = 0;
                    int maxOrder = 0;

                    if (!String.IsNullOrEmpty(xmlProduct1.Country))
                    {
                        country =
                            db.Locations.SingleOrDefault(
                                l =>
                                l.Name == xmlProduct1.Country &&
                                l.ParentId == null);
                        if (country == null)
                        {
                            country = new Location
                            {
                                Name = xmlProduct1.Country,
                                IsDeleted = false
                            };
                            db.Locations.Add(country);
                            db.SaveChanges();
                        }
                        parentId = country.Id;
                        countryId = country.Id;
                        level++;
                    }

                    if (!String.IsNullOrEmpty(xmlProduct1.County))
                    {
                        county =
                            db.Locations.SingleOrDefault(
                                l =>
                                l.Name == xmlProduct1.County &&
                                l.ParentId == parentId);
                        if (county == null)
                        {
                            county = new Location
                            {
                                Name = xmlProduct1.County,
                                ParentId = parentId.Value,
                                IsDeleted = false
                            };
                            db.Locations.Add(county);
                            db.SaveChanges();
                        }
                        parentId = county.Id;
                        countyId = county.Id;
                        level++;
                    }

                    if (!String.IsNullOrEmpty(xmlProduct1.City))
                    {
                        city =
                            db.Locations.SingleOrDefault(
                                l =>
                                l.Name == hotelCity &&
                                l.ParentId == parentId);
                        if (city == null)
                        {
                            city = new Location
                            {
                                Name = hotelCity,
                                ParentId = parentId.Value,
                                IsDeleted = false
                            };
                            db.Locations.Add(city);
                            db.SaveChanges();
                        }
                        parentId = city.Id;
                        cityId = city.Id;
                        level++;
                    }

                    db.SaveChanges();

                    i++;

                    if (bw.CancellationPending)
                    {
                        e.Cancel = true;
                        goto Cancelled;
                    }
                    else if (bw.WorkerReportsProgress && i % 100 == 0)
                    {
                        bw.ReportProgress((int)(100 * i / countProducts));
                    }
                }
            }
        Cancelled:
            int q = 0;
        }
    }
}
