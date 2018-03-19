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
using System.ComponentModel;
using NetworkCommsDotNet.Tools;
using System.Collections.ObjectModel;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;

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
		public static float BetweenTeamBufferMinutes = 1f;
		public static int GraphBarCount = 100;
		public static float MaxScorePerSecond = 30f;
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

		public bool CompareTo(ScoreSplitData other)
		{
			return other != null && DialValue == other.DialValue && TotalPoints == other.TotalPoints;
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

		public float CalcScoreWindow(float startTime, float endTime)
		{
			float score = 0f;
			DialInputData lastInput = new DialInputData();
			bool bRecordScore = false;

			foreach (DialInputData input in DialInputs)
			{
				if (input.TimeSeconds > endTime)
				{
					if (!bRecordScore)
					{
						score = (endTime - startTime) * lastInput.DialScore;
					}
					else
					{
						score += (endTime - lastInput.TimeSeconds) * lastInput.DialScore;
					}

					bRecordScore = false;

					break;
				}
				else if (!bRecordScore && input.TimeSeconds > startTime)
				{
					bRecordScore = true;

					score += (input.TimeSeconds - startTime) * lastInput.DialScore;
				}
				else if (bRecordScore)
				{
					score += (input.TimeSeconds - lastInput.TimeSeconds) * lastInput.DialScore;
				}

				lastInput = input;
			}

			// Get any remaining time after the final input
			if (bRecordScore)
			{
				score += (endTime - lastInput.TimeSeconds) * lastInput.DialScore;
			}

			return score;
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
	public class GraphBarData
	{
		[ProtoMember(1)]
		public int Rank = 0;
		[ProtoMember(2)]
		public float ScoreNormalized = 0;

		public GraphBarData()
		{
		}

		public GraphBarData(int rank, float scoreNormalized)
		{
			Rank = rank;
			ScoreNormalized = scoreNormalized;
		}
	}
	[ProtoContract]
	public class GraphBarList
	{
		[ProtoMember(1)]
		public List<GraphBarData> GraphData = new List<GraphBarData>();
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
		[ProtoMember(4)]
		public GraphBarList GraphData = new GraphBarList();
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

	public class UpNextData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		string playerNames = "";
		public string PlayerNames
		{
			get { return playerNames; }
			set
			{
				playerNames = value;

				NotifyPropertyChanged("PlayerNames");
			}
		}
		int onDeckNumber = 1;
		public int OnDeckNumber
		{
			get { return onDeckNumber; }
			set
			{
				onDeckNumber = value;

				NotifyPropertyChanged("OnDeckNumber");
				NotifyPropertyChanged("OnDeckNumberString");
			}
		}
		public string OnDeckNumberString
		{
			get { return OnDeckNumber.ToString() + "."; }
		}
		int etaMinutesToPlay = 0;
		public int EtaMinutesToPlay
		{
			get { return etaMinutesToPlay; }
			set
			{
				etaMinutesToPlay = value;

				NotifyPropertyChanged("EtaMinutesToPlay");
				NotifyPropertyChanged("EstimatedTimeToPlayString");
			}
		}
		public string EstimatedTimeToPlayString
		{
			get { return "ETA: " + EtaMinutesToPlay + " Minutes"; }
		}

		public UpNextData()
		{
		}

		public UpNextData(ScoreboardUpNextTeamData team)
		{
			PlayerNames = team.PlayerNames;
		}
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
		Timer DiscoverPeersTimer = new Timer();
		bool bIsConnected = false;
		public bool IsConnected
		{
			get { return bIsConnected; }
			set
			{
				bIsConnected = value;

				if (bIsConnected)
				{
					DiscoverPeersTimer.Stop();
				}
				else
				{
					DiscoverPeersTimer.Start();
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
		Dictionary<ShortGuid, Dictionary<ConnectionType, List<EndPoint>>> DiscoveredPeers = new Dictionary<ShortGuid, Dictionary<ConnectionType, List<EndPoint>>>();

		public ClientConnection(Action<ClientIdData> onClientIdChanged)
		{
			OnClientIdChanged = onClientIdChanged;
		}

		public void StartConnection(EClientType type)
		{
			DiscoverPeersTimer.AutoReset = true;
			DiscoverPeersTimer.Interval = 1000;
			DiscoverPeersTimer.Elapsed += DiscoverPeersTimer_Elapsed;
			DiscoverPeersTimer.Start();

			ClientId = new ClientIdData(Environment.MachineName, CommonDebug.GetOptionalNumberId(), type);

			PeerDiscovery.EnableDiscoverable(PeerDiscovery.DiscoveryMethod.UDPBroadcast);

			PeerDiscovery.OnPeerDiscovered += PeerDiscovery_OnPeerDiscovered;

			PeerDiscovery.DiscoverPeersAsync(PeerDiscovery.DiscoveryMethod.UDPBroadcast);

			NetworkComms.AppendGlobalConnectionEstablishHandler(conn => OnConnectionEstablished(conn));
			NetworkComms.AppendGlobalConnectionCloseHandler(conn => OnConnectionClosed(conn));
		}

		private void DiscoverPeersTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			PeerDiscovery.DiscoverPeersAsync(PeerDiscovery.DiscoveryMethod.UDPBroadcast);

		}

		private void PeerDiscovery_OnPeerDiscovered(ShortGuid peerIdentifier, Dictionary<ConnectionType, List<EndPoint>> discoveredListenerEndPoints)
		{
			if (!IsConnected)
			{
				foreach (KeyValuePair<ConnectionType, List<EndPoint>> connection in discoveredListenerEndPoints)
				{
					if (connection.Key == ConnectionType.TCP)
					{
						foreach (EndPoint ep in connection.Value)
						{
							if (IsConnected)
							{
								break;
							}

							IPEndPoint ipEndPoint = ep as IPEndPoint;

							if (ipEndPoint.Address.ToString().StartsWith("192"))
							{
								try
								{
									NetworkComms.SendObject("ClientConnect", ipEndPoint.Address.ToString(), ipEndPoint.Port, ClientId);
								}
								catch (CommsException ex)
								{
									System.Diagnostics.Debug.WriteLine(ex.ToString());
								}
							}
						}
					}
				}
			}
		}

		private void OnConnectionEstablished(Connection connection)
		{
			if (connection.ConnectionInfo.ConnectionType == ConnectionType.TCP)
			{
				IsConnected = true;

				IPEndPoint ipEndPoint = connection.ConnectionInfo.RemoteEndPoint as IPEndPoint;
				ServerIp = ipEndPoint.Address.ToString();
				ServerPort = ipEndPoint.Port;
			}
		}

		private void OnConnectionClosed(Connection connection)
		{
			if (connection.ConnectionInfo.ConnectionType == ConnectionType.TCP)
			{
				IsConnected = false;
			}
		}
	}

	public class TeamResultsData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		int rank = 0;
		public int Rank
		{
			get { return rank; }
			set
			{
				rank = value;
				NotifyPropertyChanged("Rank");
				NotifyPropertyChanged("RankString");
			}
		}
		public string RankString
		{
			get { return Rank.ToString() + "."; }
		}
		string playerNames = "";
		public string PlayerNames
		{
			get { return playerNames; }
			set
			{
				playerNames = value;

				NotifyPropertyChanged("PlayerNames");
			}
		}
		float totalPoints = 0f;
		public float TotalPoints
		{
			get { return totalPoints; }
			set
			{
				totalPoints = value;

				NotifyPropertyChanged("TotalPoints");
			}
		}
		public string TotalPointsString
		{
			get { return "Points: " + TotalPoints.ToString("0.0"); }
		}
		float deltaPoints = 0f;
		public float DeltaPoints
		{
			get { return deltaPoints; }
			set
			{
				deltaPoints = value;

				NotifyPropertyChanged("DeltaPoints");
			}
		}
		public string DeltaPointsString
		{
			get { return (DeltaPoints == 0f ? "" : "Delta: " + DeltaPoints.ToString("0.0")); }
		}
		public GraphBarList GraphData = new GraphBarList();
		WriteableBitmap graphBmp;
		public WriteableBitmap GraphBmp
		{
			get { return graphBmp; }
			set
			{
				graphBmp = value;
				NotifyPropertyChanged("GraphBmp");
			}
		}

		public TeamResultsData()
		{
		}

		public TeamResultsData(ScoreboardTeamResultData teamResult)
		{
			Rank = teamResult.Rank;
			PlayerNames = teamResult.PlayerNames;
			TotalPoints = teamResult.TotalPoints;

			GraphData = teamResult.GraphData;
		}

		public Color GetRankColor(int rank, bool bPrimary)
		{
			if (bPrimary)
			{
				switch (rank)
				{
					case 1:
						return Brushes.Gold.Color;
					case 2:
						return Brushes.DarkGray.Color;
					case 3:
						return Brushes.Peru.Color;
				}

				float t = 1f - (rank - 3f) / 7f;
				byte r = (byte)(191f - t * 191f);
				byte g = (byte)(218f - t * 78f);
				
				return Color.FromRgb(r, g, 255);
			}
			else
			{
				switch (rank)
				{
					case 1:
						return Brushes.LightGoldenrodYellow.Color;
					case 2:
						return Brushes.Gainsboro.Color;
					case 3:
						return Brushes.Wheat.Color;
				}
				
				return Color.FromRgb(209, 234, 255);
			}
		}

		public void RenderGraph(int width, int height)
		{
			try
			{
				GraphBmp = new WriteableBitmap(width, height, 300, 300, PixelFormats.Pbgra32, null);

				GraphBmp.Lock();

				int barWidth = width / CommonValues.GraphBarCount;
				int startX = 0;
				int endX = Math.Max(0, barWidth - 1);
				foreach (GraphBarData barData in GraphData.GraphData)
				{
					Color primaryColor = GetRankColor(barData.Rank, true);
					Color secondaryColor = GetRankColor(barData.Rank, false);
					int barHeight = (int)Math.Round(height - height * barData.ScoreNormalized);

					unsafe
					{
						byte* pBackBuffer = (byte*)GraphBmp.BackBuffer;

						for (int y = 0; y < height; ++y)
						{
							for (int x = startX; x <= endX; ++x)
							{
								*(pBackBuffer + (y * GraphBmp.BackBufferStride + x * 4)) = y > barHeight ? primaryColor.B : secondaryColor.B;
								*(pBackBuffer + (y * GraphBmp.BackBufferStride + x * 4) + 1) = y > barHeight ? primaryColor.G : secondaryColor.G;
								*(pBackBuffer + (y * GraphBmp.BackBufferStride + x * 4) + 2) = y > barHeight ? primaryColor.R : secondaryColor.R;
								*(pBackBuffer + (y * GraphBmp.BackBufferStride + x * 4) + 3) = 255;
							}
						}
					}

					startX = endX + 1;
					endX = startX + barWidth - 1;
				}

				GraphBmp.AddDirtyRect(new Int32Rect(0, 0, width, height));

				GraphBmp.Unlock();
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.Message);
			}
		}
	}
}
