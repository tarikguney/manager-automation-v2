using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks.Dataflow;

namespace TarikGuney.ManagerAutomation.DataFlow
{
    public static class ManagersGoogleChatMessageSenderAction
    {
        public static ActionBlock<string[]> Block => new ActionBlock<string[]>(async messages =>
        {
            var httpClient = new HttpClient();
            var yesterday = DateTime.Now.Subtract(TimeSpan.FromDays(1)).Date.ToShortDateString();
            var greetings = new[]
            {
                $"Hello <users/{Config.EngineeringManagerInfo.GoogleChatUserId}>. Here is the report for *yesterday* ({yesterday}) progress",
            };

            var random = new Random();
            var randomGreeting = greetings[random.Next(0, greetings.Length)];

            var finalMessage = messages.All(string.IsNullOrEmpty)
                ? "The board is looking good and every thing is on track"
                : string.Join("", messages);

            var chatMessage = new
            {
                text = $"{randomGreeting}:\n\n{finalMessage}"
            };

            await httpClient.PostAsJsonAsync(Config.EngineeringManagerInfo.ManagerRemindersGoogleWebhookUrl,
                chatMessage);
        });
    }
}
