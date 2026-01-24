using Apis.AutoMapper;
using Apis.GrpcServices;
using Apis.Hubs;
using Apis.Services;
using LPS.GrpcServices;
using LPS.Infrastructure.Monitoring.GRPCServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LPS.Apis
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // CORS: allow any origin/method/header (with credentials for SignalR)
            services.AddCors(o =>
            {
                o.AddPolicy("AllowAll", p =>
                    p.SetIsOriginAllowed(_ => true)
                     .AllowAnyMethod()
                     .AllowAnyHeader()
                     .AllowCredentials());
            });

            // gRPC
            services.AddGrpc();
            services.AddAutoMapper(typeof(LpsMappingProfile));

            // SignalR for real-time windowed metrics
            services.AddSignalR(options =>
                {
                    // Keep connections alive longer during shutdown
                    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                })
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                    options.PayloadSerializerOptions.DictionaryKeyPolicy = null;
                });

            // MVC Controllers
            services.AddControllersWithViews();
            services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.PropertyNamingPolicy = null;
                        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
                    });

            // Windowed metrics SignalR pusher (reads from queue, pushes to SignalR hub)
            services.AddHostedService<WindowedMetricsDispatcher>();
            
            // Cumulative metrics SignalR pusher (reads from queue, pushes to SignalR hub)
            // Cumulative data is pushed at its own interval (RefreshRate), separate from windowed data
            services.AddHostedService<CumulativeMetricsDispatcher>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseDefaultFiles(); 
            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors("AllowAll");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // SignalR hub for real-time metrics (cumulative and windowed)
                endpoints.MapHub<MetricsHub>("/hubs/metrics");

                // gRPC services
                endpoints.MapGrpcService<NodeGRPCService>();
                endpoints.MapGrpcService<MetricsGrpcService>();
                endpoints.MapGrpcService<EntityDiscoveryGrpcService>();
                endpoints.MapGrpcService<MonitorGRPCService>();
                endpoints.MapGrpcService<MetricsQueryGrpcService>();
                endpoints.MapGrpcService<IterationTerminationGrpcService>();

                // MVC routes
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
