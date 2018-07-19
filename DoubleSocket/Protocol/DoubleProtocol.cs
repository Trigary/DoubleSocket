using System;

namespace DoubleSocket.Protocol {
	public static class DoubleProtocol {
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		//TODO specifically written and randomized tests which test the defragmentation

		//TODO test whether calling shutdown on sockets before they have been "started" causes any errors

		//TODO the current TCP protocol is not secure: the payloads are encrypted, but that does little good
	}
}
