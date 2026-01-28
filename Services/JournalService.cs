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

            // creating the table table if it doesn't exist
            await _db.CreateTableAsync<JournalEntry>();
        }

        public async Task AddEntryAsync(JournalEntry entry)
        {
            await Init();
            try
            {
                // Checking if we already have an entry for this date
                var existing = await GetEntryByDateAsync(entry.EntryDate);

                if (existing != null)
                {
                    // Updating the existing one
                    entry.Id = existing.Id;
                    await _db.UpdateAsync(entry);
                }
                else
                {
                    // Creating a new one
                    await _db.InsertAsync(entry);
                }
            }
            catch (Exception ex)
            {
                // its just log error to console for debugging
                Console.WriteLine("Error adding entry: " + ex.Message);
            }
        }

        public async Task<List<JournalEntry>> GetAllEntriesAsync()
        {
            await Init();
            // Sorting by date so newest is the first
            return await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();
        }

        public async Task<JournalEntry> GetEntryByDateAsync(DateTime date)
        {
            await Init();
            var allEntries = await _db.Table<JournalEntry>().ToListAsync();

            // using loop to find the matching date
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

    

        public async Task<(int Current, int Longest, int Missed)> GetStats()
        {
            await Init();
            var entries = await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();

            if (entries.Count == 0)
            {
                return (0, 0, 0);
            }

            // Calculating Current Streak
            int currentStreak = 0;
            var dayToCheck = DateTime.Today;

            // If I haven't written today yet then check yesterday so streak doesn't reset to 0
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

            // calculating Longest Streak
            int longestStreak = 0;
            int tempCount = 1;

            //sorting old to new for this calculation
            var sortedList = entries.OrderBy(x => x.EntryDate).ToList();

            for (int i = 1; i < sortedList.Count; i++)
            {
                //getting difference in days
                var diff = (sortedList[i].EntryDate.Date - sortedList[i - 1].EntryDate.Date).Days;

                if (diff == 1)
                {
                    tempCount++;
                }
                else if (diff > 1)
                {
                    tempCount = 1; // Resetting if gap is found
                }

                if (tempCount > longestStreak)
                {
                    longestStreak = tempCount;
                }
            }

            // Handling edge case
            if (longestStreak == 0 && entries.Count > 0) longestStreak = 1;


            // Calculating the missed Days
            var firstEntryDate = sortedList.First().EntryDate.Date;
            var totalDaysSinceStart = (DateTime.Today - firstEntryDate).Days + 1;
            int missedDays = totalDaysSinceStart - entries.Count;

            if (missedDays < 0) missedDays = 0;

            return (currentStreak, longestStreak, missedDays);
        }

        // Logic for the Most Used Tags
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

            // Converting to a simple list of strings like "Work (5)"
            // I used a bit of Linq here just to sort them, which is standard
            return tagCounts.OrderByDescending(x => x.Value)
                            .Take(5)
                            .Select(x => x.Key + " (" + x.Value + ")")
                            .ToList();
        }

       
        public async Task<List<int>> GetWordTrend()
        {
            await Init();
            List<int> trendData = new List<int>();

        
            for (int i = 4; i >= 0; i--)
            {
                var dateToCheck = DateTime.Today.AddDays(-i);
                var entry = await GetEntryByDateAsync(dateToCheck);

                if (entry != null && !string.IsNullOrEmpty(entry.Content))
                {
                   
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
