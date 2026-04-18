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
        builder.Services
            .AddOptions<UploadLimitsOptions>()
            .Bind(builder.Configuration.GetSection(UploadLimitsOptions.SectionName));

        var uploadLimits = builder.Configuration
            .GetSection(UploadLimitsOptions.SectionName)
            .Get<UploadLimitsOptions>() ?? new UploadLimitsOptions();
        
        var maxUploadBytes = uploadLimits.MaxUploadBytes > 0
            ? uploadLimits.MaxUploadBytes
            : UploadLimitsOptions.DefaultMaxUploadBytes;

        // transport-level limit to reject oversized requests before they reach the pipeline.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = maxUploadBytes;
        });

        return builder;
    }
}
