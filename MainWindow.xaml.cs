using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WarpGenerator.Models;
using WarpGenerator.Services;

namespace WarpGenerator
{
    public partial class MainWindow : Window
    {
        private readonly WarpApiService _apiService = new();
        private readonly WarpConfigBuilderService _configBuilder = new();
        private WarpApiResponse? _currentApiData;
        private bool _isInitializing = true;
        private DispatcherTimer? _alertTimer;
        private string _clashMode = "awg"; // "awg", "masque", "hybrid"
        private bool _isEuropeMode = true;
        private int _warpDownloadCount = 0;
        private int _warpEuroDownloadCount = 0;

        // --- Сканер эндпоинтов WARP (warpscout) ---
        private readonly WarpScannerService _scannerService = new();
        private bool _foreignOnly = false;
        private bool _isScanning = false;
        private System.Threading.CancellationTokenSource? _scanCts;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitDefaultParameters();
            _isInitializing = false;
            await GenerateNewConfigAsync();
        }

        private void InitDefaultParameters()
        {
            TxtEndpoint.Text = CryptoService.GenerateRandomEuropeEndpoint();

            if (SwAwg1 != null) SwAwg1.IsChecked = false;
            if (SwAwg2 != null) SwAwg2.IsChecked = true;
            if (SwAwg3 != null) SwAwg3.IsChecked = false;
            if (SwAwg31 != null) SwAwg31.IsChecked = false;

            if (string.IsNullOrWhiteSpace(TxtI1.Text))
            {
                TxtI1.Text = CryptoService.GetRandomAwg2I1();
            }

            if (string.IsNullOrWhiteSpace(TxtCpa.Text))
            {
                var (cpa, mha, kt, rat, rkat, rt) = CryptoService.GenerateRandomAwg3();
                TxtCpa.Text = cpa;
                TxtMha.Text = mha;
                TxtKt.Text = kt;
                TxtRat.Text = rat;
                TxtRkat.Text = rkat;
                TxtRt.Text = rt;
            }

            UpdateAwgPanelsVisibility();
        }

        private WarpConfigSettings GetCurrentSettings()
        {
            var settings = new WarpConfigSettings();
            settings.ClashMode = _clashMode;

            // Client
            if (RbClientWiresock?.IsChecked == true) settings.SelectedClient = "WireSock";
            else if (RbClientClash?.IsChecked == true) settings.SelectedClient = "Clash";
            else settings.SelectedClient = "AmneziaWG";

            // DNS
            string selectedDnsId = "cf";
            if (RbDnsGoogle?.IsChecked == true) selectedDnsId = "google";
            else if (RbDnsMalw?.IsChecked == true) selectedDnsId = "malw";
            else if (RbDnsXbox?.IsChecked == true) selectedDnsId = "xbox";
            else if (RbDnsGeohide?.IsChecked == true) selectedDnsId = "geohide";
            else if (RbDnsComss?.IsChecked == true) selectedDnsId = "comss";
            settings.SelectedDnsId = selectedDnsId;

            // Endpoint & MTU
            settings.Endpoint = TxtEndpoint?.Text?.Trim() ?? "engage.cloudflareclient.com:2408";
            settings.Mtu = TxtMtu?.Text?.Trim() ?? "1420";

            // AWG 1.0
            settings.IsAwg1 = SwAwg1?.IsChecked == true;
            if (RbJunk2?.IsChecked == true) settings.Awg1JunkPreset = 2;
            else if (RbJunk3?.IsChecked == true) settings.Awg1JunkPreset = 3;
            else settings.Awg1JunkPreset = 1;

            settings.Awg1Jc = string.IsNullOrWhiteSpace(TxtJc?.Text) ? "4" : TxtJc.Text.Trim();
            settings.Awg1Jmin = string.IsNullOrWhiteSpace(TxtJmin?.Text) ? "40" : TxtJmin.Text.Trim();
            settings.Awg1Jmax = string.IsNullOrWhiteSpace(TxtJmax?.Text) ? "70" : TxtJmax.Text.Trim();

            // AWG 2.0
            settings.IsAwg2 = SwAwg2?.IsChecked == true;
            settings.Awg2I1 = TxtI1?.Text?.Trim() ?? "";
            settings.Awg2I2 = TxtI2?.Text?.Trim() ?? "";
            settings.Awg2I3 = TxtI3?.Text?.Trim() ?? "";
            settings.Awg2I4 = TxtI4?.Text?.Trim() ?? "";
            settings.Awg2I5 = TxtI5?.Text?.Trim() ?? "";

            // WireSock
            settings.WireSockId = TxtWireSockId?.Text?.Trim() ?? "apteka.ru";
            settings.WireSockIp = (CmbWireSockIp?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "quic";
            settings.WireSockIb = (CmbWireSockIb?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "curl";

            // AWG 3.0
            settings.IsAwg3 = SwAwg3?.IsChecked == true;
            settings.Awg3Cpa = TxtCpa?.Text?.Trim() ?? "";
            settings.Awg3Mha = TxtMha?.Text?.Trim() ?? "";
            settings.Awg3Kt = TxtKt?.Text?.Trim() ?? "";
            settings.Awg3Rat = TxtRat?.Text?.Trim() ?? "";
            settings.Awg3Rkat = TxtRkat?.Text?.Trim() ?? "";
            settings.Awg3Rt = TxtRt?.Text?.Trim() ?? "";

            // AWG 3.1
            settings.IsAwg31 = SwAwg31?.IsChecked == true;
            settings.Awg31RandomTrailers = SwRandomTrailers?.IsChecked == true;
            settings.Awg31DisableCookies = SwDisableCookies?.IsChecked == true;

            // Split Tunneling, IPv6 & Keepalive
            settings.ExcludeLan = SwExcludeLan?.IsChecked == true;
            settings.EnableIpv6 = SwIpv6?.IsChecked == true;
            settings.PersistentKeepaliveEnabled = SwKeepalive?.IsChecked == true;
            settings.PersistentKeepaliveValue = TxtKeepalive?.Text?.Trim() ?? "25";

            return settings;
        }

        private void TriggerConfigRegeneration()
        {
            if (_isInitializing || _currentApiData == null) return;
            var settings = GetCurrentSettings();
            TxtConfig.Text = _configBuilder.BuildConfigString(_currentApiData, settings);
        }

        private async Task GenerateNewConfigAsync()
        {
            try
            {
                BtnGenerate.IsEnabled = false;
                ShowAlert("⏳ Запрос ключей WARP...", false);

                var apiData = await _apiService.FetchWarpKeysAsync();
                if (apiData != null && !string.IsNullOrEmpty(apiData.PrivKey))
                {
                    _currentApiData = apiData;
                    TriggerConfigRegeneration();
                    ShowAlert("Профиль создан, выберите протоколы обхода слева", false);
                }
                else
                {
                    ShowAlert("❌ Не удалось получить ключи от зеркал WARP", true);
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"❌ Ошибка генерации: {ex.Message}", true);
            }
            finally
            {
                BtnGenerate.IsEnabled = true;
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            await GenerateNewConfigAsync();
        }

        private void Dns_Checked(object sender, RoutedEventArgs e)
        {
            TriggerConfigRegeneration();
        }

        private void BtnEurope_Click(object sender, RoutedEventArgs e)
        {
            _isEuropeMode = !_isEuropeMode;
            UpdateEuropeButtonVisual();
            if (_isEuropeMode)
            {
                TxtEndpoint.Text = CryptoService.GenerateRandomEuropeEndpoint();
            }
            else
            {
                TxtEndpoint.Text = "engage.cloudflareclient.com:2408";
            }
            ShowAlert(_isEuropeMode ? "Включены европейские эндпоинты (обход ТСПУ)" : "Включены все Anycast эндпоинты", false);
            TriggerConfigRegeneration();
        }

        private void TxtEndpoint_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtEndpoint.SelectAll();
        }

        private void TxtEndpoint_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TxtEndpoint.IsKeyboardFocusWithin)
            {
                TxtEndpoint.Focus();
                TxtEndpoint.SelectAll();
                e.Handled = true;
            }
        }

        private void UpdateEuropeButtonVisual()
        {
            if (BtnEurope == null) return;
            if (_isEuropeMode)
            {
                BtnEurope.Style = (Style)FindResource("ConvexBtnEmerald");
                BtnEurope.Opacity = 1.0;
                BtnEurope.ToolTip = "Европейские эндпоинты (обход ТСПУ) активны. Кликните для переключения на Все адреса.";
            }
            else
            {
                BtnEurope.Style = (Style)FindResource("ConvexBtnSlate");
                BtnEurope.Opacity = 0.65;
                BtnEurope.ToolTip = "Все Anycast эндпоинты. Кликните для переключения на Европейские.";
            }
        }

        private void BtnRandomEndpoint_Click(object sender, RoutedEventArgs e)
        {
            TxtEndpoint.Text = CryptoService.GenerateRandomEndpoint(_isEuropeMode);
        }

        private void Client_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isWiresock = RbClientWiresock?.IsChecked == true;
            bool isClash = RbClientClash?.IsChecked == true;

            if (BtnAwg2Domain != null)
                BtnAwg2Domain.Visibility = isWiresock ? Visibility.Collapsed : Visibility.Visible;

            if (isClash)
            {
                SwExcludeLan.IsEnabled = false;
                SwExcludeLan.IsChecked = false;
                SwKeepalive.IsEnabled = false;
                SwKeepalive.IsChecked = false;
                TxtKeepalive.IsEnabled = false;
            }
            else
            {
                SwExcludeLan.IsEnabled = true;
                SwKeepalive.IsEnabled = true;
                TxtKeepalive.IsEnabled = SwKeepalive.IsChecked == true;
            }

            if (isWiresock && SwAwg2 != null)
            {
                SwAwg2.IsChecked = true;
                ApplyAwgToggleRules(SwAwg2);
            }

            if (!isClash)
            {
                _clashMode = "awg";
            }

            UpdateClashButtonsVisual();
            UpdateAwgPanelsVisibility();
            TriggerConfigRegeneration();
        }

        private void ApplyAwgToggleRules(CheckBox trigger)
        {
            bool isAmnezia = RbClientAwg?.IsChecked == true;

            if (trigger == SwAwg3 && SwAwg3?.IsChecked == true)
            {
                if (SwAwg1 != null) SwAwg1.IsChecked = false;
                if (SwAwg2 != null) SwAwg2.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }
            else if (trigger == SwAwg31 && SwAwg31?.IsChecked == true)
            {
                if (SwAwg1 != null) SwAwg1.IsChecked = false;
                if (SwAwg2 != null) SwAwg2.IsChecked = false;
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
            }
            else if (trigger == SwAwg2 && SwAwg2?.IsChecked == true)
            {
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }
            else if (trigger == SwAwg1 && SwAwg1?.IsChecked == true)
            {
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }

            UpdateAwgPanelsVisibility();
        }

        private void UpdateAwgPanelsVisibility()
        {
            bool isWiresock = RbClientWiresock?.IsChecked == true;

            if (PnlAwg1Options != null)
                PnlAwg1Options.Visibility = SwAwg1?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (PnlWireSockFields != null)
                PnlWireSockFields.Visibility = (isWiresock && SwAwg2?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg2Fields != null)
                PnlAwg2Fields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg2ExtraFields != null)
                PnlAwg2ExtraFields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true && SwAwg2Custom?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg3Options != null)
                PnlAwg3Options.Visibility = SwAwg3?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg31Options != null)
                PnlAwg31Options.Visibility = SwAwg31?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwAwg1_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg1);
            if (PnlAwg1Custom != null)
            {
                if (SwAwg1?.IsChecked == true && RbJunk3?.IsChecked == true)
                {
                    PnlAwg1Custom.Opacity = 1.0;
                    PnlAwg1Custom.IsEnabled = true;
                }
                else
                {
                    PnlAwg1Custom.Opacity = 0.8;
                    PnlAwg1Custom.IsEnabled = false;
                }
            }
            TriggerConfigRegeneration();
        }

        private void SwAwg2_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg2);
            TriggerConfigRegeneration();
        }

        private void SwAwg2Custom_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateAwgPanelsVisibility();
        }

        private void SwAwg3_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg3);
            TriggerConfigRegeneration();
        }

        private void SwAwg31_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg31);
            TriggerConfigRegeneration();
        }

        private void SwKeepalive_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (TxtKeepalive != null)
                TxtKeepalive.IsEnabled = SwKeepalive?.IsChecked == true;
            TriggerConfigRegeneration();
        }

        private void ConfigSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (PnlAwg1Custom != null && (sender == RbJunk1 || sender == RbJunk2 || sender == RbJunk3))
            {
                if (RbJunk3?.IsChecked == true)
                {
                    PnlAwg1Custom.Opacity = 1.0;
                    PnlAwg1Custom.IsEnabled = true;
                    if (string.IsNullOrWhiteSpace(TxtJc.Text)) TxtJc.Text = "4";
                    if (string.IsNullOrWhiteSpace(TxtJmin.Text)) TxtJmin.Text = "40";
                    if (string.IsNullOrWhiteSpace(TxtJmax.Text)) TxtJmax.Text = "70";
                }
                else
                {
                    TxtJc.Text = "";
                    TxtJmin.Text = "";
                    TxtJmax.Text = "";
                    PnlAwg1Custom.Opacity = 0.8;
                    PnlAwg1Custom.IsEnabled = false;
                }
            }

            TriggerConfigRegeneration();
        }

        private void BtnAwg1Random_Click(object sender, RoutedEventArgs e)
        {
            RbJunk3.IsChecked = true;
            if (PnlAwg1Custom != null)
            {
                PnlAwg1Custom.Opacity = 1.0;
                PnlAwg1Custom.IsEnabled = true;
            }
            var (jc, jmin, jmax) = CryptoService.GenerateRandomAwg1();
            TxtJc.Text = jc.ToString();
            TxtJmin.Text = jmin.ToString();
            TxtJmax.Text = jmax.ToString();
            if (SwAwg1 != null)
            {
                SwAwg1.IsChecked = true;
                ApplyAwgToggleRules(SwAwg1);
            }
            TriggerConfigRegeneration();
        }

        private void BtnAwg2Random_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientWiresock.IsChecked == true)
            {
                TxtWireSockId.Text = CryptoService.GetRandomWireSockDomain();
            }
            else
            {
                TxtI1.Text = CryptoService.GetRandomAwg2I1();
            }
            if (SwAwg2 != null)
            {
                SwAwg2.IsChecked = true;
                ApplyAwgToggleRules(SwAwg2);
            }
            UpdateAwgPanelsVisibility();
            TriggerConfigRegeneration();
        }

        private void BtnAwg2Domain_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DomainInputDialog(TxtWireSockId.Text)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string i1 = QuicPacketGenerator.GenerateI1FromDomain(dialog.Domain);
                if (!string.IsNullOrEmpty(i1))
                {
                    TxtI1.Text = i1;
                    if (SwAwg2 != null)
                    {
                        SwAwg2.IsChecked = true;
                        ApplyAwgToggleRules(SwAwg2);
                    }
                    UpdateAwgPanelsVisibility();
                    ShowAlert($"I1 успешно сгенерирован из домена {dialog.Domain}", false);
                    TriggerConfigRegeneration();
                }
                else
                {
                    ShowAlert("Не удалось сгенерировать I1 из домена", true);
                }
            }
        }

        private void BtnAwg3Random_Click(object sender, RoutedEventArgs e)
        {
            var (cpa, mha, kt, rat, rkat, rt) = CryptoService.GenerateRandomAwg3();
            TxtCpa.Text = cpa;
            TxtMha.Text = mha;
            TxtKt.Text = kt;
            TxtRat.Text = rat;
            TxtRkat.Text = rkat;
            TxtRt.Text = rt;
            if (SwAwg3 != null)
            {
                SwAwg3.IsChecked = true;
                ApplyAwgToggleRules(SwAwg3);
            }
            TriggerConfigRegeneration();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_currentApiData == null || string.IsNullOrWhiteSpace(TxtConfig.Text))
            {
                ShowAlert("Сначала нажмите «⚡ Сгенерировать»", true);
                return;
            }

            Clipboard.SetText(TxtConfig.Text.TrimStart('\uFEFF'));
            ShowAlert("Конфигурация скопирована в буфер обмена!", false);
        }

        private void BtnDownloadSingle_Click(object sender, RoutedEventArgs e)
        {
            if (_currentApiData == null || string.IsNullOrWhiteSpace(TxtConfig.Text))
            {
                ShowAlert("Сначала нажмите «⚡ Сгенерировать»", true);
                return;
            }

            bool isClash = RbClientClash.IsChecked == true;
            string ext = isClash ? "yaml" : "conf";
            string filter = isClash ? "Clash YAML (*.yaml)|*.yaml" : "WireGuard Config (*.conf)|*.conf";

            string basePrefix = _isEuropeMode ? "WARP_EURO" : "WARP";
            int currentCount = _isEuropeMode ? _warpEuroDownloadCount : _warpDownloadCount;
            string defaultName = currentCount == 0 ? $"{basePrefix}.{ext}" : $"{basePrefix}{currentCount}.{ext}";

            var sfd = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = filter,
                DefaultExt = ext
            };

            if (sfd.ShowDialog() == true)
            {
                if (_isEuropeMode) _warpEuroDownloadCount++;
                else _warpDownloadCount++;

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(sfd.FileName, TxtConfig.Text.TrimStart('\uFEFF'), utf8NoBom);
                ShowAlert($"Файл сохранен: {Path.GetFileName(sfd.FileName)}", false);
            }
        }

        private async void BtnDownloadEurope10_Click(object sender, RoutedEventArgs e)
        {
            await DownloadBatchArchiveAsync(true);
        }

        private async void BtnDownloadAnycast10_Click(object sender, RoutedEventArgs e)
        {
            await DownloadBatchArchiveAsync(false);
        }

        private async Task DownloadBatchArchiveAsync(bool isEurope)
        {
            var btnTarget = isEurope ? BtnDownloadEurope10 : BtnDownloadAnycast10;
            var originalContent = btnTarget.Content;
            SetActionButtonsEnabled(false);

            try
            {
                var keysList = new List<WarpApiResponse>();
                for (int i = 0; i < 10; i++)
                {
                    btnTarget.Content = $"⏳ Запрос ({i + 1}/10)...";
                    var keys = await _apiService.FetchWarpKeysAsync();
                    keysList.Add(keys);
                    if (i < 9) await Task.Delay(250);
                }

                btnTarget.Content = "📦 Сборка архива...";
                bool isClash = RbClientClash.IsChecked == true;
                string ext = isClash ? "yaml" : "conf";

                var usedEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                byte[] zipBytes;
                using (var ms = new MemoryStream())
                {
                    using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                    {
                        var utf8NoBom = new UTF8Encoding(false);
                        for (int i = 0; i < 10; i++)
                        {
                            var confSettings = GetCurrentSettings();

                            string ep;
                            if (isEurope)
                            {
                                int attempts = 0;
                                do
                                {
                                    ep = CryptoService.GenerateDynamicEuropePrefixEndpoint();
                                    attempts++;
                                } while (usedEndpoints.Contains(ep) && attempts < 25);
                            }
                            else
                            {
                                int attempts = 0;
                                do
                                {
                                    ep = CryptoService.GenerateRandomAnycastEndpoint();
                                    attempts++;
                                } while (usedEndpoints.Contains(ep) && attempts < 25);
                            }
                            usedEndpoints.Add(ep);
                            confSettings.Endpoint = ep;

                            string confContent = _configBuilder.BuildConfigString(keysList[i], confSettings);
                            string entryName = isEurope 
                                ? (i == 0 ? $"WARP_EURO.{ext}" : $"WARP_EURO{i}.{ext}")
                                : (i == 0 ? $"WARP.{ext}" : $"WARP{i}.{ext}");

                            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                            using var entryStream = entry.Open();
                            using var writer = new StreamWriter(entryStream, utf8NoBom);
                            writer.Write(confContent.TrimStart('\uFEFF'));
                        }
                    }
                    zipBytes = ms.ToArray();
                }

                string defaultZipName = isEurope ? "WARP_EURO_10.zip" : "WARP_10.zip";
                var sfd = new SaveFileDialog
                {
                    FileName = defaultZipName,
                    Filter = "ZIP Archive (*.zip)|*.zip",
                    DefaultExt = "zip"
                };

                if (sfd.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(sfd.FileName, zipBytes);
                    ShowAlert($"Архив сохранен: {Path.GetFileName(sfd.FileName)}", false);
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка создания архива: {ex.Message}", true);
            }
            finally
            {
                btnTarget.Content = originalContent;
                SetActionButtonsEnabled(true);
            }
        }

        private void SetActionButtonsEnabled(bool enabled)
        {
            BtnGenerate.IsEnabled = enabled;
            BtnDownloadSingle.IsEnabled = enabled;
            BtnDownloadEurope10.IsEnabled = enabled;
            BtnDownloadAnycast10.IsEnabled = enabled;
        }

        private void ShowAlert(string message, bool isError)
        {
            TxtAlert.Text = message;
            TxtAlert.Foreground = new SolidColorBrush(isError ? Color.FromRgb(185, 28, 28) : Color.FromRgb(21, 128, 61));
            AlertBox.Background = new SolidColorBrush(isError ? Color.FromRgb(254, 226, 226) : Color.FromRgb(220, 252, 231));
            AlertBox.BorderBrush = new SolidColorBrush(isError ? Color.FromRgb(252, 165, 165) : Color.FromRgb(187, 247, 208));
            AlertBox.BorderThickness = new Thickness(1);
            AlertBox.Visibility = Visibility.Visible;
            TxtCredits.Visibility = Visibility.Collapsed;

            _alertTimer?.Stop();
            _alertTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _alertTimer.Tick += (s, ev) =>
            {
                _alertTimer.Stop();
                AlertBox.Visibility = Visibility.Collapsed;
                TxtCredits.Visibility = Visibility.Visible;
            };
            _alertTimer.Start();
        }

        private void UpdateClashButtonsVisual()
        {
            if (BtnMasque == null || BtnAwgMasque == null) return;

            bool isClash = RbClientClash?.IsChecked == true;
            if (!isClash)
            {
                BtnMasque.Opacity = 0.55;
                BtnAwgMasque.Opacity = 0.55;
                return;
            }

            if (_clashMode == "masque")
            {
                BtnMasque.Opacity = 1.0;
                BtnAwgMasque.Opacity = 0.45;
            }
            else if (_clashMode == "hybrid")
            {
                BtnMasque.Opacity = 0.45;
                BtnAwgMasque.Opacity = 1.0;
            }
            else
            {
                BtnMasque.Opacity = 0.55;
                BtnAwgMasque.Opacity = 0.55;
            }
        }

        private void BtnMasque_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientClash?.IsChecked == true && _clashMode == "masque")
            {
                _clashMode = "awg";
                ShowAlert("Режим Clash: стандартный AWG", false);
            }
            else
            {
                _clashMode = "masque";
                if (RbClientClash != null) RbClientClash.IsChecked = true;
                ShowAlert("Режим Clash: Masque (HTTP/3)", false);
            }

            UpdateClashButtonsVisual();
            TriggerConfigRegeneration();
        }

        private void BtnAwgMasque_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientClash?.IsChecked == true && _clashMode == "hybrid")
            {
                _clashMode = "awg";
                ShowAlert("Режим Clash: стандартный AWG", false);
            }
            else
            {
                _clashMode = "hybrid";
                if (RbClientClash != null) RbClientClash.IsChecked = true;
                ShowAlert("Режим Clash: Awg+Masque (Гибрид)", false);
            }

            UpdateClashButtonsVisual();
            TriggerConfigRegeneration();
        }

        // ======================= СКАНЕР ЭНДПОИНТОВ =======================

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning)
            {
                return;
            }

            // Разовое согласие на загрузку и применение стороннего инструмента.
            if (!_scannerService.IsWarpscoutInstalled)
            {
                var consent = MessageBox.Show(
                    "Для реальной проверки эндпоинтов используется сторонний инструмент " +
                    _scannerService.ToolCredit + ".\n\n" +
                    "При первом запуске он будет загружен (~5 МБ) с GitHub и проверен по SHA-256, " +
                    "затем зарегистрирует временный аккаунт WARP и поднимет реальные туннели с этого " +
                    "компьютера, чтобы измерить пинг и определить рабочие эндпоинты.\n\n" +
                    "Продолжить?",
                    "Сканер эндпоинтов WARP",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (consent != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _scanCts = new System.Threading.CancellationTokenSource();
            SetScanningState(true);

            var progress = new Progress<string>(msg => ShowAlert(msg, false));
            try
            {
                var results = await _scannerService.ScanEndpointsAsync(_foreignOnly, progress, _scanCts.Token);

                if (results.Count > 0)
                {
                    LstScanResults.ItemsSource = results;
                    PnlScanResults.Visibility = Visibility.Visible;
                    ShowAlert($"Готово: найдено рабочих эндпоинтов — {results.Count}. Кликните для выбора.", false);
                }
                else
                {
                    PnlScanResults.Visibility = Visibility.Collapsed;
                    ShowAlert("Рабочих эндпоинтов не найдено. Попробуйте ещё раз позже.", true);
                }
            }
            catch (OperationCanceledException)
            {
                ShowAlert("Сканирование отменено.", true);
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message, true);
            }
            finally
            {
                SetScanningState(false);
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
        }

        private void BtnForeignOnly_Click(object sender, RoutedEventArgs e)
        {
            _foreignOnly = !_foreignOnly;
            BtnForeignOnly.Opacity = _foreignOnly ? 1.0 : 0.55;
        }

        private void EndpointRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is WarpEndpointItem item)
            {
                TxtEndpoint.Text = item.Endpoint;
                TriggerConfigRegeneration();
                ShowAlert($"Выбран эндпоинт {item.Endpoint} ({item.PingMs} ms).", false);
            }
        }

        private void BtnCloseScanResults_Click(object sender, RoutedEventArgs e)
        {
            PnlScanResults.Visibility = Visibility.Collapsed;
        }

        private void SetScanningState(bool scanning)
        {
            _isScanning = scanning;
            BtnScan.IsEnabled = !scanning;
            BtnStop.IsEnabled = scanning;
            BtnForeignOnly.IsEnabled = !scanning;
            Mouse.OverrideCursor = scanning ? Cursors.Wait : null;
        }
    }
}
