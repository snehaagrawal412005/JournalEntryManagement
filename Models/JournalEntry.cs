using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JournalEntryManagement.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string PrimaryMood { get; set; } = "Neutral";
        public string Tags { get; set; } = "";
    }
}
