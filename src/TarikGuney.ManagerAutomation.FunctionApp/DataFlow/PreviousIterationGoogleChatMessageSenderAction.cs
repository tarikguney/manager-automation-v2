using System.Linq;
using System.Net.Http;
using System.Threading.Tasks.Dataflow;

namespace TarikGuney.ManagerAutomation.DataFlow
{
    public static class PreviousIterationGoogleChatMessageSenderAction
    {
        public static ActionBlock<string[]> Block => new ActionBlock<string[]>(async messages =>
        {
            var allCompleted = messages.All(m => string.IsNullOrEmpty(m) ||
                                                 m.ToLower().Contains(
                                                     GreatPreviousIteration.GreatWorkGreeting.ToLower()));

            var httpClient = new HttpClient();

            var actionMessage = allCompleted
                ? "\n\n*Great work*, <users/all>! 👏🎉👏🎉 *All of the work items are closed from the previous sprint*!"
                : "Unfortunately, there are some *incomplete work items from the previous sprint.* " +
                  "Please review and complete them *before the sprint kickoff meeting*";

            var chatMessage = new
            {
                text = $"Good morning team! 👋 Welcome to the {Config.CurrentIteration.Name}! 🎉 {actionMessage}\n\n" +
                       string.Join("", messages)
            };
            await httpClient.PostAsJsonAsync(Config.GoogleChatSettings.WebhookUrl, chatMessage);
        });
    }
}
