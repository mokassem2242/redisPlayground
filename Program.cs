using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using redisPlayground.Data;
using redisPlayground.Services;

namespace redisPlayground
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Redis Playground API",
                    Version = "v1",
                    Description = "CRUD API for Items with Redis cache-aside. GET uses cache; Create/Update/Delete invalidate cache."
                });
            });

            // Redis distributed cache (used for cache-aside in ItemRepository)
            var redisSection = builder.Configuration.GetSection(RedisOptions.SectionName);
            builder.Services.Configure<RedisOptions>(redisSection);
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisSection["ConnectionString"] ?? "localhost:6379";
                options.InstanceName = redisSection["InstanceName"] ?? "RedisPlaygroundDB:";
            });

            // SQL Server (durable store)
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddScoped<IItemRepository, ItemRepository>();

            var app = builder.Build();

            // Create SQL Server database and table on first run (Development)
            if (app.Environment.IsDevelopment())
            {
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Redis Playground API v1");
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
