using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WarpGenerator.Models;

namespace WarpGenerator.Services
{
    /// <summary>
    /// Raised when every WARP API mirror fails. <see cref="MirrorFailures"/> holds
    /// one human-readable reason per mirror so callers can surface the detail.
    /// </summary>
    public class WarpApiException : Exception
    {
        public IReadOnlyList<string> MirrorFailures { get; }

        public WarpApiException(string message, IReadOnlyList<string> mirrorFailures)
            : base(message)
        {
            MirrorFailures = mirrorFailures;
        }
    }

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

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private readonly HttpClient _httpClient;

        public WarpApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Client", "WARP");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WARP-Generator-Desktop/1.0");
        }

        /// <summary>
        /// Tries each mirror in turn. Distinguishes timeout, network failure,
        /// non-success HTTP status, malformed JSON and missing keys; on total
        /// failure throws <see cref="WarpApiException"/> naming each mirror's cause.
        /// A caller-supplied cancellation is propagated as-is, not masked as a timeout.
        /// </summary>
        public async Task<WarpApiResponse> FetchWarpKeysAsync(
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var failures = new List<string>();

            for (int i = 0; i < Endpoints.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string url = Endpoints[i];
                int mirror = i + 1;
                progress?.Report($"Запрос ключей WARP (зеркало {mirror}/{Endpoints.Length})...");

                using var timeoutCts = new CancellationTokenSource(RequestTimeout);
                using var linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await _httpClient.SendAsync(
                        request, HttpCompletionOption.ResponseContentRead, linkedCts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        failures.Add(
                            $"зеркало {mirror}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
                        continue;
                    }

                    string json = await response.Content.ReadAsStringAsync(linkedCts.Token);

                    WarpApiResponse? result;
                    try
                    {
                        result = JsonSerializer.Deserialize<WarpApiResponse>(json);
                    }
                    catch (JsonException)
                    {
                        failures.Add($"зеркало {mirror}: сервер вернул не-JSON или повреждённый ответ");
                        continue;
                    }

                    if (result == null
                        || string.IsNullOrWhiteSpace(result.PrivKey)
                        || string.IsNullOrWhiteSpace(result.PeerPub))
                    {
                        failures.Add($"зеркало {mirror}: ответ без обязательных ключей privKey/peer_pub");
                        continue;
                    }

                    result.Success = true;
                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Genuine caller cancellation — propagate instead of reporting a fake timeout.
                    throw;
                }
                catch (OperationCanceledException)
                {
                    // Only the per-request timeout fired.
                    failures.Add($"зеркало {mirror}: превышен таймаут ответа ({RequestTimeout.TotalSeconds:0} с)");
                }
                catch (HttpRequestException ex)
                {
                    failures.Add($"зеркало {mirror}: сетевой сбой ({ex.Message})");
                }
                catch (Exception ex)
                {
                    failures.Add($"зеркало {mirror}: {ex.Message}");
                }
            }

            string detail = failures.Count > 0 ? string.Join("; ", failures) : "нет доступных зеркал";
            throw new WarpApiException(
                $"Не удалось получить ключи ни с одного из {Endpoints.Length} зеркал WARP API. " +
                $"Причины по зеркалам — {detail}.",
                failures);
        }
    }
}
