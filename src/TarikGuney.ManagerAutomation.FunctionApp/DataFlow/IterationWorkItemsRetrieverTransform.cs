using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.DataFlow
{
	public class IterationWorkItemsRetrieverTransform
	{
		private readonly AzureDevOpsSettings _settings;
		private readonly HttpClient _httpClient;

		private IterationWorkItemsRetrieverTransform(AzureDevOpsSettings settings, HttpClient httpClient)
		{
			_settings = settings;
			_httpClient = httpClient;
		}

		public static TransformBlock<IterationTimeFrame, List<JObject>> Block =>
			new TransformBlock<IterationTimeFrame, List<JObject>>(async iteration =>
			{
				var retriever =
					new IterationWorkItemsRetrieverTransform(Config.AzureDevOpsSettings,
						Config.AzureAuthorizedHttpClient);
				var workItemIds = await retriever.GetWorkItemIdsByWiql(iteration);
				return await retriever.GetWorkItems(workItemIds);
			});

		private async Task<IEnumerable<long>> GetWorkItemIdsByWiql(IterationTimeFrame iterationTimeFrame)
		{
			var iterationQueryValue = iterationTimeFrame switch
			{
				IterationTimeFrame.Current => "@CurrentIteration",
				IterationTimeFrame.Previous => "@CurrentIteration - 1",
				IterationTimeFrame.Next => "@CurrentIteration + 1",
				_ => "@CurrentIteration"
			};

			var httpResponse = await _httpClient.PostAsJsonAsync(
				$"https://dev.azure.com/{_settings.Organization}/{_settings.Project}/{_settings.Team}/_apis/wit/wiql?api-version=6.0",
				new
				{
					query =
						$"Select [System.Id] From WorkItems Where [System.WorkItemType] IN ('Bug','User Story') AND " +
						$"[State] <> 'Removed' AND [System.IterationPath] = {iterationQueryValue}"
				});

			var content = await httpResponse.Content.ReadAsStringAsync();
			// todo check if the content is null or empty and return appropriate response.
			return JObject.Parse(content)!.SelectTokens("$.workItems[*].id")!.Select(a => a.Value<long>());
		}

		private async Task<List<JObject>> GetWorkItems(IEnumerable<long> workItemIds)
		{
			var result = await _httpClient.PostAsJsonAsync(
				$"https://dev.azure.com/{_settings.Organization}/{_settings.Project}/_apis/wit/workitemsbatch?api-version=6.1-preview.1",
				new WorkItemMessage() {Ids = workItemIds.ToList()}
			);
			var content = await result.Content.ReadAsStringAsync();
			return JObject.Parse(content).SelectTokens("$.value[*]").Cast<JObject>().ToList();
		}
	}
}
