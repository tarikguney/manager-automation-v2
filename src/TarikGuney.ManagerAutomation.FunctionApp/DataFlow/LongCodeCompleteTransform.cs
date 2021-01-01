using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks.Dataflow;
using System.Web;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.Helpers;

namespace TarikGuney.ManagerAutomation.DataFlow
{
	public static class LongCodeCompleteTransform
	{
		public static TransformBlock<List<JObject>, string> Block =>
			new TransformBlock<List<JObject>, string>(workItems =>
			{
				// Property names that has periods in them won't be parsed by Json.NET as opposed to online JSON Parser tools
				// eg. $.value[?(@.fields['Microsoft.VSTS.Scheduling.StoryPoints'] == null && @.fields['System.AssignedTo'] != null)]
				// Because of that reason, I had to use enumeration below.
				var offendingWorkItems = workItems
					.Where(wi => wi["fields"] is JObject fields &&
					             new List<string>() {"Bug", "User Story"}.Contains(fields!["System.WorkItemType"]!
						             .Value<string>()) &&
					             new List<string> {"PR Submitted", "Resolved"}.Contains(fields!["System.State"]!
						             .Value<string>()) &&
					             DateTime.Parse(fields!["Microsoft.VSTS.Common.StateChangeDate"]!.Value<string>())
						             .ToLocalTime() <
					             DateTime.Now.Date.Subtract(TimeSpan.FromDays(1)) &&
					             fields.ContainsKey("System.AssignedTo")).ToList();

				if (!offendingWorkItems.Any())
				{
					return null;
				}

				var messageBuilder = new StringBuilder();
				var baseUrl =
					$"https://dev.azure.com/{HttpUtility.UrlPathEncode(Config.AzureDevOpsSettings.Organization)}/" +
					$"{HttpUtility.UrlPathEncode(Config.AzureDevOpsSettings.Project)}/_workitems/edit";

				foreach (var offendingWorkItem in offendingWorkItems)
				{
					var userDisplayName = offendingWorkItem["fields"]?["System.AssignedTo"]?["displayName"]
						?.Value<string>();
					var userEmail = offendingWorkItem["fields"]?["System.AssignedTo"]?["uniqueName"]?.Value<string>();
					var devOpsGoogleChatUserMap =
						Config.DevOpsChatUserMaps.SingleOrDefault(t =>
							t.AzureDevOpsEmail.Equals(userEmail, StringComparison.InvariantCultureIgnoreCase));
					var workItemTitle = offendingWorkItem["fields"]!["System.Title"]!.Value<string>();
					var workItemId = offendingWorkItem["id"].Value<string>();
					var workItemUrl = $"{baseUrl}/{workItemId}";
					var workItemState = offendingWorkItem!["fields"]!["System.State"]!.Value<string>();
					var lastStateChange =
						DateTime.Parse(offendingWorkItem["fields"]!["Microsoft.VSTS.Common.StateChangeDate"]!
							.Value<string>()).ToLocalTime();

					var now = DateTime.Now.Date;
					var weekendCounts = DateDiffHelper.CalculateWeekendDays(lastStateChange, now);
					var idleForTimeSpan = now - lastStateChange.Date - TimeSpan.FromDays(weekendCounts);

					var chatDisplayName = devOpsGoogleChatUserMap == null
						? userDisplayName
						: $"<users/{devOpsGoogleChatUserMap.GoogleChatUserId}>";

					Logger.CurrentLogger.LogInformation(
						"BOARD: Pending in incomplete state of {currentState} for {pendingForDays} days. Story \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
						workItemState, idleForTimeSpan.TotalDays, workItemId, workItemTitle, userEmail,
						Config.CurrentIteration.Name);

					// todo Include pr follow up message for PR Submitted state.
					messageBuilder.Append(
						$"{chatDisplayName}, *follow up* on your work of <{workItemUrl}|{workItemTitle}>. " +
						$"It is in *{workItemState}* state for *{idleForTimeSpan.TotalDays}* day(s). Don't forget to *have it verified* by a fellow engineer!\n\n");
				}

				return messageBuilder.ToString();
			});
	}
}
