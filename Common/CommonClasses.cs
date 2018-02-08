using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using NetworkCommsDotNet.Connections;
using System.Net;
using System.Timers;
using NetworkCommsDotNet.Connections.UDP;
using NetworkCommsDotNet;

namespace CommonClasses
{
	public class CommonDebug
	{
#if DEBUG
		public static bool bEnableDebug = true;
#else
		public static bool bEnableDebug = true;
#endif

		public static bool bGenerateUniqueNumberId = bEnableDebug;
		public static bool bOnlyDialInputWhenFocused = bEnableDebug;

		public static int GetOptionalNumberId()
		{
			if (bGenerateUniqueNumberId)
			{
				return DateTime.Now.Millisecond;
			}
			else
			{
				return CommonValues.InvalidNumberId;
			}
		}
	}

	public class CommonValues
	{
		public static float InvalidScore = -1f;
		public static int InvalidNumberId = -1;
	}

	public enum EClientType
	{
		Judge,
		Scoreboard,
		Overlay
	}

	[ProtoContract]
	public class ClientIdData
	{
		[ProtoMember(1)]
		public string Name = "";
		[ProtoMember(2)]
		public int OptionalNumberId = CommonValues.InvalidNumberId;
		[ProtoMember(3)]
		public EClientType ClientType = EClientType.Judge;


		public string DisplayName { get { return Name + (OptionalNumberId == CommonValues.InvalidNumberId ? "" : " (" + OptionalNumberId + ")"); } }

		public ClientIdData()
		{
		}

		public ClientIdData(string name, EClientType type)
		{
			Name = name;
			ClientType = type;
		}

		public ClientIdData(string name, int numberId, EClientType type)
		{
			Name = name;
			OptionalNumberId = numberId;
			ClientType = type;
		}

		public bool CompareTo(ClientIdData id)
		{
			return Name == id.Name && OptionalNumberId == id.OptionalNumberId;
		}
	}


	[ProtoContract]
	public class ScoreSplitData
	{
		[ProtoMember(1)]
		public ClientIdData Judge = null;
		[ProtoMember(2)]
		public float DialValue = 0f;
		[ProtoMember(3)]
		public float TotalPoints = 0f;

		public ScoreSplitData()
		{
		}

		public ScoreSplitData(ClientIdData id, float score, float totalScore)
		{
			Judge = id;
			DialValue = score;
			TotalPoints = totalScore;
		}
	}

	public enum EDialInput
	{
		Up,
		Down,
		Click
	}

	[ProtoContract]
	public class ClientTeamInfoData
	{
		[ProtoMember(1)]
		public string JudgeName = "";

		[ProtoMember(2)]
		public string TeamName = "";

		[ProtoMember(3)]
		public float RoutineLengthSeconds = 0;

		public ClientTeamInfoData()
		{
		}
	}

	// Use as base class for future scoring systems
	[ProtoContract]
	[ProtoInclude(500, typeof(DialRoutineScoreData))]
	public abstract class BaseRoutineScoreData
	{
		[ProtoMember(1)]
		public string JudgeName = "";
		[ProtoMember(2)]
		public string PlayerNames = "";

		public abstract float GetTotalScore();

		public abstract string GetScoreString();

		public abstract bool IsSameInstance<T>(T other) where T : BaseRoutineScoreData;
	}

	public enum ECategory
	{
		General,
		ArtisticImpression,
		Difficulty,
		Execution
	}

	[ProtoContract]
	public class JudgeData
	{
		[ProtoMember(1)]
		public string JudgeName = "No Judge Set";
		[ProtoMember(2)]
		public ECategory Category = ECategory.General;

		public JudgeData()
		{
		}

		public JudgeData(string judgeName, ECategory categoryName)
		{
			JudgeName = judgeName;
			Category = categoryName;
		}
	}

	[ProtoContract]
	public class InitRoutineData
	{
		[ProtoMember(1)]
		public string PlayersNames = "";

		[ProtoMember(2)]
		public float RoutineLengthMinutes = 0f;

		public InitRoutineData()
		{
		}

		public InitRoutineData(string playerNames, float routineLengthMinutes)
		{
			PlayersNames = playerNames;
			RoutineLengthMinutes = routineLengthMinutes;
		}
	}

	[ProtoContract]
	public class DialInputData
	{
		[ProtoMember(1)]
		public float DialScore = 0f;
		[ProtoMember(2)]
		public float TimeSeconds = 0f;

		public DialInputData()
		{
		}

		public DialInputData(float dialScore, float timeSeconds)
		{
			DialScore = dialScore;
			TimeSeconds = timeSeconds;
		}
	}

	[ProtoContract]
	public class DialRoutineScoreData : BaseRoutineScoreData
	{
		[ProtoMember(1)]
		public List<DialInputData> DialInputs = new List<DialInputData>();

		public override float GetTotalScore()
		{
			return GetTotalScore(0f);
		}

		public float GetTotalScore(float routineLengthSeconds)
		{
			float total = 0f;

			for (int i = 0; i < DialInputs.Count - 1; ++i)
			{
				DialInputData first = DialInputs[i];
				DialInputData second = DialInputs[i + 1];

				float timeDelta = second.TimeSeconds - first.TimeSeconds;

				total += timeDelta * first.DialScore;
			}

			if (routineLengthSeconds > 0f && DialInputs.Count > 0)
			{
				DialInputData last = DialInputs.Last();

				total += (routineLengthSeconds - last.TimeSeconds) * last.DialScore;
			}

			return total;
		}

		public override string GetScoreString()
		{
			return GetTotalScore().ToString("0.0");
		}

		public override bool IsSameInstance<T>(T other)
		{
			return other.JudgeName == JudgeName && other.PlayerNames == PlayerNames;
		}
	}

	[ProtoContract]
	public class ScoreboardTeamResultData
	{
		[ProtoMember(1)]
		public int Rank = 0;
		[ProtoMember(2)]
		public string PlayerNames = "";
		[ProtoMember(3)]
		public float TotalPoints = 0f;
	}

	[ProtoContract]
	public class ScoreboardResultsData
	{
		[ProtoMember(1)]
		public List<ScoreboardTeamResultData> Results = new List<ScoreboardTeamResultData>();
	}

	[ProtoContract]
	public class ScoreboardUpNextTeamData
	{
		[ProtoMember(1)]
		public string PlayerNames = "";

		public ScoreboardUpNextTeamData()
		{
		}

		public ScoreboardUpNextTeamData(string playerNames)
		{
			PlayerNames = playerNames;
		}
	}

	[ProtoContract]
	public class ScoreboardUpNextData
	{
		[ProtoMember(1)]
		public List<ScoreboardUpNextTeamData> UpNextTeams = new List<ScoreboardUpNextTeamData>();
	}
	
	public class RoutineTimers
	{
		System.Timers.Timer FinishRoutineTimer = new System.Timers.Timer();
		System.Timers.Timer UpdateRoutineTimeTimer = new System.Timers.Timer();
		Action FinishCallback;
		Action UpdateCallback;
		DateTime StartRoutineTime;
		public float RoutineLengthMinutes = 0f;

		public bool IsRoutinePlaying
		{
			get { return FinishRoutineTimer.Enabled; }
		}
		public double ElapsedSeconds
		{
			get { return (DateTime.Now - StartRoutineTime).TotalSeconds; }
		}
		public double RemainingSeconds
		{
			get
			{
				if (IsRoutinePlaying)
				{
					return RoutineLengthMinutes * 60f - ElapsedSeconds;
				}

				return 0f;
			}
		}
		public string RemainingTimeString
		{
			get
			{
				double remainingSeconds = IsRoutinePlaying ? RemainingSeconds : RoutineLengthMinutes * 60f;
				return string.Format("{0:0}:{1:00}", (int)((remainingSeconds + .1f) / 60), ((int)Math.Round(remainingSeconds)) % 60);
			}
		}

		public RoutineTimers(Action finishCallback, Action updateCallback)
		{
			FinishCallback = finishCallback;
			UpdateCallback = updateCallback;

			FinishRoutineTimer.AutoReset = false;
			FinishRoutineTimer.Elapsed += FinishRoutineTimer_Elapsed;

			UpdateRoutineTimeTimer.Interval = 1000;
			UpdateRoutineTimeTimer.AutoReset = true;
			UpdateRoutineTimeTimer.Elapsed += UpdateRoutineTimeTimer_Elapsed;
		}

		private void UpdateRoutineTimeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			UpdateCallback();
		}

		private void FinishRoutineTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			UpdateRoutineTimeTimer.Stop();

			UpdateCallback();
			FinishCallback();
		}

		public void StartRoutine(float routineLengthMinutes)
		{
			RoutineLengthMinutes = routineLengthMinutes;

			FinishRoutineTimer.Interval = routineLengthMinutes * 60f * 1000f;
			FinishRoutineTimer.Start();

			UpdateRoutineTimeTimer.Start();

			StartRoutineTime = DateTime.Now;
		}

		public void StopRoutine()
		{
			FinishRoutineTimer.Stop();
			UpdateRoutineTimeTimer.Stop();

			UpdateCallback();
		}
	}

	public class ClientConnection
	{
		System.Timers.Timer BroadcastTimer = new System.Timers.Timer();

		bool bIsConnected = false;
		public bool IsConnected
		{
			get { return bIsConnected; }
			set
			{
				bIsConnected = value;

				if (bIsConnected)
				{
					BroadcastTimer.Stop();
				}
				else
				{
					BroadcastTimer.Start();
				}
			}
		}
		public string ServerIp = "";
		public int ServerPort = 0;
		private ClientIdData clientId = null;
		public ClientIdData ClientId
		{
			get { return clientId; }
			set
			{
				clientId = value;

				OnClientIdChanged(value);
			}
		}
		Action<ClientIdData> OnClientIdChanged;

		public ClientConnection(Action<ClientIdData> onClientIdChanged)
		{
			OnClientIdChanged = onClientIdChanged;
		}

		public void StartConnection(EClientType type)
		{
			ClientId = new ClientIdData(Environment.MachineName, CommonDebug.GetOptionalNumberId(), type);

			BroadcastTimer.Interval = 1000;
			BroadcastTimer.AutoReset = true;
			BroadcastTimer.Elapsed += BroadcastTimer_Elapsed;
			BroadcastTimer.Start();

			NetworkComms.AppendGlobalConnectionEstablishHandler(conn => OnConnectionEstablished(conn));
			NetworkComms.AppendGlobalConnectionCloseHandler(conn => OnConnectionClosed(conn));
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("BroadcastServerInfo",
				(header, connection, serverInfo) => HandleBroadcastServerInfo(header, connection, serverInfo));

			Connection.StartListening(ConnectionType.UDP, new IPEndPoint(IPAddress.Any, 0), true);
		}

		private void BroadcastTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			BroadcastFindServer();
		}

		private void BroadcastFindServer()
		{
			IPEndPoint ipEndPoint = Connection.ExistingLocalListenEndPoints(ConnectionType.UDP)[0] as IPEndPoint;
			UDPConnection.SendObject("BroadcastFindServer", ipEndPoint.Port, new IPEndPoint(IPAddress.Broadcast, 10000));
		}

		private void OnConnectionEstablished(Connection connection)
		{
			IsConnected = true;

			Connection.StopListening();
		}

		private void HandleBroadcastServerInfo(PacketHeader header, Connection connection, string serverInfo)
		{
			ServerIp = serverInfo.Split(':').First();
			ServerPort = int.Parse(serverInfo.Split(':').Last());

			NetworkComms.SendObject("ClientConnect", ServerIp, ServerPort, ClientId);
		}

		private void OnConnectionClosed(Connection connection)
		{
			IsConnected = false;
		}
	}
}
