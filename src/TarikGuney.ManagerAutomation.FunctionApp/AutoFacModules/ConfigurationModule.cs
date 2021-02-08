using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Autofac;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.AutoFacModules
{
	public class ConfigurationModule: Module
	{
		private readonly ExecutionContext _executionContext;

		public ConfigurationModule(ExecutionContext executionContext)
		{
			_executionContext = executionContext;
		}

		protected override void Load(ContainerBuilder builder)
		{
			var config = GetConfig(_executionContext);

			var azureDevOpsSettings = config.GetSection("AzureDevOps")
				.Get<AzureDevOpsSettings>();
			builder
				.RegisterInstance(
					new OptionsWrapper<AzureDevOpsSettings>(azureDevOpsSettings))
				.As<IOptions<AzureDevOpsSettings>>();

			builder
				.RegisterInstance(
					new OptionsWrapper<GoogleChatSettings>(config.GetSection("GoogleChat")
						.Get<GoogleChatSettings>()))
				.As<IOptions<GoogleChatSettings>>();


			builder
				.RegisterInstance(
					new OptionsWrapper<List<DevOpsChatUserMap>>(
						config["AzureDevOpsUsersMapToGoogleChat"].Split(";").Select(userMap =>
						{
							var userMapArray = userMap.Split(":");
							return new DevOpsChatUserMap()
							{
								AzureDevOpsEmail = userMapArray[0],
								GoogleChatUserId = userMapArray[1]
							};
						}).ToList()))
				.As<IOptions<List<DevOpsChatUserMap>>>();


			builder
				.RegisterInstance(
					new OptionsWrapper<EngineeringManagerInfo>(config.GetSection("EngineeringManagerInfo")
						.Get<EngineeringManagerInfo>()))
				.As<IOptions<EngineeringManagerInfo>>();

			var authorizedHttpClient = new HttpClient();
			authorizedHttpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Basic", azureDevOpsSettings.ApiKey);

			builder
				.RegisterInstance(authorizedHttpClient)
				.As<HttpClient>();

			var currentIterationInfo = GetCurrentIterationSettings(authorizedHttpClient, azureDevOpsSettings);
			builder
				.RegisterInstance(
					new OptionsWrapper<CurrentIterationInfo>(currentIterationInfo))
				.As<IOptions<CurrentIterationInfo>>();
		}

		private static CurrentIterationInfo GetCurrentIterationSettings(HttpClient azureAuthorizedHttpClient,
			AzureDevOpsSettings azureDevOpsSettings)
		{
			var currentIterationContent = azureAuthorizedHttpClient.GetStringAsync(
				$"https://dev.azure.com/{azureDevOpsSettings.Organization}/{azureDevOpsSettings.Project}/{azureDevOpsSettings.Team}/" +
				$"_apis/work/teamsettings/iterations?api-version=6.1-preview.1&$timeframe=current").Result;

			var iterationJson = JObject.Parse(currentIterationContent).SelectToken($".value[0]") as JObject;

			var iterationInfo = iterationJson!.ToObject<CurrentIterationInfo>();
			iterationInfo!.FinishDate = DateTime.Parse(iterationJson!["attributes"]!["finishDate"]!.Value<string>());
			iterationInfo!.StartDate = DateTime.Parse(iterationJson!["attributes"]!["startDate"]!.Value<string>());

			// todo This can be moved to the class itself. Each iteration child class can now set their TimeFrame internally.
			iterationInfo.TimeFrame = iterationJson!["attributes"]!["timeFrame"]!.Value<string>().ToLower() switch
			{
				"current" => IterationTimeFrame.Current,
				"past" => IterationTimeFrame.Previous,
				"future" => IterationTimeFrame.Next,
				_ => IterationTimeFrame.Current
			};

			return iterationInfo;
		}

		private static IConfigurationRoot GetConfig(ExecutionContext context)
		{
			var config = new ConfigurationBuilder()
				.SetBasePath(context.FunctionAppDirectory)
				.AddJsonFile("local.settings.json", true, reloadOnChange: true)
				.AddJsonFile("secrets/appsettings.personal.json", true, reloadOnChange: true)
				//.AddJsonFile("secrets/appsettings.msi.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables()
				.Build();
			return config;
		}
	}
}
