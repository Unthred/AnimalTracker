using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace AnimalTracker.Tests;

public sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "AnimalTracker.Tests";
    public string WebRootPath { get; set; } = contentRootPath;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = contentRootPath;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
