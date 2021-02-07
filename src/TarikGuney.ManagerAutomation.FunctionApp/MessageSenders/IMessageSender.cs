using System.Collections.Generic;
using System.Threading.Tasks;

namespace TarikGuney.ManagerAutomation.MessageSenders
{
	public interface IMessageSender
	{
		Task SendMessages(IReadOnlyList<string> messages);
	}
}
