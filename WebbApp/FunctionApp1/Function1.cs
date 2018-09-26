using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace FunctionApp1
{
    public static class Function1
    {
        private static string Openweatherkey => "MyOpenWeatherKey";
        private static string OpenWeatherPath => "https://api.openweathermap.org/data/2.5/weather";
        private static string MapsKey => "MyMapsKey";
        private static string MapsPath => $"https://atlas.microsoft.com/map/static/png?subscription-key={MapsKey}&api-version=1.0&layer=basic&style=main&zoom=12";
        private static string StorageKey => "MyStorageKey";

        [FunctionName("Function1")]
        public static void Run([QueueTrigger("myqueue", Connection = "AzureWebJobsStorage")]string myQueueItem, ILogger log)
        {
            string[] location = myQueueItem.Split(',');
            if (location.Length != 2)
                return;
            string lat = location[0];
            string lon = location[1];
            if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude) ||
                !double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude) ||
                latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                return;
            }
            double temperature = GetTemperatueAsync(lat, lon).GetAwaiter().GetResult();
            byte[] image = GetMapsImage(lat, lon).GetAwaiter().GetResult();
            string bier = temperature > 293 ? "BIER" : "GEEN BIER";
            Stream s = AddTextToImage(new MemoryStream(image, true), (bier, (256, 256)));
            UploadBlobImage(s, $"MapImage{lat},{lon}.png");
        }

        public static Stream AddTextToImage(Stream imageStream, params (string text, (float x, float y) position)[] texts)
        {
            var memoryStream = new MemoryStream();
            var image = Image.Load(imageStream);
            image
                .Clone(img =>
                {
                    foreach (var (text, (x, y)) in texts)
                    {
                        img.DrawText(text, SystemFonts.CreateFont("Verdana", 40), Rgba32.Red, new PointF(x, y));
                    }
                })
                .SaveAsPng(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        static void UploadBlobImage(Stream image, string name)
        {
            StorageCredentials cred = new StorageCredentials("wgbstorage", StorageKey);
            CloudStorageAccount c = new CloudStorageAccount(cred, true);
            CloudBlobClient client = c.CreateCloudBlobClient();
            CloudBlobContainer cont = client.GetContainerReference("mycontainer");
            cont.CreateIfNotExistsAsync();
            CloudBlockBlob cblob = cont.GetBlockBlobReference(name);
            cblob.UploadFromStreamAsync(image);
        }


        static async Task<double> GetTemperatueAsync(string lat, string @long)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(OpenWeatherPath);
                client.DefaultRequestHeaders.Accept.Clear();
                string requestUri = $"{OpenWeatherPath}?lat={lat}&lon={@long}&appid={Openweatherkey}";
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = client.GetAsync(requestUri).Result;
                //response.EnsureSuccessStatusCode();
                var responseAsString = await response.Content.ReadAsStringAsync();
                JObject jObject = JObject.Parse(responseAsString);
                var main = jObject["main"];
                var temp = main["temp"];
                return double.Parse(temp.ToString());
            }
        }

        static async Task<byte[]> GetMapsImage(string lat, string lon)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(OpenWeatherPath);
                client.DefaultRequestHeaders.Accept.Clear();
                string requestUri = $"{MapsPath}&center={lon},{lat}";
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage response = client.GetAsync(requestUri).Result;
                //response.EnsureSuccessStatusCode();
                var responseAsString = await response.Content.ReadAsByteArrayAsync();
                return responseAsString;
            }
        }
    }
}
