using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.IterationWorkItemRetrievers
{
	public interface IIterationWorkItemsRetriever
	{
		IReadOnlyList<JObject> GetWorkItems(IterationTimeFrame iteration);
	}
}
