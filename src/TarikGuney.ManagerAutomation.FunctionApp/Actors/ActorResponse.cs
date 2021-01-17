namespace TarikGuney.ManagerAutomation.Actors
{
	/// <summary>
	/// Encapsulates an immutable response from an actor. It provides an extensibility point for
	/// the actor communication.
	/// </summary>
	/// <typeparam name="T">The type of the payload.</typeparam>
	public class ActorResponse<T>
	{
		public T Content { get; }
		public bool Success { get; }

		public ActorResponse(T content, bool success)
		{
			Content = content;
			Success = success;
		}
	}
}
