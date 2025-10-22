using Apis.AutoMapper;
using Apis.GrpcServices;
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
            // CORS: allow any origin/method/header (no credentials)
            services.AddCors(o =>
            {
                o.AddPolicy("AllowAll", p =>
                    p.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader());
            });

            // gRPC
            services.AddGrpc();
            services.AddAutoMapper(typeof(LpsMappingProfile));

            // MVC Controllers
            services.AddControllersWithViews();
            services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.PropertyNamingPolicy = null;
                        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
                    });
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
