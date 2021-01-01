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
	public static class PassedDueWorkItemsTransform
	{
		public static TransformBlock<List<JObject>, string> Block =>
			new TransformBlock<List<JObject>, string>(workItems =>
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
					return null;
				}

				var messageBuilder = new StringBuilder();
				var baseUrl =
					$"https://dev.azure.com/{HttpUtility.UrlPathEncode(Config.AzureDevOpsSettings.Organization)}/" +
					$"{HttpUtility.UrlPathEncode(Config.AzureDevOpsSettings.Project)}/_workitems/edit";

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
					Logger.CurrentLogger.LogInformation(
						"MANAGER: Passed due date. \"{workItemId}:{workItemTitle}\" is past due for {businessDaysPassed} days in {currentStatus} state. Activated on {activatedOnDate}, was due on {dueOnDate}, and is assigned to {userEmail}, and estimated {storyPoints} points.",
						workItemId, workItemTitle, businessDaysPassed.TotalDays, currentStatus,
						activatedOn.Date.ToShortDateString(), dueDate.Date.ToShortDateString(), userEmail, storyPoints);

					messageBuilder.Append(
						$"<{workItemUrl}|{workItemTitle}> is *past due* for *{businessDaysPassed.TotalDays} business day(s)* with *{currentStatus}* state. Activated on *{activatedOn.Date.ToShortDateString()}*, " +
						$"was due on *{dueDate.Date.ToShortDateString()}*, " +
						$"is assigned to *{userDisplayName}* and estimated *{storyPoints}* points.\n\n");
				}

				return messageBuilder.ToString();
			});
	}
}
