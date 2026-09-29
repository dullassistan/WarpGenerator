using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WarpGenerator.Models;

namespace WarpGenerator.Services
{
    public class WarpApiService
    {
        private static readonly string[] Endpoints = new[]
        {
            "https://www.warp-generator.workers.dev",
            "https://warp-gen.netlify.app/",
            "https://warp.sub-aggregator.workers.dev",
            "https://warp-vercel-chi.vercel.app/api/warp-data",
            "https://warp-vercel-murex.vercel.app/api/warp-data"
        };

        private readonly HttpClient _httpClient;

        public WarpApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Client", "WARP");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WARP-Generator-Desktop/1.0");
        }

        public async Task<WarpApiResponse> FetchWarpKeysAsync(IProgress<string>? progress = null)
        {
            Exception? lastError = null;

            for (int i = 0; i < Endpoints.Length; i++)
            {
                string url = Endpoints[i];
                progress?.Report($"Запрос ключей WARP (зеркало {i + 1}/{Endpoints.Length})...");

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Сервер ответил со статусом {(int)response.StatusCode} {response.ReasonPhrase}");
                    }

                    string json = await response.Content.ReadAsStringAsync(cts.Token);
                    var result = JsonSerializer.Deserialize<WarpApiResponse>(json);

                    if (result == null || string.IsNullOrWhiteSpace(result.PrivKey) || string.IsNullOrWhiteSpace(result.PeerPub))
                    {
                        throw new Exception("Ответ сервера не содержит обязательных ключей privKey/peer_pub");
                    }

                    result.Success = true;
                    return result;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw new Exception($"Не удалось получить ключи ни с одного зеркала WARP API. Последняя ошибка: {lastError?.Message}");
        }
    }
}
