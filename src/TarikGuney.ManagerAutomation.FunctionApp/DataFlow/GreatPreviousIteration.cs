using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace TarikGuney.ManagerAutomation.DataFlow
{
    public static class GreatPreviousIteration
    {
        public const string GreatWorkGreeting = "good job";

        public static TransformBlock<List<JObject>, string> Block =>
            new TransformBlock<List<JObject>, string>(workItems =>
            {
                var workItemsByPersons = workItems
                    .Where(wi => wi["fields"] is JObject fields &&
                                 new List<string>() {"Bug", "User Story"}.Contains(fields["System.WorkItemType"]
                                     .Value<string>()) &&
                                 fields.ContainsKey("System.AssignedTo")
                    ).ToLookup(
                        wi => wi["fields"]["System.AssignedTo"]["uniqueName"].Value<string>(), t => t);

                if (!workItemsByPersons.Any())
                {
                    return null;
                }

                var messageBuilder = new StringBuilder();

                foreach (var workItemsByPerson in workItemsByPersons)
                {
                    var anyPendingWorkItems = workItemsByPerson.Any(a =>
                        !a!["fields"]!["System.State"]!.Value<string>()
                            .Equals("Closed", StringComparison.InvariantCultureIgnoreCase));

                    if (anyPendingWorkItems)
                    {
                        continue;
                    }

                    var userDisplayName = workItemsByPerson.First()["fields"]?["System.CreatedBy"]?["displayName"]
                        ?.Value<string>();
                    var userEmail = workItemsByPerson.Key;
                    var devOpsGoogleChatUserMap =
                        Config.DevOpsChatUserMaps.SingleOrDefault(t =>
                            t.AzureDevOpsEmail.Equals(userEmail, StringComparison.InvariantCultureIgnoreCase));

                    var chatDisplayName = devOpsGoogleChatUserMap == null
                        ? userDisplayName
                        : $"<users/{devOpsGoogleChatUserMap.GoogleChatUserId}>";

                    Logger.CurrentLogger.LogInformation(
	                    "BOARD: Closed everything from the previous sprint by the first day of the current sprint {currentIteration}. Assigned to {userEmail}.",
	                    Config.CurrentIteration.Name, userEmail);

                    messageBuilder.Append(
                        $"{chatDisplayName}, {GreatWorkGreeting}! 👏 You *closed* all of *your previous iteration* work items! 🎉 \n\n");
                }

                return messageBuilder.ToString();
            });
    }
}
