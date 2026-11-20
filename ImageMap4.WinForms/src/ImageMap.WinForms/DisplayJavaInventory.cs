using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using fNbt;
using ImageMap4;

namespace ImageMap.WinForms
{
    public sealed class DisplayJavaInventory : IInventory, INotifyPropertyChanged
    {
        private static readonly HttpClient Client = new();
        private readonly IInventory _wrapped;

        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name { get; private set; }
        public int FreeSlots => _wrapped.FreeSlots;

        public DisplayJavaInventory(IInventory wrapped)
        {
            _wrapped = wrapped;
            Name = wrapped.Name;
            if (Name.Length != 36)
                return;

            AppSettings.Default.UsernameCache ??= new();
            var cache = AppSettings.Default.UsernameCache;
            for (int i = 0; i + 1 < cache.Count; i += 2)
            {
                if (cache[i] == Name)
                {
                    Name = cache[i + 1];
                    return;
                }
            }

            string uuid = Name;
            _ = LookupNameAsync(uuid);
        }

        private async Task LookupNameAsync(string uuid)
        {
            try
            {
                var response = await Client.GetStringAsync("https://api.mojang.com/user/profiles/" + uuid + "/names")
                    .ConfigureAwait(false);
                string? latest = null;
                foreach (Match match in Regex.Matches(response, "\"name\"\\s*:\\s*\"([^\"]+)\""))
                    latest = match.Groups[1].Value;
                if (string.IsNullOrEmpty(latest))
                    return;
                lock (AppSettings.Default.UsernameCache)
                {
                    AppSettings.Default.UsernameCache.Add(uuid);
                    AppSettings.Default.UsernameCache.Add(latest);
                }
                Name = latest!;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
            catch (Exception ex)
            {
                CrashLog.Info("UUID lookup failed for " + uuid + ": " + ex.Message);
            }
        }

        public void AddItems(IEnumerable<NbtCompound> items) => _wrapped.AddItems(items);
    }
}
