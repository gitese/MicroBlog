﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using MicroBlog.Core;
using MicroBlog.Core.Interfaces;
using MicroBlog.Helpers;
using MicroBlog.Helpers.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NetCore.AutoRegisterDi;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MicroBlog
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddSingleton(Configuration);

            services.AddSingleton<IEmailConfiguration>(Configuration.GetSection("EmailConfiguration").Get<EmailConfiguration>());

            services.AddDistributedMemoryCache();
            services.AddSession();

            var assemblyToScan = Assembly.Load("MicroBlog");

            //Register Services
            services.RegisterAssemblyPublicNonGenericClasses(assemblyToScan)
                     .Where(x => x.Name.EndsWith("Service"))
                     .AsPublicImplementedInterfaces(ServiceLifetime.Scoped);


            //Register Repositories
            services.RegisterAssemblyPublicNonGenericClasses(assemblyToScan)
                     .Where(x => x.Name.EndsWith("Repository"))
                     .AsPublicImplementedInterfaces(ServiceLifetime.Scoped);


            //Register Helper Classes
            services.RegisterAssemblyPublicNonGenericClasses(assemblyToScan)
                     .Where(x => x.Name.EndsWith("Helper"))
                     .AsPublicImplementedInterfaces(ServiceLifetime.Scoped);

            //Add AutoMapper
            services.AddAutoMapper();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            var connectionString = "Server = localhost,1433; Database = MicroBlog; User ID=sa; Password = lexEME5195..";
            services.AddDbContext<DatabaseContext>(options => options.UseSqlServer(connectionString));

            var hangManConnectionString = "Server = localhost,1433; Database = MicroBlogHangMan; User ID=sa; Password = lexEME5195..";
            services.AddHangfire(config => config.UseSqlServerStorage(Configuration[hangManConnectionString]));

            services.AddIdentity<Entities.ApplicationUser, IdentityRole<long>>(options =>
            {
                //Password Options
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;


                //Lockout Settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;


                //User settings
                options.User.RequireUniqueEmail = true;

            })
            .AddRoles<IdentityRole<long>>()
            .AddEntityFrameworkStores<DatabaseContext>()
            .AddDefaultTokenProviders();


            services.ConfigureApplicationCookie(options => {

                //Cookie Settings

                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);

                options.LoginPath = "/auth/login";
                options.AccessDeniedPath = "/auth/login";
                options.SlidingExpiration = true;

            });

            services.AddMvc(
                        options =>
                        {
                            options.ReturnHttpNotAcceptable = true;
                            options.RespectBrowserAcceptHeader = true;
                        }
                     )
                    .AddXmlSerializerFormatters()
                    .AddJsonOptions(options =>
                    {
                        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; //Ignores cycles in related data serialization
                        if (options.SerializerSettings.ContractResolver != null)
                        {
                            //var castedResolver = options.SerializerSettings.ContractResolver as DefaultContractResolver;
                            //castedResolver.NamingStrategy = null;

                            // Force Camel Case to JSON
                            options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        }
                    })
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }


            app.UseHangfireDashboard();
            app.UseHangfireServer();

            app.UseStaticFiles();   // For the wwwroot folder

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(
                    Path.Combine(Directory.GetCurrentDirectory(), "StaticFiles")),
                RequestPath = "/StaticFiles"
            });
            app.UseHttpsRedirection();
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseMvc(routes => {

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Account}/{action=Register}"
                );
            });
        }
    }
}