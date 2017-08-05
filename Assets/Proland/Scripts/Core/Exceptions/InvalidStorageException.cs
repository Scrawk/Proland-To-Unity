
using System;

namespace Proland
{

	public class InvalidStorageException : ProlandException
	{
		public InvalidStorageException()
		{
			
		}
		
		public InvalidStorageException(string message)
		: base(message)
		{
			
		}
		
		public InvalidStorageException(string message, Exception inner)
		: base(message, inner)
		{
			
		}
	}

}
