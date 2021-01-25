using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.IterationWorkItemRetrievers
{
	public class AzureDevOpsIterationWorkItemsRetriever : IIterationWorkItemsRetriever
	{
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly HttpClient _authorizedHttpClient;

		public AzureDevOpsIterationWorkItemsRetriever(HttpClient authorizedAuthorizedHttpClient,
			IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions
		)
		{
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_authorizedHttpClient = authorizedAuthorizedHttpClient;
		}

		public IReadOnlyList<JObject> GetWorkItems(IterationTimeFrame iteration)
		{
			var workItemIds = GetWorkItemIdsByWiql(iteration);
			return GetWorkItems(workItemIds);
		}

		private IReadOnlyList<long> GetWorkItemIdsByWiql(IterationTimeFrame iterationTimeFrame)
		{
			var iterationQueryValue = iterationTimeFrame switch
			{
				IterationTimeFrame.Current => "@CurrentIteration",
				IterationTimeFrame.Previous => "@CurrentIteration - 1",
				IterationTimeFrame.Next => "@CurrentIteration + 1",
				_ => "@CurrentIteration"
			};

			var httpResponse = _authorizedHttpClient.PostAsJsonAsync(
				$"https://dev.azure.com/{_azureDevOpsSettings.Organization}/{_azureDevOpsSettings.Project}/{_azureDevOpsSettings.Team}/_apis/wit/wiql?api-version=6.0",
				new
				{
					query =
						$"Select [System.Id] From WorkItems Where [System.WorkItemType] IN ('Bug','User Story') AND " +
						$"[State] <> 'Removed' AND [System.IterationPath] = {iterationQueryValue}"
				}).Result;

			var content = httpResponse.Content.ReadAsStringAsync().Result;
			// todo check if the content is null or empty and return appropriate response.
			return JObject.Parse(content)!.SelectTokens("$.workItems[*].id")!.Select(a => Extensions.Value<long>(a))
				.ToList();
		}

		private IReadOnlyList<JObject> GetWorkItems(IEnumerable<long> workItemIds)
		{
			var result = _authorizedHttpClient.PostAsJsonAsync(
				$"https://dev.azure.com/{_azureDevOpsSettings.Organization}/{_azureDevOpsSettings.Project}/_apis/wit/workitemsbatch?api-version=6.1-preview.1",
				new WorkItemMessage() {Ids = workItemIds.ToList()}
			).Result;
			var content = result.Content.ReadAsStringAsync().Result;
			return JObject.Parse(content).SelectTokens("$.value[*]").Cast<JObject>().ToList();
		}
	}
}
