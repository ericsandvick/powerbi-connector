using PowerBIConnector.Services;

var builder = WebApplication.CreateBuilder(args);

// Add a PowerBIService to the DI container for use in the controllers
builder.Services.AddSingleton<PowerBIService>((s) =>
{
    return new PowerBIService(
        new PowerBIServiceConfig
        {
            ApiUrl = builder.Configuration.GetSection("PowerBI")["ApiUrl"],
            AuthorityBaseUrl = builder.Configuration.GetSection("PowerBI")["AuthorityBaseUrl"],
            ClientId = builder.Configuration.GetSection("PowerBI")["ClientId"],
            ClientSecret = builder.Configuration.GetSection("PowerBI")["ClientSecret"],
            ResourceUrl = builder.Configuration.GetSection("PowerBI")["ResourceUrl"],
            TenantId = builder.Configuration.GetSection("PowerBI")["TenantId"]
        });
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
