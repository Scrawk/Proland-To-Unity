
using System;

namespace Proland
{
	
	public class InvalidParameterException : ProlandException
	{
		public InvalidParameterException()
		{
			
		}
		
		public InvalidParameterException(string message)
			: base(message)
		{
			
		}
		
		public InvalidParameterException(string message, Exception inner)
			: base(message, inner)
		{
			
		}
	}
	
}

