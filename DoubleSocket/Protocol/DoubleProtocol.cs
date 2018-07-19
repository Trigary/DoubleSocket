using System;

namespace DoubleSocket.Protocol {
	public static class DoubleProtocol {
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		//TODO better way to handle errors than exceptions: don't throw exception on other-party-disconenct to IO thread, etc.
		// -> implement disconnect events

		//TODO specifically written and randomized tests which test the defragmentation
	}
}
