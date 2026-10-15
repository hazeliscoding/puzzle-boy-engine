namespace PuzzleBoy.Nocturne.Tests;

public sealed class GameInstallTests
{
    [Fact]
    public void LibraryPaths_reads_every_library_and_unescapes_backslashes()
    {
        const string Vdf = """
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            		"label"		""
            		"apps"
            		{
            			"1149360"		"13894738420"
            		}
            	}
            	"1"
            	{
            		"path"		"/home/player/.local/share/Steam"
            	}
            }
            """;

        Assert.Equal([@"C:\Program Files (x86)\Steam", "/home/player/.local/share/Steam"], GameInstall.LibraryPaths(Vdf));
    }

    [Fact]
    public void At_returns_null_for_a_folder_without_the_game() =>
        Assert.Null(GameInstall.At(AppContext.BaseDirectory));
}
