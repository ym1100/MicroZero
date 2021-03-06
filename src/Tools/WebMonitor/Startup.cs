using System.Threading.Tasks;
using Agebull.Common.Configuration;
using Agebull.Common.Ioc;
using Agebull.ZeroNet.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZeroNet.Http.Route;

namespace WebMonitor
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            ConfigurationManager.SetConfiguration(configuration);
            ZeroApplication.CheckOption();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddMvc();
            IocHelper.AddSingleton<PlanManage>();
            ZeroApplication.RegistZeroObject<ApiCounter>();
            ZeroApplication.RegistZeroObject<PlanSubscribe>();
            ZeroApplication.RegistZeroObject<EventSub>();
            ZeroApplication.Initialize(); 
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            //.AddJsonOptions(options =>
            // {
            //     //忽略循环引用
            //     options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            //     //不使用驼峰样式的key
            //     options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            //     //设置时间格式
            //     options.SerializerSettings.DateFormatString = "yyyy-MM-dd";
            // });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            Task.Factory.StartNew(ZeroApplication.Run);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            WebSocketNotify.Binding(app);

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "api",
                    template: "{controller}/{action}/{station}");
            });
        }
    }
}
