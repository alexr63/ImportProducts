using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SelectedHotelsModel;

namespace ImportProducts
{
    public static class Common
    {
        public static void SetLocation(SelectedHotelsEntities db, Location location, Hotel hotel)
        {
            var hotelLocation = hotel.HotelLocations.SingleOrDefault(hl => hl.LocationId == location.Id);
            if (hotelLocation == null)
            {
                hotelLocation = new HotelLocation
                {
                    HotelId = hotel.Id,
                    LocationId = location.Id,
                    HotelTypeId = hotel.HotelTypeId
                };
                db.HotelLocations.Add(hotelLocation);
            }
        }

        public static Location AddLocation(SelectedHotelsEntities db, string locationName, int? parentId, int locationTypeId)
        {
            Location location = db.Locations.SingleOrDefault(l => l.Name == locationName && l.ParentId == parentId && l.LocationTypeId == locationTypeId);
            if (location == null)
            {
                location = new Location
                {
                    Name = locationName,
                    ParentId = parentId,
                    LocationTypeId = locationTypeId,
                    IsDeleted = false
                };
                db.Locations.Add(location);
                db.SaveChanges();
            }
            return location;
        }

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

        public static void DeleteEmptyLocations(SelectedHotelsEntities db)
        {
            foreach (Location location in db.Locations.Where(l => !l.IsDeleted))
            {
                if (!db.HotelsInLocation(location.Id).Any())
                {
                    location.IsDeleted = true;
                }
            }
            db.SaveChanges();
        }
    }
}
