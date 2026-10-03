using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ClearFrame {
    // Only fixed YouTube image URLs are fetched; metadata cannot choose arbitrary hosts.
    sealed class Thumbnails {
        readonly HttpClient client = new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(8),MaxResponseContentBufferSize=2097152 };
        readonly SemaphoreSlim slots = new SemaphoreSlim(4);
        readonly CancellationTokenSource stopping = new CancellationTokenSource();
        readonly Dictionary<string,Task<BitmapSource>> cache = new Dictionary<string,Task<BitmapSource>>();
        public Task<BitmapSource> Get(string id) {
            if (!Regex.IsMatch(id??"",@"^[A-Za-z0-9_-]{11}$")) return Task.FromResult<BitmapSource>(null);
            Task<BitmapSource> result;if(!cache.TryGetValue(id,out result)){result=Fetch(id);cache[id]=result;}return result;
        }
        async Task<BitmapSource> Fetch(string id) {
            bool acquired=false;
            try {
                await slots.WaitAsync(stopping.Token);acquired=true;
                using(var response=await client.GetAsync("https://i.ytimg.com/vi/"+id+"/mqdefault.jpg",stopping.Token)) {
                    response.EnsureSuccessStatusCode();var bytes=await response.Content.ReadAsByteArrayAsync();
                    using(var memory=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=224;image.StreamSource=memory;image.EndInit();image.Freeze();return image;}
                }
            } catch { return null; } finally { if(acquired)slots.Release(); }
        }
        public void Stop(){stopping.Cancel();client.Dispose();}
    }
}
