using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LolAutoPicker.Helpers;

namespace LolAutoPicker.Services
{
    public class ChampStat
    {
        public int ChampId { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate => (Wins + Losses) == 0 ? 0 : (double)Wins / (Wins + Losses);
    }

    public static class StatsService
    {
        private static readonly string FolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        private static readonly string FilePath = Path.Combine(FolderPath, "stats.json");
        private static List<ChampStat> stats = new();
        private static readonly object lockObj = new();

        public static void Load()
        {
            lock (lockObj)
            {
                try
                {
                    if (!Directory.Exists(FolderPath))
                    {
                        Directory.CreateDirectory(FolderPath);
                    }

                    if (File.Exists(FilePath))
                    {
                        string json = File.ReadAllText(FilePath);
                        stats = JsonConvert.DeserializeObject<List<ChampStat>>(json) ?? new List<ChampStat>();
                    }
                    else
                    {
                        stats = new List<ChampStat>();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("İstatistik yükleme hatası: " + ex.Message);
                }
            }
        }

        public static void Save()
        {
            lock (lockObj)
            {
                try
                {
                    if (!Directory.Exists(FolderPath))
                    {
                        Directory.CreateDirectory(FolderPath);
                    }

                    string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                    File.WriteAllText(FilePath, json);
                }
                catch (Exception ex)
                {
                    Logger.Info("İstatistik kaydetme hatası: " + ex.Message);
                }
            }
        }

        public static void RecordMatch(int champId, bool win)
        {
            lock (lockObj)
            {
                Load();

                var item = stats.FirstOrDefault(x => x.ChampId == champId);
                if (item == null)
                {
                    item = new ChampStat { ChampId = champId };
                    stats.Add(item);
                }

                if (win) item.Wins++;
                else item.Losses++;

                Save();
            }
        }

        public static List<ChampStat> GetStats()
        {
            lock (lockObj)
            {
                Load();
                return new List<ChampStat>(stats);
            }
        }
    }
}