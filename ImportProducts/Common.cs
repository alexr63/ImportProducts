using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
