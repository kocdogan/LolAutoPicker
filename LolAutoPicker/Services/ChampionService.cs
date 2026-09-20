using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LolAutoPicker.Services
{
    public static class ChampionService
    {
        private static string LatestVersion = "14.1.1";
        private static readonly Dictionary<string, int> ChampNameToId = new();
        private static readonly Dictionary<int, string> ChampIdToName = new();

        private static readonly Dictionary<int, string> ChampIdToImageKey = new();

        public static async Task Load()
        {
            using var http = new HttpClient();

            var versionJson = await http.GetStringAsync("https://ddragon.leagueoflegends.com/api/versions.json");
            var versions = JArray.Parse(versionJson);
            string latestVersion = versions[0]?.ToString() ?? "14.1.1";

            var json = await http.GetStringAsync($"https://ddragon.leagueoflegends.com/cdn/{latestVersion}/data/en_US/champion.json");
            var data = JObject.Parse(json)["data"] as JObject;

            ChampNameToId.Clear();
            ChampIdToName.Clear();

            if (data == null) return;

            foreach (var prop in data.Properties())
            {
                var champToken = prop.Value;
                string? rawName = champToken["name"]?.ToString();
                string? keyStr = champToken["key"]?.ToString();
                string? imageKey = champToken["id"]?.ToString();

                if (!string.IsNullOrEmpty(rawName) && int.TryParse(keyStr, out int id))
                {
                    ChampNameToId[rawName.ToLower().Trim()] = id;
                    ChampIdToName[id] = rawName;
                    ChampIdToImageKey[id] = imageKey;
                }
            }
        }

        public static int GetId(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            return ChampNameToId.TryGetValue(name.ToLower().Trim(), out var id) ? id : -1;
        }

        public static string GetName(int id)
        {
            return ChampIdToName.TryGetValue(id, out var name) ? name : $"Bilinmeyen ({id})";
        }

        public static string GetImageUrl(int id)
        {
            if (ChampIdToImageKey.TryGetValue(id, out var imageKey))
            {
                return $"https://ddragon.leagueoflegends.com/cdn/{LatestVersion}/img/champion/{imageKey}.png";
            }
            return "";
        }
    }
}