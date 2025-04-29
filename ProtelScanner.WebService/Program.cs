using ProtelScanner.WebService.Models;
using ProtelScanner.WebService.Services;
using Microsoft.AspNetCore.SignalR;
using ProtelScanner.WebService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Singleton services
builder.Services.AddSingleton<IDeviceManagementService, DeviceManagementService>();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHub<ScannerHub>("/scannerhub");

app.Run();