using System;

namespace DoubleSocket.Protocol {
	public class DoubleProtocol {
		public static long TimeMillis => DateTimeOffset.Now.ToUnixTimeMilliseconds();

		//TODO better way to handle errors than exceptions: don't throw exception on other-party-disconenct to IO thread

		//TODO ByteBuffer writer actions (Action<ByteBuffer>) instead of passing byte[] parameters:
		// no need to copy byte[] just to add something at the front, etc.
		// final solution should become clear once I determine how the packet modifier pipeline
		// (pre-modifiers, late-modifiers, etc.) should work
	}
}
