using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using WebbApp.Models;

namespace WebbApp.Controllers
{
    public class HomeController : Controller
    {
        private static string StorageConnection => "DefaultEndpointsProtocol=https;AccountName=wgbstorage;AccountKey=JhPkjwEfOGPwBKTQ8FCofQroA5+OkBrosPa8zM/jRuwbxl2PRjveoKnCQqXjrjTkLS4Lvd7FjbLHWyTVIi7xcg==;EndpointSuffix=core.windows.net";
        private static string BlobUrl => "https://wgbstorage.blob.core.windows.net/mycontainer/";
        public async Task<ActionResult> Index()
        {
            string lat = Request.QueryString.Get("lat");
            string lon = Request.QueryString.Get("lon");
            if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) ||
                !double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude) ||
                latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Provide valid lat and lon");
            }
            try
            {
                await MakeQueue(lat, lon);
            }
            catch (Exception e)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Can't write queue: " + e);
            }
            string url = BlobUrl + $"MapImage{lat},{lon}.png";
            string json = new JavaScriptSerializer().Serialize(new ViewModel { ImageUrl = url });
            return Content(json, "application/json");
        }

        private async Task MakeQueue(string lat, string lon)
        {
            var storageAccount = CloudStorageAccount.Parse(StorageConnection);
            var client = storageAccount.CreateCloudQueueClient();
            var queue = client.GetQueueReference("myqueue");
            await queue.CreateIfNotExistsAsync();
            var message = $"{lat},{lon}";
            await queue.AddMessageAsync(new CloudQueueMessage(message));
        }
    }
}