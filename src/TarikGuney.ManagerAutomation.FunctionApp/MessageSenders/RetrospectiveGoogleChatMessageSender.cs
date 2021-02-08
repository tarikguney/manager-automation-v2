using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TarikGuney.ManagerAutomation.Actors;
using TarikGuney.ManagerAutomation.SettingsModels;

namespace TarikGuney.ManagerAutomation.MessageSenders
{
	public class RetrospectiveGoogleChatMessageSender : IRetrospectiveMessageSender
	{
		private readonly IOptions<CurrentIterationInfo> _currentIterationOptions;
		private readonly IOptions<GoogleChatSettings> _googleChatSettingsOptions;

		public RetrospectiveGoogleChatMessageSender(IOptions<CurrentIterationInfo> _currentIterationOptions,
			IOptions<GoogleChatSettings> _googleChatSettingsOptions)
		{
			this._currentIterationOptions = _currentIterationOptions;
			this._googleChatSettingsOptions = _googleChatSettingsOptions;
		}

		public async Task SendMessages(IReadOnlyList<string> messages)
		{
			var allCompleted = messages.All(m => string.IsNullOrEmpty(m) ||
			                                     m.ToLower().Contains(
				                                     GreatPreviousIterationActor.GreatWorkGreeting.ToLower()));

			var httpClient = new HttpClient();

			var actionMessage = allCompleted
				? "\n\n*Great work*, <users/all>! 👏🎉👏🎉 *All of the work items are closed from the previous sprint*!"
				: "Unfortunately, there are some *incomplete work items from the previous sprint.* " +
				  "Please review and complete them *before the sprint kickoff meeting*";

			var chatMessage = new
			{
				text = $"Good morning team! 👋 Welcome to the {_currentIterationOptions.Value.Name}! 🎉 {actionMessage}\n\n" +
				       string.Join("\n", messages)
			};

			await httpClient.PostAsJsonAsync(_googleChatSettingsOptions.Value.WebhookUrl, chatMessage);
		}
	}
}
