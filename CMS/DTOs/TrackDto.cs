namespace CMS.DTOs
{
    public sealed class CreateTrackRequest
    {
        public int ID { get; set; }            // College.ID
        public int UserId { get; set; }        // Users.UserId
        public string Status { get; set; } = "";
        public DateTime? Date { get; set; }    // optional; null means "today" on server
        public string? Remarks { get; set; }
    }

    public sealed class UpdateTrackRequest
    {
        // All fields optional except TID (path param)
        public int? ID { get; set; }
        public int? UserId { get; set; }
        public string? Status { get; set; }
        public DateTime? Date { get; set; }
        public string? Remarks { get; set; }
    }

    public sealed class TrackResponse
    {
        public int TID { get; set; }
        public int ID { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "";
        public DateTime Date { get; set; }
        public string? Remarks { get; set; }
    }

    public class LeadInfoDto
    {
        public int LId { get; set; }
        public int ID { get; set; }        // College ID (FK to College.ID)
        public int UserId { get; set; }    // FK to Users.UserId
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class GetLeadInfoDto
    {
        public int LId { get; set; }
        public int ID { get; set; }        // College ID (FK to College.ID)
        public int UserId { get; set; }    // FK to Users.UserId
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

}
