
using System;

namespace Proland
{
	
	public class CacheCapacityException : ProlandException
	{
		public CacheCapacityException()
		{
			
		}
		
		public CacheCapacityException(string message)
			: base(message)
		{
			
		}
		
		public CacheCapacityException(string message, Exception inner)
			: base(message, inner)
		{
			
		}
	}
	
}
