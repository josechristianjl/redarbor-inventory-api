using InventoryManagement.Api.Configuration;
using InventoryManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddInventorySwagger(builder.Configuration);
builder.Services.AddInventoryApplication();
builder.Services.AddInventoryPersistence(builder.Configuration);
builder.Services.AddInventoryErrorHandling();
builder.Services.AddInventoryAuthentication(
    builder.Configuration, builder.Environment);
builder.Services.AddInventoryHealthChecks();
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseInventorySwagger();
}
if (!app.Environment.IsDevelopment() ||
    app.Configuration.GetValue("API_ENABLE_HTTPS_REDIRECTION", true))
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapInventoryHealthChecks();
app.Run();
