using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace DoubleSocket.Protocol {
	/// <summary>
	/// A utility class which help the disconnecting, error handling and handles the packet reassembly.
	/// </summary>
	public class TcpHelper {
		/// <summary>
		/// Fired when a packet was reassembled.
		/// </summary>
		/// <param name="sender">The socket which was specified as the sender of the packet
		/// or null, if none was specified.</param>
		/// <param name="buffer">The buffer containing the assembled packet.</param>
		/// <param name="offset">The offset of the buffer.</param>
		/// <param name="size">The size of the packet.</param>
		public delegate void AssembledPacketHandler(Socket sender, byte[] buffer, int offset, int size);

		private readonly byte[] _packetSizeBytes = new byte[2];
		private readonly byte[] _packetBuffer = new byte[DoubleProtocol.TcpBufferArraySize];
		private readonly AssembledPacketHandler _assembledPacketHandler;
		private ushort _packetSize;
		private int _savedPacketSizeBytes;
		private int _savedPayloadBytes;

		/// <summary>
		/// Creates a new instance with the specified handler. Each TCP connection should have its own instance.
		/// </summary>
		/// <param name="assembledPacketHandler">The handler of reassembled packets.</param>
		public TcpHelper(AssembledPacketHandler assembledPacketHandler) {
			_assembledPacketHandler = assembledPacketHandler;
		}



		/// <summary>
		/// Disconnects the speicifed socket asynchronously.
		/// </summary>
		/// <param name="socket">The socekt to disconnect.</param>
		/// <param name="eventArgsQueue">A queue which may contain SocketAsyncEventArgs
		/// which can be used for the disconnect operation.</param>
		/// <param name="previousHandler">The previous handler of the SocketAsyncEventArgs found in the queue.</param>
		public static void DisconnectAsync(Socket socket, Queue<SocketAsyncEventArgs> eventArgsQueue,
											EventHandler<SocketAsyncEventArgs> previousHandler) {
			if (!socket.Connected) {
				socket.Close();
				return;
			}

			SocketAsyncEventArgs eventArgs;
			if (eventArgsQueue.Count == 0) {
				eventArgs = new SocketAsyncEventArgs();
			} else {
				eventArgs = eventArgsQueue.Dequeue();
				eventArgs.Completed -= previousHandler;
			}

			eventArgs.Completed += (sender, args) => {
				if (args.SocketError != SocketError.Success) {
					throw new SocketException((int)args.SocketError);
				}
			};
			eventArgs.DisconnectReuseSocket = false;
			socket.LingerState = new LingerOption(true, 1);

			try {
				socket.Shutdown(SocketShutdown.Both);
				socket.DisconnectAsync(eventArgs);
			} catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException) {
			}
		}

		/// <summary>
		/// Determines whether there is an error and whether it should be handled.
		/// Throws an exception for errors which are not expected to happen.
		/// </summary>
		/// <param name="eventArgs">The SocketEventArgs containing the information.</param>
		/// <param name="isRemoteShutdown">Whether the remote socket is being shut down or the local one.</param>
		/// <returns>Whether the error should be handled.</returns>
		public static bool ShouldHandleError(SocketAsyncEventArgs eventArgs, out bool isRemoteShutdown) {
			switch (eventArgs.SocketError) {
				case SocketError.Success:
					isRemoteShutdown = eventArgs.BytesTransferred == 0;
					return isRemoteShutdown;
				case SocketError.OperationAborted:
				case SocketError.Shutdown:
					isRemoteShutdown = false;
					return true;
				case SocketError.ConnectionReset:
				case SocketError.Disconnecting:
					isRemoteShutdown = true;
					return true;
				default:
					throw new SocketException((int)eventArgs.SocketError);
			}
		}



		/// <summary>
		/// Handles a possibly fragment TCP packet and tries to reassemble it.
		/// </summary>
		/// <param name="sender">The sender of the packet which should be passed to the AssembledPacketHandler.</param>
		/// <param name="buffer">The buffer containing the packet contents.</param>
		/// <param name="size">The size of the received data.</param>
		public void OnTcpReceived(Socket sender, byte[] buffer, int size) {
			int offset = 0;
			while (true) {
				if (size - offset == 0) {
					return;
				}

				if (_packetSize == 0) {
					if (_savedPacketSizeBytes + size - offset >= 2) {
						if (_savedPacketSizeBytes == 0) {
							_packetSize = BitConverter.ToUInt16(buffer, offset);
							offset += 2;
						} else {
							_packetSizeBytes[1] = buffer[offset++];
							_packetSize = BitConverter.ToUInt16(_packetSizeBytes, 0);
							_savedPacketSizeBytes = 0;
						}

						if (size - offset >= _packetSize) { //buffer contains a whole packet, no need to copy bytes
							_assembledPacketHandler(sender, buffer, offset, _packetSize);
							offset += _packetSize;
							_packetSize = 0;
							continue;
						}
					} else { //_savedPacketSizeBytes == 0 && size == 1
						_packetSizeBytes[0] = buffer[offset]; //technically should increase offset
						_savedPacketSizeBytes = 1;
						return;
					}
				}

				int copySize = Math.Min(size - offset, _packetSize);
				Buffer.BlockCopy(buffer, offset, _packetBuffer, _savedPayloadBytes, copySize);
				offset += copySize;
				_savedPayloadBytes += copySize;

				if (_savedPayloadBytes == _packetSize) {
					_assembledPacketHandler(sender, _packetBuffer, 0, _packetSize);
					_packetSize = 0;
					_savedPayloadBytes = 0;
					continue;
				}
				break;
			}
		}
	}
}
