using FileSanitizerService.Api.Middlewares;
using FileSanitizerService.Api.Options;
using FileSanitizerService.Api.Extensions;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

const long DefaultMaxUploadBytes = 500L * 1024 * 1024;

builder.Services.AddFileSanitizerServices();
builder.Services
    .AddOptions<UploadLimitsOptions>()
    .Bind(builder.Configuration.GetSection(UploadLimitsOptions.SectionName));

var uploadLimits = builder.Configuration
    .GetSection(UploadLimitsOptions.SectionName)
    .Get<UploadLimitsOptions>() ?? new UploadLimitsOptions();
var maxUploadBytes = uploadLimits.MaxUploadBytes > 0
    ? uploadLimits.MaxUploadBytes
    : DefaultMaxUploadBytes;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxUploadBytes;
    options.MemoryBufferThreshold = 64 * 1024;
});
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = maxUploadBytes; });

// API infrastructure
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
