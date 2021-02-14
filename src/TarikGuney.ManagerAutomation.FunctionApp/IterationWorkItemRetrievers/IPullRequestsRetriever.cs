using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json.Linq;

namespace TarikGuney.ManagerAutomation.IterationWorkItemRetrievers
{
	public interface IPullRequestsRetriever
	{
		IReadOnlyList<JObject> GetPullRequests();
	}
}
