using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string Action { get; set; }=string.Empty; // e.g., "CREATE", "UPDATE", "DELETE"
        public string EntityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ChangedByUserId { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string? Changes { get; set; } // JSON or description of changes
    }
}

