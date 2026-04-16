using FileSanitizerService.Core.Detection;
using FileSanitizerService.Core.Formats.Abc;
using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.Registry;
using FileSanitizerService.Core.Services;
using FileSanitizerService.Infrastructure.Services;

namespace FileSanitizerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileSanitizerServices(this IServiceCollection services)
    {
        services.AddSingleton<IFormatDetector, HeaderDetector>();

        // Each IFileSanitizer implementation is registered here.
        // To add a new format: implement IFileSanitizer and add one line below.
        services.AddSingleton<IFileSanitizer, AbcFileSanitizer>();

        services.AddSingleton<IFileSanitizerResolver, FileSanitizerResolver>();
        services.AddSingleton<ITempFileProvider, TempFileProvider>();
        services.AddScoped<SanitizationService>();

        return services;
    }
}
