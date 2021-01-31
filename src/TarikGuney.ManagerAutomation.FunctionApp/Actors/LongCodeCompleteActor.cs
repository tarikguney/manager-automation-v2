using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.Helpers;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// During the sprint and on the first day of the sprint for the last sprint,
	/// this actor finds the works items that are not closed/completed. Closed
	/// is the items that have "Closed" as the state.
	/// </summary>
	public class LongCodeCompleteActor : ReceiveActor
	{
		private readonly ILogger _logger;
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly CurrentIterationInfo _currentIteration;

		public LongCodeCompleteActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<CurrentIterationInfo> currentIterationOptions, ILogger logger)
		{
			_logger = logger;
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_currentIteration = currentIterationOptions.Value;

			Receive<IReadOnlyList<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(IReadOnlyList<JObject> workItems)
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
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(new List<string>(), false));
			}

			var messages = new List<string>();
			var baseUrl =
				$"https://dev.azure.com/{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Organization)}/" +
				$"{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Project)}/_workitems/edit";

			foreach (var offendingWorkItem in offendingWorkItems)
			{
				var userDisplayName = offendingWorkItem["fields"]?["System.AssignedTo"]?["displayName"]
					?.Value<string>();
				var userEmail = offendingWorkItem["fields"]?["System.AssignedTo"]?["uniqueName"]?.Value<string>();
				var devOpsGoogleChatUserMap =
					_devOpsChatUserMaps.SingleOrDefault(t =>
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

				_logger.LogInformation(
					"BOARD: Pending in incomplete state of {currentState} for {pendingForDays} days. Story \"{workItemId}:{workItemTitle}\". Assigned to {userEmail} in {currentIteration}.",
					workItemState, idleForTimeSpan.TotalDays, workItemId, workItemTitle, userEmail,
					_currentIteration.Name);

				// todo Include pr follow up message for PR Submitted state.
				messages.Append(
					$"{chatDisplayName}, *follow up* on your work of <{workItemUrl}|{workItemTitle}>. " +
					$"It is in *{workItemState}* state for *{idleForTimeSpan.TotalDays}* day(s). Don't forget to *have it verified* by a fellow engineer!");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
