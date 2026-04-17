using FileSanitizerService.Api.Options;
using FileSanitizerService.Core.Detection;
using FileSanitizerService.Core.Formats.Abc;
using FileSanitizerService.Core.Interfaces;
using FileSanitizerService.Core.SanitizerFormatResolver;
using FileSanitizerService.Core.Services;
using FileSanitizerService.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;

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
    
    public static WebApplicationBuilder ConfigureUploadLimits(this WebApplicationBuilder builder)
    {
        // Fall back to 500 MB if nothing is set in appsettings.
        const long defaultMaxUploadBytes = 500L * 1024 * 1024; // 500 mb

        builder.Services
            .AddOptions<UploadLimitsOptions>()
            .Bind(builder.Configuration.GetSection(UploadLimitsOptions.SectionName));

        var uploadLimits = builder.Configuration
            .GetSection(UploadLimitsOptions.SectionName)
            .Get<UploadLimitsOptions>() ?? new UploadLimitsOptions();
        
        var maxUploadBytes = uploadLimits.MaxUploadBytes > 0
            ? uploadLimits.MaxUploadBytes
            : defaultMaxUploadBytes;

        // define max size for any file coming from rest api route,
        // and define that we can only save 64mb in the ram to nor overflow the ram
        // (the rest is written to tmp file on the disk)
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = maxUploadBytes;
            options.MemoryBufferThreshold = 64 * 1024;
        });

        // Kestrel needs to know the same limit, otherwise it'll cut the connection
        // before the route even gets to read the body.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = maxUploadBytes;
        });

        return builder;
    }
}
