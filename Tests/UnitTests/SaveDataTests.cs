using System.Text.Json;
using GodotGameTemplate.Persistence;
using Xunit;

namespace GodotGameTemplate.UnitTests;

public class SaveDataTests
{
    [Fact]
    public void Json_RoundTrip_PreservesSpawnAndVersion()
    {
        var data = new SaveData { SpawnX = 12.5f, SpawnY = -3.25f };
        string json = JsonSerializer.Serialize(data);
        var back = JsonSerializer.Deserialize<SaveData>(json);
        Assert.NotNull(back);
        Assert.Equal(1, back!.Version);
        Assert.Equal(12.5f, back.SpawnX);
        Assert.Equal(-3.25f, back.SpawnY);
    }

    [Fact]
    public void Json_VersionFieldIsPreservedForFutureMigrations()
    {
        var json = JsonSerializer.Serialize(
            new SaveData
            {
                SpawnX = 1f,
                SpawnY = 2f,
                Version = 2,
            }
        );
        var back = JsonSerializer.Deserialize<SaveData>(json);
        Assert.Equal(2, back!.Version);
    }
}
