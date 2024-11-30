#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace deovrScraper.DB.Models
{
  public class DbDeoVrTab
  {
    [NotMapped]
    public bool IsDirty { get; set; } = false;

    [NotMapped]
    public bool IsEditing { get; set; } = false;

    [Key]
    public long Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Type { get; set; }

    [Required]
    public bool Active { get; set; }

    [Required]
    public int Order { get; set; }

    [Required]
    public string TagWhitelist { get; set; }

    [NotMapped]
    public IEnumerable<string> TagWhitelistList
    {
      get => string.IsNullOrEmpty(TagWhitelist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(TagWhitelist) ?? new List<string>();
      set
      {
        TagWhitelist = JsonSerializer.Serialize(value);
        this.IsDirty = true;
      }
    }

    [NotMapped]
    public string TagWhitelistDisplayString
    {
      get
      {
        var list = string.IsNullOrEmpty(TagWhitelist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(TagWhitelist) ?? new List<string>();
        return string.Join(", ", list);
      }
      set
      {
      }
    }

    [Required]
    public string ActressWhitelist { get; set; }

    [NotMapped]
    public IEnumerable<string> ActressWhitelistList
    {
      get => string.IsNullOrEmpty(ActressWhitelist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ActressWhitelist) ?? new List<string>();
      set
      {
        ActressWhitelist = JsonSerializer.Serialize(value);
        this.IsDirty = true;
      }
    }

    [NotMapped]
    public string ActressWhitelistDisplayString
    {
      get
      {
        var list = string.IsNullOrEmpty(ActressWhitelist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ActressWhitelist) ?? new List<string>();
        return string.Join(", ", list);
      }
      set
      {
      }
    }

    [Required]
    public string TagBlacklist { get; set; }

    [NotMapped]
    public IEnumerable<string> TagBlacklistList
    {
      get => string.IsNullOrEmpty(TagBlacklist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(TagBlacklist) ?? new List<string>();
      set
      {
        TagBlacklist = JsonSerializer.Serialize(value);
        this.IsDirty = true;
      }
    }

    [NotMapped]
    public string TagBlacklistDisplayString
    {
      get
      {
        var list = string.IsNullOrEmpty(TagBlacklist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(TagBlacklist) ?? new List<string>();
        return string.Join(", ", list);
      }
      set
      {
      }
    }

    [Required]
    public string ActressBlacklist { get; set; }

    [NotMapped]
    public IEnumerable<string> ActressBlacklistList
    {
      get => string.IsNullOrEmpty(ActressBlacklist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ActressBlacklist) ?? new List<string>();
      set
      {
        ActressBlacklist = JsonSerializer.Serialize(value);
        this.IsDirty = true;
      }
    }

    [NotMapped]
    public string ActressBlacklistDisplayString
    {
      get
      {
        var list = string.IsNullOrEmpty(ActressBlacklist) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ActressBlacklist) ?? new List<string>();
        return string.Join(", ", list);
      }
      set
      {
      }
    }

    [Required]
    public string VideoWhitelist { get; set; }

    [Required]
    public string VideoBlacklist { get; set; }
  }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
