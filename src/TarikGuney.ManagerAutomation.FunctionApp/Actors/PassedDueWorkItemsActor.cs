using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.Helpers;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// For the manager's report, this actor finds the work items that passed their due dates.
	/// </summary>
	public class PassedDueWorkItemsActor : ReceiveActor
	{
		private readonly ILogger _logger;
		private readonly AzureDevOpsSettings _azureDevOpsSettings;
		private readonly List<DevOpsChatUserMap> _devOpsChatUserMaps;
		private readonly IterationInfo _currentIteration;

		public PassedDueWorkItemsActor(IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions,
			IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapsOptions,
			IOptions<IterationInfo> currentIterationOptions, ILogger logger)
		{
			_logger = logger;
			_azureDevOpsSettings = azureDevOpsSettingsOptions.Value;
			_devOpsChatUserMaps = devOpsChatUserMapsOptions.Value;
			_currentIteration = currentIterationOptions.Value;

			Receive<IReadOnlyList<JObject>>(HandleIncomingWorkItems);
		}

		private void HandleIncomingWorkItems(IReadOnlyList<JObject> workItems)
		{
			var offendingWorkItems = workItems
				.Where(wi => wi["fields"] is JObject fields &&
				             new List<string> {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
					             .Value<string>()) &&
				             // No need to track the closed or new items
				             !new[] {"new", "closed"}.Contains(fields["System.State"].Value<string>().ToLower()
					             .Trim()) &&
				             fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints") &&
				             fields.ContainsKey("System.AssignedTo")).ToList();

			if (!offendingWorkItems.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(null, false));
			}

			var messages = new List<string>();
			var baseUrl =
				$"https://dev.azure.com/{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Organization)}/" +
				$"{HttpUtility.UrlPathEncode(_azureDevOpsSettings.Project)}/_workitems/edit";

			foreach (var offendingWorkItem in offendingWorkItems)
			{
				var storyPoints = offendingWorkItem["fields"]["Microsoft.VSTS.Scheduling.StoryPoints"].Value<int>();
				var activatedOn =
					DateTime.Parse(offendingWorkItem["fields"]["Microsoft.VSTS.Common.StateChangeDate"]
						.Value<string>()).ToLocalTime();

				// Finding out the weekend days (Saturday and Sunday) in between of activation date and the possible due date.
				var pointsToDays = IterationHelper.PointsToDays(storyPoints);

				var weekendDaysCount =
					DateDiffHelper.CalculateWeekendDays(activatedOn, activatedOn.Add(pointsToDays));

				var possibleDueDate = activatedOn.Add(pointsToDays).Add(TimeSpan.FromDays(weekendDaysCount));

				// If 1 point story is activated on Friday, the due date falls on Saturday.
				// Counting Saturday in to the due date, Sunday becomes the new due date, which is still wrong.
				// The following switch addresses that problem.
				var extraWeekendDays = possibleDueDate.DayOfWeek switch
				{
					DayOfWeek.Saturday => 2,
					DayOfWeek.Sunday => 1,
					_ => 0
				};

				var dueDate = possibleDueDate.Add(TimeSpan.FromDays(extraWeekendDays));

				if (DateTime.Now.Date <= dueDate.Date)
				{
					continue;
				}

				var businessDaysPassed = DateTime.Now.Date - dueDate.Date;

				// todo get the email address of the person.
				var userDisplayName = offendingWorkItem["fields"]?["System.AssignedTo"]?["displayName"]
					?.Value<string>();
				var userEmail = offendingWorkItem["fields"]?["System.CreatedBy"]?["uniqueName"]?.Value<string>();
				var workItemTitle = offendingWorkItem["fields"]?["System.Title"]?.Value<string>();
				var workItemId = offendingWorkItem["id"].Value<string>();
				var workItemUrl = $"{baseUrl}/{workItemId}";
				var currentStatus = offendingWorkItem["fields"]["System.State"].Value<string>();

				// Logging to the application insights with a different tag.
				_logger.LogInformation(
					"MANAGER: Passed due date. \"{workItemId}:{workItemTitle}\" is past due for {businessDaysPassed} days in {currentStatus} state. Activated on {activatedOnDate}, was due on {dueOnDate}, and is assigned to {userEmail}, and estimated {storyPoints} points.",
					workItemId, workItemTitle, businessDaysPassed.TotalDays, currentStatus,
					activatedOn.Date.ToShortDateString(), dueDate.Date.ToShortDateString(), userEmail, storyPoints);

				messages.Add(
					$"<{workItemUrl}|{workItemTitle}> is *past due* for *{businessDaysPassed.TotalDays} business day(s)* with *{currentStatus}* state. Activated on *{activatedOn.Date.ToShortDateString()}*, " +
					$"was due on *{dueDate.Date.ToShortDateString()}*, " +
					$"is assigned to *{userDisplayName}* and estimated *{storyPoints}* points.");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
