using System;

namespace DoubleSocket.Protocol {
	public static class DoubleProtocol {
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		//TODO more tests in general, both specifically written and randomized tests which test the defragmentation, etc.
	}
}
