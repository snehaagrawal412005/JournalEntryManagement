using SQLite;
using JournalEntryManagement.Models;
using System.IO;

namespace JournalEntryManagement.Services
{
    public class JournalService
    {
        // Variable for database connection
        private SQLiteAsyncConnection _db;

        // Initialize the database
        private async Task Init()
        {
            if (_db != null)
                return;

            // Storing the db in the app data folder
            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "MyStudentJournal.db");
            _db = new SQLiteAsyncConnection(databasePath);

            // Create table if it doesn't exist
            await _db.CreateTableAsync<JournalEntry>();
        }

        // --- CRUD OPERATIONS ---

        public async Task AddEntryAsync(JournalEntry entry)
        {
            await Init();
            try
            {
                // Check if we already have an entry for this date
                var existing = await GetEntryByDateAsync(entry.EntryDate);

                if (existing != null)
                {
                    // Update the existing one
                    entry.Id = existing.Id;
                    await _db.UpdateAsync(entry);
                }
                else
                {
                    // Create a new one
                    await _db.InsertAsync(entry);
                }
            }
            catch (Exception ex)
            {
                // Just log error to console for debugging
                Console.WriteLine("Error adding entry: " + ex.Message);
            }
        }

        public async Task<List<JournalEntry>> GetAllEntriesAsync()
        {
            await Init();
            // Sort by date so newest is first
            return await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();
        }

        public async Task<JournalEntry> GetEntryByDateAsync(DateTime date)
        {
            await Init();
            var allEntries = await _db.Table<JournalEntry>().ToListAsync();

            // Loop to find the matching date
            foreach (var item in allEntries)
            {
                if (item.EntryDate.Date == date.Date)
                {
                    return item;
                }
            }
            return null;
        }

        public async Task DeleteEntryAsync(JournalEntry entry)
        {
            await Init();
            await _db.DeleteAsync(entry);
        }

        public async Task DeleteAllEntriesAsync()
        {
            await Init();
            await _db.DeleteAllAsync<JournalEntry>();
        }

        // --- STATS & ANALYTICS (Written with simple loops) ---

        public async Task<(int Current, int Longest, int Missed)> GetStats()
        {
            await Init();
            var entries = await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();

            if (entries.Count == 0)
            {
                return (0, 0, 0);
            }

            // 1. Calculate Current Streak
            int currentStreak = 0;
            var dayToCheck = DateTime.Today;

            // If I haven't written today yet, check yesterday so streak doesn't reset to 0
            bool wroteToday = false;
            foreach (var e in entries)
            {
                if (e.EntryDate.Date == DateTime.Today)
                {
                    wroteToday = true;
                    break;
                }
            }

            if (!wroteToday)
            {
                dayToCheck = DateTime.Today.AddDays(-1);
            }

            // Loop to count consecutive days
            foreach (var e in entries)
            {
                if (e.EntryDate.Date == dayToCheck.Date)
                {
                    currentStreak++;
                    dayToCheck = dayToCheck.AddDays(-1); // Move back one day
                }
                else if (e.EntryDate.Date < dayToCheck.Date)
                {
                    break; // Streak is broken
                }
            }

            // 2. Calculate Longest Streak
            int longestStreak = 0;
            int tempCount = 1;

            // Sort old to new for this calculation
            var sortedList = entries.OrderBy(x => x.EntryDate).ToList();

            for (int i = 1; i < sortedList.Count; i++)
            {
                // Get difference in days
                var diff = (sortedList[i].EntryDate.Date - sortedList[i - 1].EntryDate.Date).Days;

                if (diff == 1)
                {
                    tempCount++;
                }
                else if (diff > 1)
                {
                    tempCount = 1; // Reset if gap is found
                }

                if (tempCount > longestStreak)
                {
                    longestStreak = tempCount;
                }
            }

            // Handle edge case
            if (longestStreak == 0 && entries.Count > 0) longestStreak = 1;


            // 3. Calculate Missed Days
            var firstEntryDate = sortedList.First().EntryDate.Date;
            var totalDaysSinceStart = (DateTime.Today - firstEntryDate).Days + 1;
            int missedDays = totalDaysSinceStart - entries.Count;

            if (missedDays < 0) missedDays = 0;

            return (currentStreak, longestStreak, missedDays);
        }

        // Logic for "Most Used Tags"
        public async Task<List<string>> GetTopTags()
        {
            await Init();
            var allEntries = await _db.Table<JournalEntry>().ToListAsync();

            // Using a dictionary to count
            Dictionary<string, int> tagCounts = new Dictionary<string, int>();

            foreach (var entry in allEntries)
            {
                if (!string.IsNullOrEmpty(entry.Tags))
                {
                    // Split by comma
                    var tagsArray = entry.Tags.Split(',');

                    foreach (var t in tagsArray)
                    {
                        string cleanTag = t.Trim();
                        if (string.IsNullOrEmpty(cleanTag)) continue;

                        if (tagCounts.ContainsKey(cleanTag))
                        {
                            tagCounts[cleanTag]++;
                        }
                        else
                        {
                            tagCounts.Add(cleanTag, 1);
                        }
                    }
                }
            }

            // Convert to a simple list of strings like "Work (5)"
            // I used a bit of Linq here just to sort them, which is standard
            return tagCounts.OrderByDescending(x => x.Value)
                            .Take(5)
                            .Select(x => x.Key + " (" + x.Value + ")")
                            .ToList();
        }

        // Logic for bar chart (last 5 days)
        public async Task<List<int>> GetWordTrend()
        {
            await Init();
            List<int> trendData = new List<int>();

            // Loop 5 times for last 5 days
            for (int i = 4; i >= 0; i--)
            {
                var dateToCheck = DateTime.Today.AddDays(-i);
                var entry = await GetEntryByDateAsync(dateToCheck);

                if (entry != null && !string.IsNullOrEmpty(entry.Content))
                {
                    // Simple word count by splitting spaces
                    int words = entry.Content.Split(' ').Length;
                    trendData.Add(words);
                }
                else
                {
                    trendData.Add(0);
                }
            }
            return trendData;
        }

        public async Task<string> GetTopMood()
        {
            await Init();
            var all = await _db.Table<JournalEntry>().ToListAsync();

            // Simple group by logic
            var moodGroup = all.GroupBy(e => e.PrimaryMood)
                               .OrderByDescending(g => g.Count())
                               .FirstOrDefault();

            if (moodGroup != null)
            {
                return moodGroup.Key;
            }
            return "None";
        }

        public async Task<List<JournalEntry>> GetFavoritesAsync()
        {
            await Init();
            var all = await _db.Table<JournalEntry>().ToListAsync();
            // Simple loop to filter
            return all.Where(e => e.IsFavorite).OrderByDescending(x => x.EntryDate).ToList();
        }
    }
}
