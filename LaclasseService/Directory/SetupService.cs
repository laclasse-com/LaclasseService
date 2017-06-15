
using Erasme.Http;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class SetupService : HttpRouting
	{
		public SetupService(Setup setup)
		{
			// TODO: ensure only super admin
			// API only available to authenticated users
			BeforeAsync = async(p, c) => await c.EnsureIsAuthenticatedAsync();

			Get["/"] = (p, c) =>
			{
				c.Response.Content = setup.ToJson();
			};
		}
	}
}
