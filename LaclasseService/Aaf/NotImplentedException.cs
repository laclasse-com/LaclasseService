using System;
using System.Runtime.Serialization;

namespace Laclasse.Aaf
{
	[Serializable]
	class NotImplentedException : Exception
	{
		public NotImplentedException()
		{
		}

		public NotImplentedException(string message) : base(message)
		{
		}

		public NotImplentedException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected NotImplentedException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
		}
	}
}