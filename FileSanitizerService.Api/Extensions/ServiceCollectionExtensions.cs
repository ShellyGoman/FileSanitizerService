using FileSanitizerService.Api.Options;
using FileSanitizerService.Core.Detection;
using FileSanitizerService.Core.Formats.Abc;
using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.SanitizerFormatResolver;
using FileSanitizerService.Core.Services;
using FileSanitizerService.Infrastructure.Services;

namespace FileSanitizerService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalServices(this IServiceCollection services)
    {
        services.AddSingleton<IFormatDetector, HeaderDetector>();

        // Each IFileSanitizer implementation is registered here.
        // To add a new format: implement IFileSanitizer and add one line below.
        services.AddSingleton<IFileSanitizer, AbcFileSanitizer>();

        services.AddSingleton<IFileSanitizerResolver, FileSanitizerResolver>();
        services.AddSingleton<ITempFileProvider, TempFileProvider>();
        services.AddScoped<ISanitizationService, SanitizationService>();

        return services;
    }
    
    public static WebApplicationBuilder ConfigureUploadLimits(this WebApplicationBuilder builder)
    {
        // MaxRequestBodySize is null(unlimited) here because it's handled 
        // in the MultipartReader -> BodyLengthLimit field.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null;
        });

        return builder;
    }
}
