using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Akka.Actor;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using TarikGuney.ManagerAutomation.CommMessages;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.Actors
{
	public class PendingPullRequestsActor : ReceiveActor
	{
		private readonly IOptions<List<DevOpsChatUserMap>> _devOpsChatUserMapOptions;
		private readonly IOptions<AzureDevOpsSettings> _azureDevOpsSettingsOptions;

		public PendingPullRequestsActor(IOptions<List<DevOpsChatUserMap>> devOpsChatUserMapOptions,
			IOptions<AzureDevOpsSettings> azureDevOpsSettingsOptions
		)
		{
			_devOpsChatUserMapOptions = devOpsChatUserMapOptions;
			_azureDevOpsSettingsOptions = azureDevOpsSettingsOptions;

			Receive<IReadOnlyList<JObject>>(StartAnalysingPullRequests);
		}

		private void StartAnalysingPullRequests(IReadOnlyList<JObject> pullRequestJObjects)
		{
			if (!pullRequestJObjects.Any())
			{
				Sender.Tell(new ActorResponse<IReadOnlyList<string>>(new List<string>(), false));
			}

			var messages = new List<string>();

			foreach (var pullRequestJObject in pullRequestJObjects)
			{
				var prCreationDate = pullRequestJObject!["creationDate"]!.Value<DateTime>().ToLocalTime().Date;

				var now = DateTime.Now.Date;

				var pendingForDays = (now - prCreationDate).TotalDays;

				// todo adjust this value based on the feedback
				if (pendingForDays < 1)
				{
					continue;
				}

				var prTitle = pullRequestJObject["title"]!.Value<string>();
				var prId = pullRequestJObject["pullRequestId"]!.Value<string>();
				var urlPathEncodedRepoName = HttpUtility.UrlPathEncode(pullRequestJObject["repository"]["name"].Value<string>());
				var organizationName = _azureDevOpsSettingsOptions.Value.Organization;
				var urlPathEncodedProjectName = HttpUtility.UrlPathEncode(_azureDevOpsSettingsOptions.Value.Project);
				var prUrl = $"https://dev.azure.com/{organizationName}/{urlPathEncodedProjectName}/_git/{urlPathEncodedRepoName}/pullrequest/{prId}";
				var creatorEmail = pullRequestJObject["createdBy"]!["uniqueName"]!.Value<string>();
				var devOpsGoogleChatUserMap =
					_devOpsChatUserMapOptions.Value.SingleOrDefault(t =>
						t.AzureDevOpsEmail.Equals(creatorEmail, StringComparison.InvariantCultureIgnoreCase));

				var userDisplayName = pullRequestJObject["createdBy"]!["displayName"]!.Value<string>();

				var chatDisplayName = _devOpsChatUserMapOptions.Value == null
					? userDisplayName
					: $"<users/{devOpsGoogleChatUserMap!.GoogleChatUserId}>";

				messages.Add(
					$"{chatDisplayName}, *the pull request* <{prUrl}|{prTitle}> is pending for *{pendingForDays} day(s)*. " +
					$"Please have it reviewed and merged.");
			}

			Sender.Tell(new ActorResponse<IReadOnlyList<string>>(messages, true));
		}
	}
}
