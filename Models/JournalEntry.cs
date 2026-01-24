using SQLite; //for the database

namespace JournalEntryManagement.Models
{
    public class JournalEntry
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
       
        public DateTime EntryDate { get; set; } = DateTime.Today;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public string PrimaryMood { get; set; } = "Neutral";
        public string SecondaryMood1 { get; set; } = "";
        public string SecondaryMood2 { get; set; } = "";

        public string Tags { get; set; } = "";
    }
}
