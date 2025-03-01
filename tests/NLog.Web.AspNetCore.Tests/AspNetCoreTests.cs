﻿#if ASP_NET_CORE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace NLog.Web.Tests
{
    public class AspNetCoreTests : TestBase, IDisposable
    {
        #region IDisposable

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            LogManager.Configuration = null;
            GC.SuppressFinalize(this);
        }

        #endregion

        [Fact]
        public void UseNLogShouldLogTest()
        {
            var webhost = CreateWebHost();

            var loggerFact = GetLoggerFactory(webhost);

            Assert.NotNull(loggerFact);

            var configuration = CreateConfigWithMemoryTarget(out var target, "${logger}|${message}");

            LogManager.Setup().RegisterNLogWeb(serviceProvider: webhost.Services).LoadConfiguration(configuration);

            var logger = loggerFact.CreateLogger("logger1");

            logger.LogError("error1");

            var logged = target.Logs;

            Assert.Single(logged);
            Assert.Equal("logger1|error1", logged.First());
        }

        [Fact]
        public void UseNLog_ReplaceLoggerFactory()
        {
            var webhost = CreateWebHost(new NLogAspNetCoreOptions() { ReplaceLoggerFactory = true });

            // Act
            var loggerFactory = webhost.Services.GetService<ILoggerFactory>();
            var loggerProvider = webhost.Services.GetService<ILoggerProvider>();

            // Assert
            Assert.Equal(typeof(NLogLoggerFactory), loggerFactory.GetType());
            Assert.Equal(typeof(NLogLoggerProvider), loggerProvider.GetType());
        }

#if !ASP_NET_CORE2
        [Fact]
        public void LoadConfigurationFromAppSettingsShouldLogTest()
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(AspNetCoreTests), Guid.NewGuid().ToString()).Replace("\\", "/");
            var appSettings = System.IO.Path.Combine(tempPath, "appsettings.json");

            try
            {
                // Arrange
                CreateJsonConfigFile(tempPath, appSettings);

                // Act
                var logFactory = new LogFactory();
                var logger = logFactory.Setup().LoadConfigurationFromAppSettings(basePath: tempPath).GetCurrentClassLogger();
                logger.Info("Hello World");

                // Assert
                logFactory.Dispose();
                var fileOutput = System.IO.File.ReadAllText(System.IO.Path.Combine(tempPath, "hello.txt"));
                Assert.Contains("Hello World", fileOutput);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }
            }
        }

        [Fact]
        public void LoadConfigurationFromCustomJsonShouldLogTest()
        {
            string filename = "nlog-config.json";
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(AspNetCoreTests), Guid.NewGuid().ToString()).Replace("\\", "/");
            var appSettings = System.IO.Path.Combine(tempPath, filename);

            try
            {
                // Arrange
                CreateJsonConfigFile(tempPath, appSettings);

                // Act
                var logFactory = new LogFactory();
                var logger = logFactory.Setup().LoadConfigurationFromAppSettings(filename, basePath: tempPath).GetCurrentClassLogger();
                logger.Info("Hello World");

                // Assert
                logFactory.Dispose();
                var fileOutput = System.IO.File.ReadAllText(System.IO.Path.Combine(tempPath, "hello.txt"));
                Assert.Contains("Hello World", fileOutput);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }
            }
        }

        [Fact]
        public void LoadConfigurationWithCustomPipelineShouldLogTest()
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(AspNetCoreTests), Guid.NewGuid().ToString()).Replace("\\", "/");
            var appSettings = System.IO.Path.Combine(tempPath, "appsettings.json");

            try
            {
                // Arrange
                CreateJsonConfigFile(tempPath, appSettings);

                // Act
                var logFactory = new LogFactory();
                var logger = logFactory.Setup().LoadConfigurationUsingConfigBuilder(() => {
                    return new ConfigurationBuilder()
                        .SetBasePath(tempPath)
                        .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                        .AddEnvironmentVariables(prefix: "DOTNET_")
                        .AddJsonFile("appsettings.json", false, false)
                        .AddEnvironmentVariables();
                }).GetCurrentClassLogger();

                logger.Info("Hello World");

                // Assert
                logFactory.Dispose();
                var fileOutput = System.IO.File.ReadAllText(System.IO.Path.Combine(tempPath, "hello.txt"));
                Assert.Contains("Hello World", fileOutput);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }
            }
        }

        [Fact]
        public void LoadConfigurationFromAppSettingsShouldLogTest2()
        {
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(AspNetCoreTests), Guid.NewGuid().ToString()).Replace("\\", "/");
            var appSettings = System.IO.Path.Combine(tempPath, "appsettings.json");

            try
            {
                // Arrange
                System.IO.Directory.CreateDirectory(tempPath);
                System.IO.File.AppendAllText(appSettings, @"{
                  ""basepath"": """ + tempPath + @"""
                }");

                System.IO.File.AppendAllText(System.IO.Path.Combine(tempPath, "nlog.config"), @"<nlog>
                    <targets>
                        <target type=""file"" name=""logfile"" layout=""${message}"" fileName=""${configsetting:basepath}/hello.txt"" />
                    </targets>
                    <rules>
                        <logger name=""*"" minLevel=""Debug"" writeTo=""logfile"" />
                    </rules>
                </nlog>");

                // Act
                var logFactory = new LogFactory();
                var logger = logFactory.Setup().LoadConfigurationFromAppSettings(basePath: tempPath).GetCurrentClassLogger();
                logger.Info("Hello World");

                // Assert
                logFactory.Dispose();
                var fileOutput = System.IO.File.ReadAllText(System.IO.Path.Combine(tempPath, "hello.txt"));
                Assert.Contains("Hello World", fileOutput);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempPath))
                {
                    System.IO.Directory.Delete(tempPath, true);
                }
            }
        }
#endif

        private static LoggingConfiguration CreateConfigWithMemoryTarget(out MemoryTarget target, Layout layout)
        {
            var configuration = new LoggingConfiguration();
            target = new MemoryTarget("target1") { Layout = layout };

            configuration.AddRuleForAllLevels(target);
            return configuration;
        }

        [Fact]
        public void UseAspNetWithoutRegister()
        {
            try
            {
                //clear so next time it's rebuild
                ConfigurationItemFactory.Default = null;

                var webhost = CreateWebHost();

                var configuration = CreateConfigWithMemoryTarget(out var target, "${logger}|${message}|${aspnet-item:key1}");

                LogManager.Setup().RegisterNLogWeb(serviceProvider: webhost.Services).LoadConfiguration(configuration);

                var httpContext = webhost.Services.GetService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext();
                httpContext.Items["key1"] = "value1";

                var loggerFact = GetLoggerFactory(webhost);

                var logger = loggerFact.CreateLogger("logger1");

                logger.LogError("error1");

                var logged = target.Logs;

                Assert.Single(logged);
                Assert.Equal("logger1|error1|value1", logged.First());
            }
            finally
            {
                //clear so next time it's rebuild
                ConfigurationItemFactory.Default = null;
            }
        }

        [Fact]
        public void RegisterHttpContext()
        {
            var webhost = CreateWebHost();
            Assert.NotNull(webhost.Services.GetService<IHttpContextAccessor>());
        }

        [Fact]
        public void SkipRegisterHttpContext()
        {
            var webhost = CreateWebHost(new NLogAspNetCoreOptions { RegisterHttpContextAccessor = false });
            Assert.Null(webhost.Services.GetService<IHttpContextAccessor>());
        }


        [Fact]
        public void UseNLog_LoadConfigurationFromSection()
        {
            var host = CreateWebHostBuilder().ConfigureAppConfiguration((context, config) =>
            {
                var memoryConfig = new Dictionary<string, string>();
                memoryConfig["NLog:Rules:0:logger"] = "*";
                memoryConfig["NLog:Rules:0:minLevel"] = "Trace";
                memoryConfig["NLog:Rules:0:writeTo"] = "inMemory";
                memoryConfig["NLog:Targets:inMemory:type"] = "Memory";
                memoryConfig["NLog:Targets:inMemory:layout"] = "${logger}|${message}|${configsetting:NLog.Targets.inMemory.type}";
                config.AddInMemoryCollection(memoryConfig);
            }).UseNLog(new NLogAspNetCoreOptions() { LoggingConfigurationSectionName = "NLog", ReplaceLoggerFactory = true }).Build();

            var loggerFact = host.Services.GetService<ILoggerFactory>();
            var logger = loggerFact.CreateLogger("logger1");
            logger.LogError("error1");

            var loggerProvider = host.Services.GetService<ILoggerProvider>() as NLogLoggerProvider;
            var logged = loggerProvider.LogFactory.Configuration.FindTargetByName<Targets.MemoryTarget>("inMemory").Logs;

            Assert.Single(logged);
            Assert.Equal("logger1|error1|Memory", logged[0]);
        }

        [Fact]
        public void UseNLog_ReplaceLoggerFactory_FromConfiguration()
        {
            var host = CreateWebHostBuilder().ConfigureAppConfiguration((context, config) =>
            {
                var memoryConfig = new Dictionary<string, string>();
                memoryConfig["Logging:NLog:ReplaceLoggerFactory"] = "True";
                memoryConfig["Logging:NLog:RemoveLoggerFactoryFilter"] = "False";
                config.AddInMemoryCollection(memoryConfig);
            }).UseNLog().Build();

            // Act
            var loggerFactory = host.Services.GetService<ILoggerFactory>();
            var loggerProvider = host.Services.GetService<ILoggerProvider>();

            // Assert
            Assert.Equal(typeof(NLogLoggerFactory), loggerFactory.GetType());
            Assert.Equal(typeof(NLogLoggerProvider), loggerProvider.GetType());
        }

        private static IWebHostBuilder CreateWebHostBuilder()
        {
            var builder =
                Microsoft.AspNetCore.WebHost.CreateDefaultBuilder()
                    .Configure(c => c.New());//.New needed, otherwise:
                                             // Unhandled Exception: System.ArgumentException: A valid non-empty application name must be provided.
                                             // Parameter name: applicationName
            return builder;
        }

        /// <summary>
        /// Create webhost with UseNlog
        /// </summary>
        /// <returns></returns>
        private static IWebHost CreateWebHost(NLogAspNetCoreOptions options = null)
        {
            return CreateWebHostBuilder()
                .UseNLog(options)
                .Build();
        }

        private static ILoggerFactory GetLoggerFactory(IWebHost webhost)
        {
            return webhost.Services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        }

        private static void CreateJsonConfigFile(string path, string fileName)
        {
            System.IO.Directory.CreateDirectory(path);
            System.IO.File.AppendAllText(fileName, @"{
                  ""basepath"": """ + path + @""",
                  ""NLog"": {
                    ""throwConfigExceptions"": true,
                    ""targets"": {
                        ""logfile"": {
                            ""type"": ""File"",
                            ""fileName"": ""${configsetting:basepath}/hello.txt"",
                            ""layout"": ""${message}""
                        }
                    },
                    ""rules"": [
                      {
                        ""logger"": ""*"",
                        ""minLevel"": ""Debug"",
                        ""writeTo"": ""logfile""
                      }
                    ]
                  }
                }");
        }
    }
}

#endif