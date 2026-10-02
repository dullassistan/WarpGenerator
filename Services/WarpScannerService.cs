using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WarpGenerator.Models;

namespace WarpGenerator.Services
{
    /// <summary>
    /// Реальный сканер эндпоинтов WARP на базе внешнего инструмента warpscout
    /// (https://github.com/vernette/warpscout, лицензия MIT). Инструмент
    /// скачивается по запросу в %LocalAppData%\WarpGenerator\Tools, целостность
    /// проверяется по SHA-256. Никаких фиктивных проверок: пинг и «рабочесть»
    /// берутся из реального двухфазного скана warpscout (фаза 2 поднимает
    /// настоящий AWG-туннель и проверяет выходную колонию), поэтому в список
    /// попадают только эндпоинты, через которые туннель реально устанавливается
    /// с этой машины.
    /// </summary>
    public class WarpScannerService
    {
        private const string WarpscoutVersion = "0.16.0";
        private const string DownloadUrl =
            "https://github.com/vernette/warpscout/releases/download/v0.16.0/warpscout_0.16.0_windows_amd64.zip";
        // Контрольные суммы официального релиза v0.16.0 (windows/amd64).
        private const string ZipSha256 =
            "8f5c39686b780923fbe0696f6b4b87682a8d840f3ba07e9b726b3ac2ee771b5c";
        private const string ExeSha256 =
            "cbea2f600c3cfaf2bda5b1e6180c1d215fb3807fd425092cde40b0cf17ee3130";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

        private static readonly Regex EndpointRe =
            new(@"^(?:\d{1,3}\.){3}\d{1,3}:\d{1,5}$", RegexOptions.Compiled);
        private static readonly Regex PingRe =
            new(@"(\d+)\s*ms", RegexOptions.Compiled);

        private readonly string _toolsDir;
        private readonly string _exePath;
        private readonly string _accountPath;

        public WarpScannerService()
        {
            _toolsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WarpGenerator", "Tools");
            _exePath = Path.Combine(_toolsDir, "warpscout.exe");
            _accountPath = Path.Combine(_toolsDir, "warpscout-account.json");
        }

        /// <summary>warpscout.exe уже скачан и доступен?</summary>
        public bool IsWarpscoutInstalled => File.Exists(_exePath);

        /// <summary>Атрибуция стороннего инструмента для интерфейса.</summary>
        public string ToolCredit => $"warpscout v{WarpscoutVersion} © vernette (MIT)";

        /// <summary>
        /// Скачивает (при необходимости) warpscout, регистрирует аккаунт WARP и
        /// возвращает до трёх лучших рабочих эндпоинтов по возрастанию пинга.
        /// </summary>
        public async Task<List<WarpEndpointItem>> ScanEndpointsAsync(
            bool foreignOnly, IProgress<string>? progress, CancellationToken ct)
        {
            await EnsureToolAsync(progress, ct);
            await EnsureAccountAsync(progress, ct);
            return await RunScanAsync(foreignOnly, progress, ct);
        }

        // ---------- Установка инструмента ----------

        private async Task EnsureToolAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (File.Exists(_exePath))
            {
                return;
            }

            Directory.CreateDirectory(_toolsDir);
            progress?.Report("⏳ Загрузка warpscout (~5 МБ)...");

            byte[] zipBytes;
            try
            {
                zipBytes = await Http.GetByteArrayAsync(DownloadUrl, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось скачать warpscout: {ex.Message}");
            }

            if (!HashEquals(SHA256.HashData(zipBytes), ZipSha256))
            {
                throw new Exception("Контрольная сумма архива warpscout не совпала — загрузка отклонена.");
            }

            progress?.Report("⏳ Распаковка warpscout...");
            string tmpExe = _exePath + ".tmp";
            ExtractExe(zipBytes, tmpExe);

            byte[] exeBytes = await File.ReadAllBytesAsync(tmpExe, ct);
            if (!HashEquals(SHA256.HashData(exeBytes), ExeSha256))
            {
                TryDelete(tmpExe);
                throw new Exception("Контрольная сумма warpscout.exe не совпала — файл удалён.");
            }

            TryDelete(_exePath);
            File.Move(tmpExe, _exePath);
        }

        private static void ExtractExe(byte[] zipBytes, string destPath)
        {
            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.Name.Equals("warpscout.exe", StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                throw new Exception("В архиве warpscout не найден warpscout.exe.");
            }
            using var es = entry.Open();
            using var fs = File.Create(destPath);
            es.CopyTo(fs);
        }

        // ---------- Аккаунт WARP ----------

        private async Task EnsureAccountAsync(IProgress<string>? progress, CancellationToken ct)
        {
            if (File.Exists(_accountPath))
            {
                return;
            }

            progress?.Report("⏳ Регистрация аккаунта WARP...");
            var (code, _, stderr) = await RunProcessAsync(
                $"register -a \"{_accountPath}\"", TimeSpan.FromSeconds(90), null, ct);

            if (code != 0 || !File.Exists(_accountPath))
            {
                throw new Exception($"Не удалось зарегистрировать аккаунт WARP. {Shorten(stderr)}");
            }
        }

        // ---------- Скан ----------

        private async Task<List<WarpEndpointItem>> RunScanAsync(
            bool foreignOnly, IProgress<string>? progress, CancellationToken ct)
        {
            var args = new StringBuilder("scan -proto awg -plain -no-report -best-by ping ");
            args.Append($"-a \"{_accountPath}\"");
            if (foreignOnly)
            {
                args.Append(" -exclude-country RU");
            }

            progress?.Report("⏳ Сканирование пулов WARP (может занять 1–3 минуты)...");

            var (code, lines, stderr) = await RunProcessAsync(
                args.ToString(), TimeSpan.FromMinutes(4),
                line =>
                {
                    string? p = MapProgress(line);
                    if (p != null)
                    {
                        progress?.Report(p);
                    }
                }, ct);

            var items = ParseTable(lines);
            if (items.Count == 0 && code != 0)
            {
                throw new Exception($"warpscout завершился с ошибкой. {Shorten(stderr)}");
            }

            return items
                .GroupBy(i => i.Endpoint)
                .Select(g => g.First())
                .OrderBy(i => i.PingMs)
                .Take(3)
                .ToList();
        }

        internal static string? MapProgress(string line)
        {
            if (line.Contains("Phase 1"))
            {
                return "⏳ Фаза 1: поиск доступных портов WARP...";
            }
            if (line.Contains("Phase 2"))
            {
                return "⏳ Фаза 2: проверка туннелей (реальное соединение)...";
            }
            return null;
        }

        internal static List<WarpEndpointItem> ParseTable(List<string> lines)
        {
            var items = new List<WarpEndpointItem>();
            foreach (var raw in lines)
            {
                if (!raw.Contains('│'))
                {
                    continue;
                }

                var cols = raw.Split('│')
                              .Select(c => c.Trim())
                              .Where(c => c.Length > 0)
                              .ToArray();
                if (cols.Length < 6 || !EndpointRe.IsMatch(cols[1]))
                {
                    continue;
                }

                var m = PingRe.Match(cols[2]);
                if (!m.Success || !long.TryParse(m.Groups[1].Value, out long ping))
                {
                    continue;
                }

                string ep = cols[1];
                var parts = ep.Split(':');
                int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 2408;

                items.Add(new WarpEndpointItem
                {
                    Endpoint = ep,
                    Ip = parts[0],
                    Port = port,
                    PingMs = ping,
                    Node = cols[4],
                    Location = string.IsNullOrEmpty(cols[5]) ? cols[4] : $"{cols[4]} ({cols[5]})",
                    IsForeign = !string.Equals(cols[3], "RU", StringComparison.OrdinalIgnoreCase)
                });
            }
            return items;
        }

        // ---------- Запуск процесса ----------

        private async Task<(int Code, List<string> Lines, string Stderr)> RunProcessAsync(
            string arguments, TimeSpan timeout, Action<string>? onLine, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = arguments,
                WorkingDirectory = _toolsDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync();
            var lines = new List<string>();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            try
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(timeoutCts.Token)) != null)
                {
                    lines.Add(line);
                    onLine?.Invoke(line);
                }
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }
                throw new Exception("warpscout: превышено время ожидания, процесс остановлен.");
            }

            string stderr = await stderrTask;
            return (process.ExitCode, lines, stderr);
        }

        // ---------- Утилиты ----------

        private static bool HashEquals(byte[] hash, string expectedHex)
        {
            return string.Equals(Convert.ToHexString(hash), expectedHex, StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryKill(Process process)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        }

        private static string Shorten(string? s)
        {
            s = (s ?? string.Empty).Trim();
            const int max = 300;
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }
    }
}
