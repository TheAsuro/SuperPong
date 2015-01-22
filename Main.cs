using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using Alchemy;
using Alchemy.Classes;

namespace PongServer
{
	class MainClass
	{
		private static List<UserContext> allSockets;
		private static List<GameRoom> roomList;

		public static void Main (string[] args)
		{
			//Create lists
			allSockets = new List<UserContext>();
			roomList = new List<GameRoom>();

			//Load ip address and port
			var address = LocalIPAddress();
			var port = 42096;

			//Use first argument as port if possible
			if(args.Length >= 1)
			{
				int newPort;
				if(int.TryParse(args[0], out newPort))
				{
					port = newPort;
				}
			}

			//Prepare server
			var server = new WebSocketServer(false, port, address);

			server.Start();
			server.OnConnected += OnOpen;
			server.OnReceive += OnMessage;
			server.OnDisconnect += OnClose;
			Console.WriteLine("Started server at " + address.ToString());

			//Start reading console
			while(true)
			{
				string message = Console.ReadLine();
				
				if(message.ToLower().Equals("exit"))
					break;
				
				foreach(UserContext socket in allSockets)
				{
					socket.Send(message);
				}
			}

			server.Stop();
		}
		
		private static void OnOpen(UserContext connection)
		{
			allSockets.Add(connection);
			Console.WriteLine("Connection opened. ID: " + allSockets.IndexOf(connection));
			FindRoomForPlayer(connection);
		}
		
		private static void OnClose(UserContext connection)
		{
			string oldId = allSockets.IndexOf(connection).ToString();
			allSockets.Remove(connection);
			Console.WriteLine("Connection closed. ID: " + oldId);

			//Look through the rooms to find the disconnected player
			GameRoom room = FindRoomWithPlayer(connection);

			if(room.isFull())
			{
				//Room has one player left, set back to waiting state
				room.RemovePlayerFromRoom(connection);
				Console.WriteLine("Reset room #" + roomList.IndexOf(room) + ".");
			}
			else
			{
				//Last player disconnected, remove room
				string roomId = roomList.IndexOf(room).ToString();
				roomList.Remove(room);
				Console.WriteLine("Removed room #" + roomId + ".");
				return;
			}
		}
		
		private static void OnMessage(UserContext connection)
		{
			InterpretMessage(connection, connection.DataFrame.ToString());
		}

		//Finds an empty room for a new player
		private static void FindRoomForPlayer(UserContext socket)
		{
			//Look if we have a room with only one player
			foreach(GameRoom room in roomList)
			{
				if(!room.isFull())
				{
					room.AddSecondPlayer(socket);
					return;
				}
			}
			
			//No free rooms, create one
			GameRoom newRoom = new GameRoom(socket);
			roomList.Add(newRoom);
		}

		//Finds the room the specified player is in
		private static GameRoom FindRoomWithPlayer(UserContext user)
		{
			foreach(GameRoom room in roomList)
			{
				if(room.GetPlayers().Contains(user))
				{
					return room;
				}
			}

			throw new InvalidOperationException("Searched for a user that was not in any room");
		}

		private static void InterpretMessage(UserContext user, string message)
		{
			if(message.StartsWith("pos"))
			{
				int pos;
				if(int.TryParse(message.Substring(3), out pos))
				{
					var room = FindRoomWithPlayer(user);
					room.SetCursorPosition(user, pos);
				}
				else
				{
					throw new Exception("Could not parse position info: " + message);
				}				
			}
			else
			{
				Console.WriteLine("Unknown message: " + message);
			}
		}

		//Get an ip adress to host the server on
		private static IPAddress LocalIPAddress()
		{
			IPHostEntry host;
			host = Dns.GetHostEntry(Dns.GetHostName());
			foreach(IPAddress ip in host.AddressList)
			{
				if(ip.AddressFamily == AddressFamily.InterNetwork)
				{
					return ip;
				}
			}
			throw new Exception("Found no IP adress!");
		}
	}
}