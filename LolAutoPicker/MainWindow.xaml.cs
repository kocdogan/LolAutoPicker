using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LolAutoPicker.Helpers;
using LolAutoPicker.Services;

namespace LolAutoPicker
{
    public class StatDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string WinRateStr { get; set; } = string.Empty;
        public string ScoreStr { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public static bool IsEnglish = false;

        private readonly Dictionary<string, int> spellsTR = new Dictionary<string, int>
        {
            {"Sıçra", 4}, {"Tutuştur", 14}, {"Çarp", 11}, {"Işınlan", 12},
            {"Hayalet", 6}, {"Şifa", 7}, {"Bitkinlik", 3}, {"Bariyer", 21}, {"Arındır", 1}
        };

        private readonly Dictionary<string, int> spellsEN = new Dictionary<string, int>
        {
            {"Flash", 4}, {"Ignite", 14}, {"Smite", 11}, {"Teleport", 12},
            {"Ghost", 6}, {"Heal", 7}, {"Exhaust", 3}, {"Barrier", 21}, {"Cleanse", 1}
        };

        public MainWindow()
        {
            InitializeComponent();
            Logger.Log = LogToUi;

            LoLClient.OnClientDisconnected += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = IsEnglish ? "CLIENT: DISCONNECTED" : "İSTEMCİ: BAĞLI DEĞİL";
                    txtStatus.Foreground = Brushes.Red;
                    Logger.Info(IsEnglish ? "Client connection lost! Stopped searching." : "İstemci bağlantısı koptu! İşlemler durduruldu.");
                });
            };

            LoadSpellsToUI();

            this.Loaded += async (s, e) =>
            {
                StatsService.Load();
                await ChampionService.Load();
                LoadStatsSilently();
            };
        }

        private void LoadSpellsToUI()
        {
            int selectedIndex1 = cmbSpell1.SelectedIndex > -1 ? cmbSpell1.SelectedIndex : 0;
            int selectedIndex2 = cmbSpell2.SelectedIndex > -1 ? cmbSpell2.SelectedIndex : 1;

            var currentSpells = IsEnglish ? spellsEN : spellsTR;

            cmbSpell1.ItemsSource = null;
            cmbSpell2.ItemsSource = null;

            cmbSpell1.ItemsSource = currentSpells;
            cmbSpell2.ItemsSource = currentSpells;

            cmbSpell1.SelectedIndex = selectedIndex1;
            cmbSpell2.SelectedIndex = selectedIndex2;
        }

        private void LogToUi(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                txtLogs.Text += msg + Environment.NewLine;
                LogScroll.ScrollToEnd();
            });
        }

        private void LoadStatsSilently()
        {
            lstStats.ItemsSource = null;
            var stats = StatsService.GetStats();
            if (stats == null || !stats.Any()) return;

            var displayList = new List<StatDisplayItem>();
            foreach (var c in stats.OrderByDescending(x => x.WinRate))
            {
                displayList.Add(new StatDisplayItem
                {
                    Name = ChampionService.GetName(c.ChampId) ?? "Unknown",
                    ImageUrl = ChampionService.GetImageUrl(c.ChampId) ?? "",
                    WinRateStr = $"%{c.WinRate * 100:F0}",
                    ScoreStr = IsEnglish ? $"({c.Wins}W - {c.Losses}L)" : $"({c.Wins}G - {c.Losses}M)"
                });
            }
            lstStats.ItemsSource = displayList;
        }

        private void BtnLangTR_Click(object sender, RoutedEventArgs e)
        {
            IsEnglish = false;

            BtnLangTR.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF007F"));
            BtnLangTR.Foreground = Brushes.White;
            BtnLangTR.BorderBrush = Brushes.White;

            BtnLangEN.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
            BtnLangEN.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF41"));
            BtnLangEN.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF41"));

            UpdateLanguageUI();
        }

        private void BtnLangEN_Click(object sender, RoutedEventArgs e)
        {
            IsEnglish = true;

            BtnLangEN.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF007F"));
            BtnLangEN.Foreground = Brushes.White;
            BtnLangEN.BorderBrush = Brushes.White;

            BtnLangTR.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
            BtnLangTR.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF41"));
            BtnLangTR.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF41"));

            UpdateLanguageUI();
        }

        private void UpdateLanguageUI()
        {
            lblPlayerSetup.Text = IsEnglish ? "[ PLAYER SETUP ]" : "[ OYUNCU AYARLARI ]";
            lblQueue.Text = IsEnglish ? "> Queue Type:" : "> Kuyruk Türü:";
            lblBans.Text = IsEnglish ? "> Bans:" : "> Yasaklamalar (Ban):";
            lblPicks.Text = IsEnglish ? "> Picks:" : "> Seçimler (Pick):";
            lblRoles.Text = IsEnglish ? "> Roles (1st & 2nd):" : "> Koridor (1. ve 2.):";
            lblSpells.Text = IsEnglish ? "> Spells:" : "> Büyüler:";
            lblRunes.Text = IsEnglish ? "> Rune Page:" : "> Rün Sayfası:";

            BtnCheckClient.Content = IsEnglish ? "CHECK CLIENT" : "İSTEMCİYİ KONTROL ET";
            BtnFindMatch.Content = IsEnglish ? "START GAME" : "MAÇ ARAT";
            BtnRefreshStats.Content = IsEnglish ? "RELOAD" : "YENİLE";

            lblTerminal.Text = IsEnglish ? "[ TERMINAL OUTPUT ]" : "[ TERMİNAL ÇIKTISI ]";
            lblLeaderboard.Text = IsEnglish ? "[ LEADERBOARD ]" : "[ İSTATİSTİKLER ]";

            if (txtStatus.Foreground == Brushes.Red)
                txtStatus.Text = IsEnglish ? "CLIENT: DISCONNECTED" : "İSTEMCİ: BAĞLI DEĞİL";
            else
                txtStatus.Text = IsEnglish ? "CLIENT: CONNECTED" : "İSTEMCİ: BAĞLI";

            LoadSpellsToUI();

            LoadStatsSilently();
        }

        private void cmbQueue_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private async void BtnCheckClient_Click(object sender, RoutedEventArgs e)
        {
            bool ok = LoLClient.InitFromLockfile();

            if (ok)
            {
                Logger.Info(IsEnglish ? "CLIENT CONNECTED" : "İSTEMCİ BAĞLANTISI BAŞARILI");
                txtStatus.Text = IsEnglish ? "CLIENT: CONNECTED" : "İSTEMCİ: BAĞLI";
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF41"));

                Logger.Info(IsEnglish ? "Fetching rune pages..." : "Hesap rünleri çekiliyor...");
                var runes = await LoLClient.GetRunePages();

                if (runes != null)
                {
                    cmbRunes.ItemsSource = runes;
                    if (runes.Any())
                    {
                        cmbRunes.SelectedIndex = 0;
                        Logger.Info(IsEnglish ? $"{runes.Count} rune pages loaded." : $"{runes.Count} adet rün sayfası başarıyla yüklendi.");
                    }
                }
            }
            else
            {
                Logger.Info(IsEnglish ? "CLIENT NOT FOUND" : "İSTEMCİ BULUNAMADI");
                txtStatus.Text = IsEnglish ? "CLIENT: DISCONNECTED" : "İSTEMCİ: BAĞLI DEĞİL";
                txtStatus.Foreground = Brushes.Red;
            }
        }

        private void BtnRefreshStats_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(IsEnglish ? "Refreshing stats..." : "Kayıtlı istatistikler yenileniyor...");
            StatsService.Load();
            LoadStatsSilently();

            var stats = StatsService.GetStats();
            if (stats == null || !stats.Any())
            {
                Logger.Info(IsEnglish ? "No stats found." : "Kayıtlı istatistik bulunamadı.");
            }
        }

        private async void BtnFindMatch_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(async () =>
            {
                var queueType = Dispatcher.Invoke(() => { var s = cmbQueue.SelectedItem as ComboBoxItem; return s?.Content?.ToString() == "Flex" ? "flex" : "solo"; });
                var primary = Dispatcher.Invoke(() => { var s = cmbRole1.SelectedItem as ComboBoxItem; return s?.Content?.ToString() ?? "TOP"; });
                var secondary = Dispatcher.Invoke(() => { var s = cmbRole2.SelectedItem as ComboBoxItem; return s?.Content?.ToString() ?? "MID"; });

                var banNames = new List<string> { Dispatcher.Invoke(() => txtBan1.Text ?? ""), Dispatcher.Invoke(() => txtBan2.Text ?? ""), Dispatcher.Invoke(() => txtBan3.Text ?? "") };
                var pickNames = new List<string> { Dispatcher.Invoke(() => txtPick1.Text ?? ""), Dispatcher.Invoke(() => txtPick2.Text ?? ""), Dispatcher.Invoke(() => txtPick3.Text ?? "") };

                var bans = banNames.Select(ChampionService.GetId).Where(x => x != -1).ToList();
                var picks = pickNames.Select(ChampionService.GetId).Where(x => x != -1).ToList();

                int spell1 = Dispatcher.Invoke(() => cmbSpell1.SelectedValue != null ? (int)cmbSpell1.SelectedValue : -1);
                int spell2 = Dispatcher.Invoke(() => cmbSpell2.SelectedValue != null ? (int)cmbSpell2.SelectedValue : -1);
                int runeId = Dispatcher.Invoke(() => cmbRunes.SelectedValue != null ? (int)cmbRunes.SelectedValue : -1);

                if (!LoLClient.InitFromLockfile())
                {
                    Logger.Info(IsEnglish ? "Error: Client not found!" : "Hata: İstemci bulunamadı!");
                    return;
                }

                bool posOk = await LoLClient.SetPositionPreferences(primary, secondary);
                Logger.Info(posOk ? (IsEnglish ? $"Roles set: {primary}, {secondary}" : $"Tercihler ayarlandı: {primary}, {secondary}") : (IsEnglish ? "Role selection failed!" : "Koridor seçimi başarısız!"));

                bool ok = await LoLClient.StartQueue(queueType);
                if (!ok)
                {
                    Logger.Info(IsEnglish ? "Error: Could not start queue!" : "Hata: Queue başlatılamadı!");
                    return;
                }

                _ = Task.Run(() => LoLClient.ListenReadyCheck());

                Logger.Info(IsEnglish ? "Searching for match..." : "Maç aranıyor...");

                await LoLClient.RunChampSelect(bans, picks, spell1, spell2, runeId);
                _ = Task.Run(async () => await LoLClient.WaitForGameEndAndRecord());
            });
        }
    }
}