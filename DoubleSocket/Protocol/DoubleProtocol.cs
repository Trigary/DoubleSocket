using System;

namespace DoubleSocket.Protocol {
	public static class DoubleProtocol {
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		//TODO both specifically written and randomized tests which test the defragmentation

		//TODO more tests in general, see whether tests can be configured to fail in case ANY exception
	}
}
