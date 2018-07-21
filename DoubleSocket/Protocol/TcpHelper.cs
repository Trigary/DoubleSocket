using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace DoubleSocket.Protocol {
	public class TcpHelper {
		public delegate void AssembledPacketHandler(Socket sender, byte[] buffer, int offset, int size);

		private readonly byte[] _packetSizeBytes = new byte[2];
		private readonly byte[] _packetBuffer;
		private readonly AssembledPacketHandler _assembledPacketHandler;
		private ushort _packetSize;
		private int _savedPacketSizeBytes;
		private int _savedPayloadBytes;

		public TcpHelper(int receiveBufferSize, AssembledPacketHandler assembledPacketHandler) {
			_packetBuffer = new byte[receiveBufferSize];
			_assembledPacketHandler = assembledPacketHandler;
		}



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
			
			socket.Shutdown(SocketShutdown.Both);
			socket.DisconnectAsync(eventArgs);
		}

		public static bool ShouldHandleError(SocketAsyncEventArgs eventArgs, out bool isRemoteShutdown) {
			switch (eventArgs.SocketError) {
				case SocketError.Success:
					isRemoteShutdown = eventArgs.BytesTransferred == 0;
					return isRemoteShutdown;
				case SocketError.OperationAborted:
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
					_assembledPacketHandler(sender, _packetBuffer, offset, _packetSize);
					_savedPayloadBytes = 0;

					if (offset < size) {
						continue;
					}
				}
				break;
			}
		}
	}
}
