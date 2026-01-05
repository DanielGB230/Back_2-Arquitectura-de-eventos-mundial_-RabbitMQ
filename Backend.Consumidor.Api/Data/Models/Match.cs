using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Consumidor.Api.Data.Models;

public class Match
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int MatchId { get; set; }

    public int HomeTeamId { get; set; } // Added HomeTeamId
    public int AwayTeamId { get; set; } // Added AwayTeamId
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public MatchStatus Status { get; set; }

    public MatchStatistic MatchStatistic { get; set; }

    public ICollection<MatchEvent> MatchEvents { get; set; } = new List<MatchEvent>();
}

public enum MatchStatus
{
    NotStarted,
    InProgress,
    Finished
}
