using System.ComponentModel.DataAnnotations;

namespace Backend.Consumidor.Api.Data.Models;

public class MatchStatistic
{
    [Key]
    public int MatchId { get; set; }
    
    public Match Match { get; set; }

    public int TotalGoals { get; set; }
    public int TotalYellowCards { get; set; }
    public int TotalRedCards { get; set; }
    public int TotalSubstitutions { get; set; }
    public int TotalEvents { get; set; }
}
