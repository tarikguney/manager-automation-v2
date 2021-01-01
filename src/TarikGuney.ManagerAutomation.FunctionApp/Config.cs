using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation
{
	public static class Config
	{
		public static List<DevOpsChatUserMap> DevOpsChatUserMaps { get; set; }
		public static AzureDevOpsSettings AzureDevOpsSettings { get; set; }
		public static EngineeringManagerInfo EngineeringManagerInfo { get; set; }
		public static GoogleChatSettings GoogleChatSettings { get; set; }
		public static HttpClient AzureAuthorizedHttpClient { get; set; }
		public static IterationInfo CurrentIteration { get; set; }

		public static async Task SetSharedSettings(ExecutionContext context)
		{
			var config = GetConfig(context);

			AzureDevOpsSettings = config.GetSection("AzureDevOps").Get<AzureDevOpsSettings>();
			GoogleChatSettings = config.GetSection("GoogleChat").Get<GoogleChatSettings>();
			DevOpsChatUserMaps =
				config["AzureDevOpsUsersMapToGoogleChat"].Split(";").Select(userMap =>
				{
					var userMapArray = userMap.Split(":");
					return new DevOpsChatUserMap()
					{
						AzureDevOpsEmail = userMapArray[0],
						GoogleChatUserId = userMapArray[1]
					};
				}).ToList();
			EngineeringManagerInfo = config.GetSection("EngineeringManagerInfo").Get<EngineeringManagerInfo>();

			AzureAuthorizedHttpClient = new HttpClient();
			AzureAuthorizedHttpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Basic", AzureDevOpsSettings.ApiKey);

			CurrentIteration = await SetCurrentIterationSettings();
		}

		private static async Task<IterationInfo> SetCurrentIterationSettings()
		{
			var currentIterationContent = await AzureAuthorizedHttpClient.GetStringAsync(
				$"https://dev.azure.com/{AzureDevOpsSettings.Organization}/{AzureDevOpsSettings.Project}/{AzureDevOpsSettings.Team}/" +
				$"_apis/work/teamsettings/iterations?api-version=6.1-preview.1&$timeframe=current");

			var iterationJson = JObject.Parse(currentIterationContent).SelectToken($".value[0]") as JObject;

			var iterationInfo = iterationJson!.ToObject<IterationInfo>();
			iterationInfo!.FinishDate = DateTime.Parse(iterationJson!["attributes"]!["finishDate"]!.Value<string>());
			iterationInfo!.StartDate = DateTime.Parse(iterationJson!["attributes"]!["startDate"]!.Value<string>());

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
