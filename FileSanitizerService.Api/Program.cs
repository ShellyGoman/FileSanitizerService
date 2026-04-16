using FileSanitizerService.Api.Middlewares;
using FileSanitizerService.Api.Extensions;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

const long DefaultMaxUploadBytes = 500L * 1024 * 1024;
var maxUploadBytes = builder.Configuration.GetValue<long?>("UploadLimits:MaxUploadBytes") ?? DefaultMaxUploadBytes;

builder.Services.AddFileSanitizerServices();
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
