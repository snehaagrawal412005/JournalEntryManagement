using SQLite;
using JournalEntryManagement.Models;
using System.IO;

namespace JournalEntryManagement.Services
{
    public class JournalService
    {
        private SQLiteAsyncConnection _db;

        // This function will set up the database connection
        private async Task Init()
        {
            if (_db != null)
                return;

            // I used the AppDataDirectory because it works on both the Android and Windows
            // I added v' to the name because I changed the model and it was crashing with the old file
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "JournalDB_v2.db3");

            _db = new SQLiteAsyncConnection(dbPath);

            // Creating the table if it doesn't exist
            await _db.CreateTableAsync<JournalEntry>();
        }

        public async Task AddEntryAsync(JournalEntry entry)
        {
            await Init();

            // First I need to check if I already wrote something today.
            // The requirements wil say One entry per day.
            var existingEntry = await GetEntryByDateAsync(entry.EntryDate);

            if (existingEntry != null)
            {
                // If found, I will just update the old one so I dont get duplicates
                entry.Id = existingEntry.Id;
                await _db.UpdateAsync(entry);
            }
            else
            {
                // If not found, I will save it as a new entry
                await _db.InsertAsync(entry);
            }
        }

        // I used it for the History page list
        public async Task<List<JournalEntry>> GetAllEntriesAsync()
        {
            await Init();
            //I shorted by the date so the newest ones show up at thetop
            return await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();
        }

        // I ts a helper to find a single entry
        public async Task<JournalEntry> GetEntryByDateAsync(DateTime date)
        {
            await Init();
            var entries = await _db.Table<JournalEntry>().ToListAsync();

            // I use .Date here to ignore the time (hours/minutes) difference
            return entries.FirstOrDefault(e => e.EntryDate.Date == date.Date);
        }

        public async Task DeleteEntryAsync(JournalEntry entry)
        {
            await Init();
            await _db.DeleteAsync(entry);
        }

        // Its the logic to calculate the streak of how many days in a row
        public async Task<int> CalculateStreak()
        {
            await Init();
            var entries = await _db.Table<JournalEntry>().OrderByDescending(x => x.EntryDate).ToListAsync();

            if (entries.Count == 0) return 0;

            int streak = 0;
            var dayToCheck = DateTime.Today;

            foreach (var entry in entries)
            {
                //I hecke if this entry matches the day we are checking
                if (entry.EntryDate.Date == dayToCheck.Date)
                {
                    streak++;
                    //and if it matches we will move to yesterday to check the next one
                    dayToCheck = dayToCheck.AddDays(-1);
                }
                //if the entry is in the future tomorrow, we will just skip it
                else if (entry.EntryDate.Date > dayToCheck.Date)
                {
                    continue;
                }
                else
                {
                    //and if the dates dont match, the streak is broken
                    break;
                }
            }
            return streak;
        }

        // this is also called from the Settings page to reset everything
        public async Task DeleteAllEntriesAsync()
        {
            await Init();
            await _db.DeleteAllAsync<JournalEntry>();
        }
    }
}