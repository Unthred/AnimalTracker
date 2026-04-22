using AnimalTracker.Services;

namespace AnimalTracker.Tests;

public sealed class PhotoStorageServiceTests : IClassFixture<SqliteServiceTestFixture>
{
    private readonly SqliteServiceTestFixture _fixture;

    public PhotoStorageServiceTests(SqliteServiceTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("folder/photo.jpg", "image/jpeg")]
    [InlineData("folder/photo.jpeg", "image/jpeg")]
    [InlineData("x.PNG", "image/png")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("w.webp", "image/webp")]
    [InlineData("unknown.bin", "application/octet-stream")]
    public void GetContentTypeForStoredPath_maps_extensions(string path, string expected)
    {
        Assert.Equal(expected, PhotoStorageService.GetContentTypeForStoredPath(path));
    }

    [Fact]
    public void OpenRead_throws_for_path_traversal_outside_content_root()
    {
        var service = CreateService();
        Assert.Throws<InvalidOperationException>(() => service.OpenRead("../outside.jpg"));
    }

    [Fact]
    public void OpenRead_reads_file_inside_content_root()
    {
        var env = _fixture.CreateWebHostEnvironment();
        var service = CreateService(env);
        var relativePath = "App_Data/photos/test.jpg";
        var absPath = Path.Combine(env.ContentRootPath, "App_Data", "photos", "test.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
        File.WriteAllBytes(absPath, [1, 2, 3]);

        using var stream = service.OpenRead(relativePath);
        Assert.Equal(3, stream.Length);
    }

    [Fact]
    public void TryDeleteStoredFile_deletes_existing_file_and_rejects_invalid_paths()
    {
        var env = _fixture.CreateWebHostEnvironment();
        var service = CreateService(env);
        var relativePath = "App_Data/photos/delete-me.jpg";
        var absPath = Path.Combine(env.ContentRootPath, "App_Data", "photos", "delete-me.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
        File.WriteAllBytes(absPath, [1]);

        Assert.True(service.TryDeleteStoredFile(relativePath));
        Assert.False(File.Exists(absPath));
        Assert.False(service.TryDeleteStoredFile("../outside.jpg"));
        Assert.False(service.TryDeleteStoredFile("App_Data/photos/missing.jpg"));
    }

    private PhotoStorageService CreateService(TestWebHostEnvironment? env = null)
    {
        var webHost = env ?? _fixture.CreateWebHostEnvironment();
        var currentUser = _fixture.CreatePrimaryUserAccessor();
        return new PhotoStorageService(webHost, currentUser);
    }
}
