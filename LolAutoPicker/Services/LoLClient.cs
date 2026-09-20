using LolAutoPicker.Helpers;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace LolAutoPicker.Services
{
    public class RunePage
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public static class LoLClient
    {
        private static HttpClient? client;

        public static event Action? OnClientDisconnected;

        public static void HandleDisconnect()
        {
            if (client != null)
            {
                client = null; 
                OnClientDisconnected?.Invoke(); 
            }
        }

        private static List<string> GetDynamicLockfilePaths()
        {
            var paths = new List<string>();

            try
            {
                var processes = Process.GetProcessesByName("LeagueClientUx");
                if (processes.Length > 0)
                {
                    string? exePath = processes[0].MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        string? dir = Path.GetDirectoryName(exePath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            paths.Add(Path.Combine(dir, "lockfile"));
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Riot Games, Inc\League of Legends"))
                {
                    if (key != null)
                    {
                        string? location = key.GetValue("Location")?.ToString();
                        if (!string.IsNullOrEmpty(location))
                        {
                            paths.Add(Path.Combine(location, "lockfile"));
                        }
                    }
                }
            }
            catch { }

            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo drive in drives.Where(d => d.IsReady))
                {
                    string root = drive.RootDirectory.FullName; 

                    paths.Add(Path.Combine(root, @"Riot Games\League of Legends\lockfile"));
                    paths.Add(Path.Combine(root, @"Riot Games\Riot Client\lockfile"));
                }
            }
            catch { }

            return paths.Distinct().ToList();
        }

        public static bool InitFromLockfile()
        {

            if (client != null) return true;


            var possiblePaths = GetDynamicLockfilePaths();


            foreach (string path in possiblePaths)
            {

                if (File.Exists(path))
                {
                    try
                    {
                        string content;
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var sr = new StreamReader(fs))
                        {
                            content = sr.ReadToEnd();
                        }

                        var p = content.Split(':');
                        int port = int.Parse(p[2]);
                        string token = p[3];

                        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (msg, cert, chain, err) => true };
                        client = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{port}/") };

                        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes("riot:" + token));
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                        Logger.Info(MainWindow.IsEnglish ? "CLIENT CONNECTED (lockfile ok)" : "İSTEMCİ BAĞLANTISI BAŞARILI (lockfile ok)");
                        return true; 
                    }
                    catch (Exception ex)
                    {
                        Logger.Info(MainWindow.IsEnglish ? $"Lockfile read error ({path}): " + ex.Message : $"Lockfile okuma hatası ({path}): " + ex.Message);
                    }
                }
            }

            Logger.Info(MainWindow.IsEnglish ? "Lockfile not found or the game is currently closed." : "Lockfile bulunamadı veya oyun şu anda kapalı.");
            return false;
        }

        public static async Task<bool> SetPositionPreferences(string primary, string secondary)
        {
            if (client == null) return false;
            try
            {
                var body = new { firstPreference = primary.ToUpper(), secondPreference = secondary.ToUpper() };
                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                var res = await client.PutAsync("lol-lobby/v2/lobby/members/localMember/position-preferences", content);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Info(MainWindow.IsEnglish ? "Position Preferences error: " + ex.Message : "Position Preferences hata: " + ex.Message);
                return false;
            }
        }

        public static async Task<bool> StartQueue(string type)
        {
            if (client == null) return false;
            try
            {
                int q = type == "flex" ? 440 : 420;
                var lobbyContent = new StringContent($"{{\"queueId\":{q}}}", Encoding.UTF8, "application/json");

                await client.PostAsync("lol-lobby/v2/lobby", lobbyContent);
                await Task.Delay(1000);

                var res = await client.PostAsync("lol-lobby/v2/lobby/matchmaking/search", null);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Info(MainWindow.IsEnglish ? "StartQueue error: " + ex.Message : "StartQueue hata: " + ex.Message);
                return false;
            }
        }

        public static async Task ListenReadyCheck()
        {
            bool acceptedCurrent = false;

            while (true)
            {
                if (client == null) break;
                try
                {
                    var res = await client.GetAsync("lol-matchmaking/v1/ready-check");

                    if (!res.IsSuccessStatusCode)
                    {
                        acceptedCurrent = false;
                        await Task.Delay(1000);
                        continue;
                    }

                    var json = JObject.Parse(await res.Content.ReadAsStringAsync());
                    string? state = json["state"]?.ToString();

                    if (state == "InProgress")
                    {
                        if (!acceptedCurrent)
                        {
                            Logger.Info("READY CHECK → ACCEPT");
                            await client.PostAsync("lol-matchmaking/v1/ready-check/accept", null);
                            Logger.Info(MainWindow.IsEnglish ? "Match accepted!" : "Maç kabul edildi!");
                            acceptedCurrent = true;
                        }
                    }
                    else
                    {
                        acceptedCurrent = false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info(MainWindow.IsEnglish ? "ReadyCheck error: " + ex.Message : "ReadyCheck hata: " + ex.Message);
                }

                await Task.Delay(1000);
            }
        }


        public static async Task RunChampSelect(List<int> bans, List<int> picks, int spell1Id, int spell2Id, int runePageId)
        {
            bool setupCompleted = false;
            int myLockedChampId = -1;

            HashSet<int> completedActionsLog = new HashSet<int>();
            HashSet<string> attemptedLogs = new HashSet<string>();

            while (true)
            {
                if (client == null) break;
                try
                {
                    var r = await client.GetAsync("lol-champ-select/v1/session");

                    if (!r.IsSuccessStatusCode)
                    {
                        var flowRes = await client.GetAsync("lol-gameflow/v1/gameflow-phase");
                        if (flowRes.IsSuccessStatusCode)
                        {
                            string phase = (await flowRes.Content.ReadAsStringAsync()).Replace("\"", "");
                            if (phase == "InProgress")
                            {
                                Logger.Info(MainWindow.IsEnglish ? "Entering loading screen. Listener stopped." : "Oyun yükleme ekranına geçildi. Dinleyici durduruluyor...");
                                break;
                            }
                        }
                        await Task.Delay(1500);
                        continue;
                    }

                    var session = Newtonsoft.Json.Linq.JObject.Parse(await r.Content.ReadAsStringAsync());
                    if (session["isChampSelectCompleted"]?.Value<bool>() == true)
                    {
                        Logger.Info(MainWindow.IsEnglish ? "Champ select completed. Process stopped." : "Şampiyon seçimi tamamlandı. İşlemler durduruldu.");
                        break;
                    }

                    var localCellId = session["localPlayerCellId"]?.Value<int>();
                    var actions = session["actions"] as Newtonsoft.Json.Linq.JArray;
                    bool actionInProgress = false;

                    string myPosition = "";
                    if (session["myTeam"] is Newtonsoft.Json.Linq.JArray myTeamArray)
                    {
                        foreach (var p in myTeamArray)
                        {
                            if (p["cellId"]?.Value<int>() == localCellId)
                            {
                                myPosition = p["assignedPosition"]?.ToString().ToLower() ?? "";
                                break;
                            }
                        }
                    }

                    HashSet<int> disabledChamps = new HashSet<int>();

                    if (actions != null)
                    {
                        foreach (var group in actions)
                        {
                            foreach (var a in group)
                            {
                                if (a["completed"]?.Value<bool>() == true)
                                {
                                    int cId = a["championId"]?.Value<int>() ?? 0;
                                    if (cId > 0) disabledChamps.Add(cId);
                                }
                            }
                        }
                    }

                    if (actions != null)
                    {
                        foreach (var group in actions)
                        {
                            if (group is not Newtonsoft.Json.Linq.JArray groupArray) continue;

                            foreach (var a in groupArray)
                            {
                                int? actionId = a["id"]?.Value<int>();
                                string? type = a["type"]?.ToString();
                                bool isInProgress = a["isInProgress"]?.Value<bool>() ?? false;
                                int actorCellId = a["actorCellId"]?.Value<int>() ?? -1;
                                bool completed = a["completed"]?.Value<bool>() ?? false;
                                int currentChampId = a["championId"]?.Value<int>() ?? 0;

                                if (!actionId.HasValue || string.IsNullOrEmpty(type) || actorCellId != localCellId) continue;

                                if (isInProgress)
                                {
                                    actionInProgress = true;
                                    List<int> targetList = type == "ban" ? bans : (type == "pick" ? picks : new List<int>());

                                    foreach (int champId in targetList)
                                    {
                                        if (champId <= 0) continue;

                                        if (disabledChamps.Contains(champId)) continue;

                                        string logKey = $"{actionId}_{champId}";
                                        if (!attemptedLogs.Contains(logKey))
                                        {
                                            Logger.Info(MainWindow.IsEnglish ? $"Attempting {type.ToUpper()} → {ChampionService.GetName(champId)}..." : $"{type.ToUpper()} deneniyor → {ChampionService.GetName(champId)}...");
                                            attemptedLogs.Add(logKey);
                                        }

                                        bool success = await TryAction(actionId.Value, champId);

                                        if (success)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            disabledChamps.Add(champId);
                                        }
                                    }
                                }
                                else if (completed)
                                {
                                    if (!completedActionsLog.Contains(actionId.Value))
                                    {
                                        completedActionsLog.Add(actionId.Value);
                                        Logger.Info(MainWindow.IsEnglish ? $"SUCCESS → {type.ToUpper()} Locked: {ChampionService.GetName(currentChampId)}" : $"BAŞARILI → {type.ToUpper()} Kilitlendi: {ChampionService.GetName(currentChampId)}");
                                    }

                                    if (type == "pick" && currentChampId > 0)
                                    {
                                        myLockedChampId = currentChampId;
                                    }
                                }
                            }
                        }
                    }

                    if (!setupCompleted && myLockedChampId > 0 && !actionInProgress)
                    {
                        int finalSpell1 = spell1Id;
                        int finalSpell2 = spell2Id;

                        if (!string.IsNullOrEmpty(myPosition) && myPosition != "jungle")
                        {
                            if (finalSpell1 == 11) finalSpell1 = 14;
                            if (finalSpell2 == 11) finalSpell2 = 14;
                            Logger.Info(MainWindow.IsEnglish ? $"Role '{myPosition.ToUpper()}' assigned. Smite changed to Ignite." : $"Rol '{myPosition.ToUpper()}' geldi. Çarp, Tutuştur yapıldı.");
                        }

                        await SetSummonerSpells(finalSpell1, finalSpell2);
                        Logger.Info(MainWindow.IsEnglish ? "Summoner spells configured." : "Sihirdar Büyüleri ayarlandı.");

                        await SetActiveRunePage(runePageId);
                        Logger.Info(MainWindow.IsEnglish ? "Selected rune page activated." : "Listeden seçilen rün sayfası aktif edildi.");

                        setupCompleted = true;
                    }
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    HandleDisconnect();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException is System.Net.Sockets.SocketException || ex.Message.Contains("connection"))
                    {
                        HandleDisconnect();
                        break;
                    }

                    Logger.Info(MainWindow.IsEnglish ? $"ChampSelect error: {ex.Message}" : $"ChampSelect hata: {ex.Message}");
                }

                await Task.Delay(800);
            }
        }

        private static async Task<bool> TryAction(int actionId, int champId)
        {
            if (client == null || champId <= 0) return false;
            try
            {
                var hoverBody = new { championId = champId, completed = false };
                var hoverContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(hoverBody), Encoding.UTF8, "application/json");
                var hoverRes = await client.PatchAsync($"lol-champ-select/v1/session/actions/{actionId}", hoverContent);

                if (!hoverRes.IsSuccessStatusCode)
                {
                    return false; 
                }

                await Task.Delay(200);

                var lockBody = new { championId = champId, completed = true };
                var lockContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(lockBody), Encoding.UTF8, "application/json");
                var lockRes = await client.PatchAsync($"lol-champ-select/v1/session/actions/{actionId}", lockContent);

                return lockRes.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<List<RunePage>> GetRunePages()
        {
            var list = new List<RunePage>();
            if (client == null) return list;
            try
            {
                var res = await client.GetAsync("lol-perks/v1/pages");
                if (res.IsSuccessStatusCode)
                {
                    var array = JArray.Parse(await res.Content.ReadAsStringAsync());
                    foreach (var item in array)
                    {
                        if (item["isEditable"]?.Value<bool>() == true)
                        {
                            list.Add(new RunePage
                            {
                                Id = item["id"].Value<int>(),
                                Name = item["name"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info(MainWindow.IsEnglish ? "Error fetching runes: " + ex.Message : "Rünler çekilirken hata: " + ex.Message);
            }
            return list;
        }

        public static async Task<bool> SetActiveRunePage(int pageId)
        {
            if (client == null || pageId == -1) return false;
            try
            {
                var getPage = await client.GetAsync($"lol-perks/v1/pages/{pageId}");
                if (getPage.IsSuccessStatusCode)
                {
                    string pageData = await getPage.Content.ReadAsStringAsync();
                    var content = new StringContent(pageData, Encoding.UTF8, "application/json");
                    var res = await client.PutAsync("lol-perks/v1/currentpage", content);
                    return res.IsSuccessStatusCode;
                }
                return false;
            }
            catch { return false; }
        }

        public static async Task<bool> SetSummonerSpells(int spell1Id, int spell2Id)
        {
            if (client == null || spell1Id == -1 || spell2Id == -1) return false;
            try
            {
                var body = new { spell1Id = spell1Id, spell2Id = spell2Id };
                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var res = await client.PatchAsync("lol-champ-select/v1/session/my-selection", content);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> DoAction(int actionId, int champId)
        {
            if (client == null) return false;
            try
            {
                var content = new StringContent(
                    $"{{\"championId\":{champId},\"completed\":true}}",
                    Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(
                    new HttpMethod("PATCH"),
                    $"lol-champ-select/v1/session/actions/{actionId}")
                {
                    Content = content
                };

                var res = await client.SendAsync(request);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Info(MainWindow.IsEnglish ? "DoAction error: " + ex.Message : "DoAction hata: " + ex.Message);
                return false;
            }
        }

        public static async Task WaitForGameEndAndRecord()
        {
            Logger.Info(MainWindow.IsEnglish ? "Waiting for the game to start..." : "Oyunun başlaması bekleniyor...");

            bool gameStarted = false;
            while (!gameStarted)
            {
                await Task.Delay(10000);
                try
                {
                    var res = await client.GetAsync("lol-gameflow/v1/gameflow-phase");
                    if (res.IsSuccessStatusCode)
                    {
                        string phase = (await res.Content.ReadAsStringAsync()).Replace("\"", "");
                        if (phase == "InProgress")
                        {
                            Logger.Info(MainWindow.IsEnglish ? "Game started. Application is in passive mode until the match ends..." : "Oyun başladı. Uygulama maç bitene kadar pasif modda...");
                            gameStarted = true;
                        }
                    }
                }
                catch { }
            }

            while (true)
            {
                await Task.Delay(15000);
                try
                {
                    var res = await client.GetAsync("lol-gameflow/v1/gameflow-phase");
                    if (res.IsSuccessStatusCode)
                    {
                        string phase = (await res.Content.ReadAsStringAsync()).Replace("\"", "");
                        if (phase == "EndOfGame" || phase == "PreEndOfGame" || phase == "Lobby" || phase == "None")
                        {
                            Logger.Info(MainWindow.IsEnglish ? "Match ended. Waiting for stats to be processed by the server (20 sec)..." : "Maç bitti. İstatistiklerin sunucuya işlenmesi bekleniyor (20 sn)...");
                            break;
                        }
                    }
                }
                catch { }
            }

            await Task.Delay(20000);
            await RecordLastMatch();
        }

        private static async Task RecordLastMatch()
        {
            if (client == null)
            {
                Logger.Info(MainWindow.IsEnglish ? "Save Error: Client connection lost." : "Kayıt Hatası: Client bağlantısı kopmuş.");
                return;
            }

            try
            {
                Logger.Info(MainWindow.IsEnglish ? "Fetching user info..." : "Kullanıcı bilgileri alınıyor...");
                var meRes = await client.GetAsync("lol-summoner/v1/current-summoner");
                if (!meRes.IsSuccessStatusCode)
                {
                    Logger.Info(MainWindow.IsEnglish ? "Save Error: Could not fetch current summoner info." : "Kayıt Hatası: Mevcut sihirdar bilgisi alınamadı.");
                    return;
                }

                var me = JObject.Parse(await meRes.Content.ReadAsStringAsync());
                string? myPuuid = me["puuid"]?.ToString();
                long? mySummonerId = me["summonerId"]?.Value<long>();

                if (string.IsNullOrEmpty(myPuuid))
                {
                    Logger.Info(MainWindow.IsEnglish ? "Save Error: PUUID not found." : "Kayıt Hatası: PUUID bulunamadı.");
                    return;
                }

                Logger.Info(MainWindow.IsEnglish ? "Querying match history..." : "Maç geçmişi sorgulanıyor...");
                var matchRes = await client.GetAsync("lol-match-history/v1/products/lol/current-summoner/matches");
                if (!matchRes.IsSuccessStatusCode)
                {
                    Logger.Info(MainWindow.IsEnglish ? "Save Error: Match history endpoint unreachable." : "Kayıt Hatası: Maç geçmişi endpoint'ine ulaşılamadı.");
                    return;
                }

                var json = JObject.Parse(await matchRes.Content.ReadAsStringAsync());
                var lastGame = json["games"]?["games"]?.First;
                if (lastGame == null)
                {
                    Logger.Info(MainWindow.IsEnglish ? "Save Error: No past matches found." : "Kayıt Hatası: Geçmişte hiç maç bulunamadı.");
                    return;
                }

                int gameDuration = lastGame["gameDuration"]?.Value<int>() ?? 0;
                if (gameDuration < 300) 
                {
                    Logger.Info(MainWindow.IsEnglish ? "Game detected as 'Remake'. Not added to stats." : "Oyun 'Remake' (Bozma) olarak algılandı. İstatistiklere eklenmedi.");
                    return;
                }

                int myParticipantId = -1;
                var identities = lastGame["participantIdentities"] as JArray;
                if (identities != null)
                {
                    foreach (var identity in identities)
                    {
                        var player = identity["player"];
                        if (player != null && (player["puuid"]?.ToString() == myPuuid || player["summonerId"]?.Value<long>() == mySummonerId))
                        {
                            myParticipantId = identity["participantId"]?.Value<int>() ?? -1;
                            break;
                        }
                    }
                }

                if (myParticipantId == -1)
                {
                    Logger.Info(MainWindow.IsEnglish ? "Save Error: Player identity could not be matched." : "Kayıt Hatası: Oyuncu kimliği (Identity) eşleştirilemedi.");
                    return;
                }

                var participants = lastGame["participants"] as JArray;
                if (participants != null)
                {
                    foreach (var p in participants)
                    {
                        if (p["participantId"]?.Value<int>() == myParticipantId)
                        {
                            int champId = p["championId"]?.Value<int>() ?? -1;

                            bool win = p["stats"]?["win"]?.Value<bool>() ?? p["stats"]?["WIN"]?.Value<bool>() ?? false;

                            if (champId != -1)
                            {
                                StatsService.RecordMatch(champId, win);
                                string champName = ChampionService.GetName(champId);
                                Logger.Info(MainWindow.IsEnglish ? $"SUCCESS → Champ: {champName} | Result: {(win ? "Win" : "Loss")} recorded." : $"BAŞARILI → Şampiyon: {champName} | Sonuç: {(win ? "Galibiyet" : "Mağlubiyet")} kaydedildi.");
                            }
                            else
                            {
                                Logger.Info(MainWindow.IsEnglish ? "Save Error: Champion ID could not be read." : "Kayıt Hatası: Şampiyon ID'si okunamadı.");
                            }
                            return;
                        }
                    }
                }

                Logger.Info(MainWindow.IsEnglish ? "Save Error: Player not found among match participants." : "Kayıt Hatası: Oyuncu maç katılımcıları (participants) arasında bulunamadı.");
            }
            catch (Exception ex)
            {
                Logger.Info(MainWindow.IsEnglish ? "MatchHistory critical error: " + ex.Message : "MatchHistory kritik hata: " + ex.Message);
            }
        }
    }
}