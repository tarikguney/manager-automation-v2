using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.IterationWorkItemRetrievers
{
	public class AzureDevOpsAllPullRequestsRetriever : IPullRequestsRetriever
	{
		private readonly HttpClient _authorizedHttpClient;
		private readonly IOptions<AzureDevOpsSettings> _azureDevOpsSettingsOptions;

		public AzureDevOpsAllPullRequestsRetriever(HttpClient authorizedHttpClient,
			IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions)
		{
			_authorizedHttpClient = authorizedHttpClient;
			_azureDevOpsSettingsOptions = azureDevOpsSettingsOptions;
		}

		public IReadOnlyList<JObject> GetPullRequests()
		{
			var pullRequestsAPIEndpoint =
				$"https://dev.azure.com/{_azureDevOpsSettingsOptions.Value.Organization}/{_azureDevOpsSettingsOptions.Value.Project}/_apis/git/pullrequests?api-version=6.0";
			var resultTask = _authorizedHttpClient.GetAsync(pullRequestsAPIEndpoint);
			var prRequestResponse = resultTask.Result;

			if (prRequestResponse.IsSuccessStatusCode)
			{
				return JObject.Parse(prRequestResponse.Content.ReadAsStringAsync().Result).SelectTokens("$.value[*]")
					.Cast<JObject>().ToList();
			}
			// Todo Think about a better exception message;
			throw new HttpResponseException(prRequestResponse);
		}
	}
}
