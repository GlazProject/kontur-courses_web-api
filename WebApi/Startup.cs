using System;
using System.Buffers;
using System.Reflection;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using WebApi.Models;

namespace WebApi
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
            services
                .AddSingleton<IUserRepository, InMemoryUserRepository>()
                .AddAutoMapper(cfg =>
                {
                    cfg.CreateMap<UserEntity, UserDto>()
                        .ForMember(dest => dest.FullName,
                            opt => opt.MapFrom(src => src.LastName + src.FirstName));
                    cfg.CreateMap<UserForPostDto, UserEntity>();
                    cfg.CreateMap<UserForPutDto, UserEntity>();
                    cfg.CreateMap<UserEntity, UserForPutDto>();
                }, Array.Empty<Assembly>())
                .AddControllers(options =>
                {
                    // Этот OutputFormatter позволяет возвращать данные в XML, если требуется.
                    options.OutputFormatters.Add(new XmlSerializerOutputFormatter());
                    options.OutputFormatters.Insert(0, new NewtonsoftJsonOutputFormatter(new JsonSerializerSettings
                        {
                            ContractResolver = new 
                                CamelCasePropertyNamesContractResolver()
                        }, ArrayPool<char>.Shared, options));
                    // Эта настройка позволяет отвечать кодом 406 Not Acceptable на запросы неизвестных форматов.
                    options.ReturnHttpNotAcceptable = true;
                    // Эта настройка приводит к игнорированию заголовка Accept, когда он содержит */*
                    // Здесь она нужна, чтобы в этом случае ответ возвращался в формате JSON
                    options.RespectBrowserAcceptHeader = true;
                })
                .ConfigureApiBehaviorOptions(options => {
                    options.SuppressModelStateInvalidFilter = true;
                    options.SuppressMapClientErrors = true;
                })
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Populate;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
