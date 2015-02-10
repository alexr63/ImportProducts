using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity.Spatial;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Geocoding;
using Geocoding.Google;
using ImportProducts.Properties;
using NGeo.GeoNames;
using SelectedHotelsModel;

namespace ImportProducts
{
    public static class Common
    {
        public static void UpdateSteps(int? stepImport = null, int? stepAddToCategories = null, int? stepAddImages = null)
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
        public static IEnumerable<XElement> StreamRootChildDoc(string uri, string nodeName)
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
                            if (reader.Name == nodeName)
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
        public static void SaveFileFromURL(string url, string destinationFileName, int timeoutInSeconds, BackgroundWorker bw, DoWorkEventArgs e, log4net.ILog log)
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
                log.Error("Error error logging", err);
            }
        }
        public static void SetGeoNameId(ProductView product, SelectedHotelsEntities db, Hotel hotel, log4net.ILog log)
        {
            var placeName = product.Country;
            if (!String.IsNullOrEmpty(product.County))
            {
                placeName = product.County;
            }
            if (!String.IsNullOrEmpty(product.City))
            {
                placeName = product.City;
            }
            var geoNames = db.GeoNames.Where(gn => gn.Name.ToLower() == placeName.ToLower())
                .OrderByDescending(gn => gn.Population)
                .ThenByDescending(gn => gn.ModificationDate);
            if (geoNames.Any())
            {
                var geoName = geoNames.FirstOrDefault();
                if (geoName != null)
                {
                    hotel.GeoNameId = geoName.Id;
                }
            }
            if (hotel.GeoNameId == null && hotel.Location != null && hotel.Location.Latitude.HasValue && hotel.Location.Longitude.HasValue)
            {
                using (var geoNamesClient = new GeoNamesClient())
                {
                    var finder = new NearbyPlaceNameFinder
                    {
                        Latitude = hotel.Location.Latitude.Value,
                        Longitude = hotel.Location.Longitude.Value,
                        UserName = Settings.Default.GeoNamesUserName
                    };
                    try
                    {
                        var results = geoNamesClient.FindNearbyPlaceName(finder);
                        if (results != null && results.Any(r => r.FeatureClassName == "P"))
                        {
                            var toponym = results.First(r => r.FeatureClassName == "P");
                            hotel.GeoNameId = toponym.GeoNameId;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error error logging", ex);
                        if (ex.InnerException != null)
                        {
                            log.Error("Error error logging", ex.InnerException);
                        }
                    }
                }
            }
        }

        public static void SetLocation(ProductView product, SelectedHotelsEntities db, Hotel hotel, log4net.ILog log)
        {
            if (!String.IsNullOrEmpty(product.Lat) && !String.IsNullOrEmpty(product.Long))
            {
                var location = DbGeography.FromText(String.Format("POINT({0} {1})", product.Long, product.Lat));
                if (hotel.Location != location)
                {
                    hotel.Location = location;
                }
            }
            else
            {
                try
                {
                    IGeocoder geocoder = new GoogleGeocoder() { ApiKey = "" };
                    var addresses =
                        geocoder.Geocode(String.Format("{0}, {1}, {2}", product.Country, product.City, product.Address));
                    if (addresses.Any())
                    {
                        var address = addresses.First();
                        hotel.Location =
                            DbGeography.FromText(String.Format("POINT({0} {1})", address.Coordinates.Longitude, address.Coordinates.Latitude));
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Error error logging", ex);
                }
            }
        }
    }
}
