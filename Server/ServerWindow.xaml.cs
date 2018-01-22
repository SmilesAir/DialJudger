using CommonClasses;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace Server
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public static MainWindow ServerWindow = null;
		static string ListeningUrl = "";
		static public ClientList Clients = new ClientList();
		SaveData SaveDataInst = new SaveData();
		public string SaveFilename = "DialJudgerServerSave.txt";
		float routineLengthMinutes = .1f;
		public float RoutineLengthMinutes
		{
			get { return routineLengthMinutes; }
			set
			{
				routineLengthMinutes = value;
				RoutineTimer.RoutineLengthMinutes = value;

				NotifyPropertyChanged("RoutineLengthMinutes");
			}
		}
		TeamData currentPlayingTeam = null;
		public TeamData CurrentPlayingTeam
		{
			get { return currentPlayingTeam; }
			set
			{
				currentPlayingTeam = value;

				NotifyPropertyChanged("NowPlayingString");
				NotifyPropertyChanged("StartButtonText");
			}
		}
		RoutineTimers RoutineTimer = new RoutineTimers(() => ServerWindow.OnFinishRoutineTimer(), () => ServerWindow.OnUpdateRoutineTimer());
		int CancelRoutineClickCount = 0;
		float CancelRoutineTimeLimit = .4f;
		DateTime CancelRoutineClickTime;
		float StartAfterCancelTimeLimit = .75f;

		public string NowPlayingString
		{
			get
			{
				if (CurrentPlayingTeam != null)
				{
					return CurrentPlayingTeam.PlayerNamesString;
				}
				else
				{
					return "Need To Set Playing Team";
				}
			}
		}
		public string TimeRemainingString
		{
			get { return RoutineTimer.RemainingTimeString; }
		}
		string routineLengthMinutesString = "";
		public string RoutineLengthString
		{
			get { return routineLengthMinutesString; }
			set
			{
				float newLength = 0;
				if (float.TryParse(value, out newLength) || value == "." || value == "")
				{
					routineLengthMinutesString = value;
					RoutineLengthMinutes = newLength;
				}
				else
				{
					routineLengthMinutesString = RoutineLengthMinutes.ToString();
				}

				NotifyPropertyChanged("RoutineLengthString");
			}
		}
		public bool IsJudging
		{
			get { return RoutineTimer.IsRoutinePlaying; }
		}
		public string StartButtonText
		{
			get
			{
				if (CurrentPlayingTeam == null)
				{
					return "Set Playing Team";
				}
				else if (IsJudging)
				{
					return "Triple Click To Stop Routine";
				}
				else
				{
					return "Click on First Throw";
				}
			}
		}
		string resultsText = "";
		public string ResultsText
		{
			get { return resultsText; }
			set
			{
				resultsText = value;

				NotifyPropertyChanged("ResultsText");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public MainWindow()
		{
			ServerWindow = this;

			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			AppendHandlers();

			JudgesControl.ItemsSource = Clients.Clients;

			TeamsControl.ItemsSource = SaveDataInst.TeamList;

			TopLevelGrid.DataContext = this;

			//TeamData td = new TeamData();
			//td.PlayerNames.Add("Ryan Young");
			//td.PlayerNames.Add("James Wiseman");
			//SaveDataInst.TeamList.Add(td);
			//td = new TeamData();
			//td.PlayerNames.Add("Jake Gauthier");
			//td.PlayerNames.Add("Arthur Coddington");
			//SaveDataInst.TeamList.Add(td);
			//SetPlayingTeam(SaveDataInst.TeamList[0]);

			Connection.StartListening(ConnectionType.UDP, new IPEndPoint(IPAddress.Any, 10000));

			//Start listening for incoming connections
			Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));

			//Print out the IPs and ports we are now listening on
			Console.WriteLine("Server listening for TCP connection on:");
			foreach (System.Net.IPEndPoint localEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
			{
				ListeningUrl = localEndPoint.Address + ":" + localEndPoint.Port;
			}

			Load();

			routineLengthMinutesString = RoutineLengthMinutes.ToString();
			RoutineLengthMinutes = routineLengthMinutes;
		}

		private void AppendHandlers()
		{
			NetworkComms.AppendGlobalConnectionEstablishHandler(OnConnectionEstablished);
			NetworkComms.AppendGlobalConnectionCloseHandler(OnConnectionClosed);

			NetworkComms.AppendGlobalIncomingPacketHandler<int>("BroadcastFindServer", HandleBroadcastFindServer);
			NetworkComms.AppendGlobalIncomingPacketHandler<ClientIdData>("ClientConnect", HandleClientConnect);
			NetworkComms.AppendGlobalIncomingPacketHandler<ScoreUpdateData>("JudgeScoreUpdate", HandleJudgeScoreUpdate);
			NetworkComms.AppendGlobalIncomingPacketHandler<DialRoutineScoreData>("JudgeFinishedScore", HandleJudgeFinishedScore);
			NetworkComms.AppendGlobalIncomingPacketHandler<DialRoutineScoreData>("JudgeSendBackupScore", HandleJudgeSendBackupScore);
		}

		private static void HandleBroadcastFindServer(PacketHeader header, Connection connection, int port)
		{
			UDPConnection.SendObject("BroadcastServerInfo", ListeningUrl, new IPEndPoint(IPAddress.Broadcast, port));
		}

		private static void HandleClientConnect(PacketHeader header, Connection connection, ClientIdData clientInfo)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.Add(connection, clientInfo, () => ServerWindow.UpdateJudgesForClients());

				if (clientInfo.ClientType == EClientType.Scoreboard)
				{
					ServerWindow.SendUpdatesToScoreboard();
				}
			}));
		}

		private static void HandleJudgeScoreUpdate(PacketHeader header, Connection connection, ScoreUpdateData scoreUpdate)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.SetLastJudgeScore(scoreUpdate);
			}));
		}

		private static void HandleJudgeFinishedScore(PacketHeader header, Connection connection, DialRoutineScoreData score)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ServerWindow.SetTeamScore(ServerWindow.CurrentPlayingTeam, score);

				ServerWindow.TryIncrementPlayingTeam();
			}));
		}

		private static void HandleJudgeSendBackupScore(PacketHeader header, Connection connection, DialRoutineScoreData score)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ServerWindow.SetTeamScore(score);
			}));
		}

		public void SetTeamScore(DialRoutineScoreData score)
		{
			foreach (TeamData team in SaveDataInst.TeamList)
			{
				if (team.PlayerNamesString == score.PlayerNames)
				{
					SetTeamScore(team, score);
				}
			}
		}

		public void SetTeamScore(TeamData team, DialRoutineScoreData score)
		{
			if (team != null)
			{
				team.SetFinishedScore(ServerWindow.SaveDataInst.TeamList, score);

				UpdateResultsText();

				SendUpdatesToScoreboard();

				Save();
			}
		}

		void SendResultsToScoreboard()
		{
			List<TeamData> sortedTeams = CalcSortedTeams();
			ScoreboardResultsData results = new ScoreboardResultsData();

			foreach (TeamData team in sortedTeams)
			{
				ScoreboardTeamResultData teamResult = new ScoreboardTeamResultData();

				teamResult.PlayerNames = team.PlayerNamesString;
				teamResult.Rank = team.Rank;
				teamResult.TotalPoints = team.TotalScore;

				results.Results.Add(teamResult);
			}

			foreach (ClientData cd in Clients.Clients)
			{
				if (cd.ClientId.ClientType == EClientType.Scoreboard)
				{
					cd.ConnectionData.SendObject("ServerSendResults", results);
				}
			}
		}

		void SendUpNextTeamsToScoreboard()
		{
			List<TeamData> upNextTeams = new List<TeamData>();
			foreach (TeamData team in SaveDataInst.TeamList)
			{
				if (team.Rank == 0)
				{
					upNextTeams.Add(team);
				}
			}

			ScoreboardUpNextData upNextData = new ScoreboardUpNextData();
			foreach (TeamData team in upNextTeams)
			{
				upNextData.UpNextTeams.Add(new ScoreboardUpNextTeamData(team.PlayerNamesString));
			}

			foreach (ClientData cd in Clients.Clients)
			{
				if (cd.ClientId.ClientType == EClientType.Scoreboard)
				{
					cd.ConnectionData.SendObject("ServerSendUpNextTeams", upNextData);
				}
			}
		}

		void SendUpdatesToScoreboard()
		{
			SendResultsToScoreboard();
			SendUpNextTeamsToScoreboard();
		}

		private static void OnConnectionEstablished(Connection connection)
		{
		}

		private static void OnConnectionClosed(Connection connection)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.Remove(connection);
			}));
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			NetworkComms.Shutdown();
		}

		private void SetPlayingTeam_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			TeamData td = button.Tag as TeamData;

			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				SetPlayingTeam(td);
			}));
		}

		private void SetPlayingTeam(TeamData playingTeam)
		{
			CurrentPlayingTeam = playingTeam;

			foreach (TeamData td in SaveDataInst.TeamList)
			{
				td.IsPlaying = td == playingTeam;
			}

			InitRoutineData routineData = new InitRoutineData(playingTeam == null ? "No Team" : playingTeam.PlayerNamesString, RoutineLengthMinutes);
			foreach (Connection connection in NetworkComms.GetExistingConnection())
			{
				connection.SendObject("ServerSetPlayingTeam", routineData);
			}
		}

		private void MenuItemExit_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			double secondsSinceCancelClick = (DateTime.Now - CancelRoutineClickTime).TotalSeconds;

			if (!IsJudging)
			{
				if (secondsSinceCancelClick > StartAfterCancelTimeLimit)
				{
					StartRoutine();
				}
			}
			else
			{
				if (secondsSinceCancelClick > CancelRoutineTimeLimit)
				{
					CancelRoutineClickCount = 0;
				}

				++CancelRoutineClickCount;
				CancelRoutineClickTime = DateTime.Now;

				if (CancelRoutineClickCount >= 3)
				{
					CancelRoutine();
				}
			}
		}

		void StartRoutine()
		{
			if (CurrentPlayingTeam != null)
			{
				RoutineTimer.StartRoutine(RoutineLengthMinutes);

				NotifyPropertyChanged("StartButtonText");

				InitRoutineData routineData = new InitRoutineData(CurrentPlayingTeam.PlayerNamesString, RoutineLengthMinutes);

				foreach (Connection connection in NetworkComms.GetExistingConnection())
				{
					connection.SendObject("ServerStartRoutine", routineData);
				}
			}
		}

		void CancelRoutine()
		{
			RoutineTimer.StopRoutine();

			NotifyPropertyChanged("StartButtonText");

			foreach (Connection connection in NetworkComms.GetExistingConnection())
			{
				connection.SendObject("ServerCancelRoutine", "");
			}
		}

		void UpdateJudgesForClients()
		{
			// Parse the Judges text

			//List<JudgeData> newJudges = new List<JudgeData>();
			//newJudges.Add(new JudgeData("Randy Silvey", ECategory.General));
			//newJudges.Add(new JudgeData("Bob Boulware", ECategory.General));

			for (int judgeIndex = 0, clientIndex = 0; judgeIndex < SaveDataInst.ImportedJudges.Count && clientIndex < Clients.Clients.Count; ++clientIndex)
			{
				JudgeData jd = SaveDataInst.ImportedJudges[judgeIndex];
				ClientData cd = Clients.Clients[clientIndex];

				if (cd.ClientId.ClientType == EClientType.Judge)
				{
					cd.Judge = jd;

					cd.ConnectionData.SendObject("ServerSetJudgeInfo", jd);

					++judgeIndex;
				}
			}
		}

		List<TeamData> CalcSortedTeams()
		{
			List<TeamData> sortedTeams = new List<TeamData>();
			foreach (TeamData team in SaveDataInst.TeamList)
			{
				if (team.Rank > 0)
				{
					if (sortedTeams.Count == 0)
					{
						sortedTeams.Add(team);
					}
					else
					{
						int insertIndex = 0;
						for (; insertIndex < sortedTeams.Count; ++insertIndex)
						{
							if (sortedTeams[insertIndex].Rank > 0 && team.Rank < sortedTeams[insertIndex].Rank)
							{
								break;
							}
						}

						sortedTeams.Insert(insertIndex, team);
					}
				}
			}

			return sortedTeams;
		}

		void UpdateResultsText()
		{
			List<TeamData> sortedTeams = CalcSortedTeams();

			ResultsText = "";

			foreach (TeamData team in sortedTeams)
			{
				ResultsText += team.PlayerNamesString + ", Rank: " + team.RankString + ", Points: " + team.TotalScoreString +
					", Details: " + team.DetailedJudgeScoresString + "\r\n";
			}
		}

		public void TryIncrementPlayingTeam()
		{
			var teamList = SaveDataInst.TeamList;
			for (int i = 0; i < teamList.Count; ++i)
			{
				TeamData team = teamList[i];
				if (CurrentPlayingTeam == team)
				{
					if (i < teamList.Count - 1)
					{
						SetPlayingTeam(teamList[i + 1]);
					}
					else
					{
						SetPlayingTeam(null);
					}

					return;
				}
			}
		}

		void ImportTeamsFromTextbox()
		{
			// Check if we are overriding data

			string[] splitters = { ",", "-", "/", "|" };
			StringReader text = new StringReader(TeamsTextBox.Text);

			SaveDataInst.TeamList.Clear();

			string line = null;
			while ((line = text.ReadLine()) != null)
			{
				TeamData newTeam = new TeamData();

				string[] names = line.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
				foreach (string name in names)
				{
					newTeam.PlayerNames.Add(name.Trim());
				}

				SaveDataInst.TeamList.Add(newTeam);
			}

			Save();
			SendUpdatesToScoreboard();
			UpdateResultsText();
		}

		private void ImportTeamsButton_Click(object sender, RoutedEventArgs e)
		{
			ImportTeamsFromTextbox();
		}

		private void ImportJudgesButton_Click(object sender, RoutedEventArgs e)
		{
			ImportJudgesFromTextbox();
		}

		void ImportJudgesFromTextbox()
		{
			// Check if we are overriding data

			string[] splitters = { ",", "-", "/", "|" };
			StringReader text = new StringReader(JudgesTextBox.Text);

			string line = null;
			while ((line = text.ReadLine()) != null)
			{
				string[] judgeParams = line.Split(splitters, StringSplitOptions.RemoveEmptyEntries);

				bool bImportError = false;
				if (judgeParams.Length == 2)
				{
					ECategory judgeCategory;
					if (Enum.TryParse<ECategory>(judgeParams[1].Trim(), out judgeCategory))
					{
						SaveDataInst.ImportedJudges.Add(new JudgeData(judgeParams[0].Trim(), judgeCategory));
					}
					else
					{
						// Parse Error
						bImportError = true;
					}
				}
				else
				{
					// Parse Error
					bImportError = true;
				}

				if (!bImportError)
				{
					UpdateJudgesForClients();

					Save();
				}
			}
		}

		void Save()
		{
			try
			{
				XmlSerializer serializer = new XmlSerializer(typeof(SaveData));
				using (StringWriter retString = new StringWriter())
				{
					serializer.Serialize(retString, SaveDataInst);
					using (StreamWriter saveFile = new StreamWriter(SaveFilename))
					{
						saveFile.Write(retString.ToString());
					}
				}
			}
			catch
			{
				// Popup error
			}
		}

		void Load()
		{
			try
			{
				if (File.Exists(SaveFilename))
				{
					using (StreamReader saveFile = new StreamReader(SaveFilename))
					{
						XmlSerializer serializer = new XmlSerializer(typeof(SaveData));
						SaveDataInst = (SaveData)serializer.Deserialize(saveFile);

						TeamsControl.ItemsSource = SaveDataInst.TeamList;

						foreach (TeamData td in SaveDataInst.TeamList)
						{
							td.IsPlaying = false;
						}

						UpdateJudgesForClients();
						UpdateResultsText();
					}
				}
			}
			catch
			{
				// Popup error
			}
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			Save();
		}

		public void OnFinishRoutineTimer()
		{
			NotifyPropertyChanged("TimeRemainingString");
		}

		public void OnUpdateRoutineTimer()
		{
			NotifyPropertyChanged("TimeRemainingString");
		}
	}

	public class ClientData : INotifyPropertyChanged
	{
		public Connection ConnectionData = null;
		public ClientIdData ClientId = null;
		public RoutineData RoutineScores = new RoutineData();
		JudgeData judge = null;
		public JudgeData Judge
		{
			get { return judge; }
			set
			{
				judge = value;
				NotifyPropertyChanged("Judge");
				NotifyPropertyChanged("JudgeName");
				NotifyPropertyChanged("JudgeCategory");
			}
		}
		private float lastJudgeValue = CommonValues.InvalidScore;
		public float LastJudgeValue
		{
			get { return lastJudgeValue; }
			set
			{
				lastJudgeValue = value;

				NotifyPropertyChanged("LastJudgeValue");
				NotifyPropertyChanged("JudgeValue");
			}
		}
		public string ClientTypeString
		{
			get { return ClientId.ClientType.ToString(); }
		}

		// Display
		public string ClientName { get { return ClientId == null ? "None" : ClientId.DisplayName; } }
		public string JudgeValue { get { return LastJudgeValue.ToString("0.0"); } }
		public string JudgeName { get { return Judge == null ? "None" : Judge.JudgeName; } }
		public string JudgeCategory { get { return Judge == null ? "None" : Judge.Category.ToString(); } }

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public ClientData()
		{
		}

		public ClientData(Connection connection, ClientIdData id)
		{
			ConnectionData = connection;
			ClientId = id;
		}
	}

	public class ClientList
	{
		public ObservableCollection<ClientData> Clients = new ObservableCollection<ClientData>();

		public ClientList()
		{
		}

		public void Add(Connection connection, ClientIdData id, Action onComplete)
		{
			int insertIndex = 0;
			foreach (ClientData cd in Clients)
			{
				if (cd.ClientId.CompareTo(id))
				{
					return;
				}

				if (id.OptionalNumberId >= cd.ClientId.OptionalNumberId)
				{
					++insertIndex;
				}
			}

			Clients.Insert(insertIndex, new ClientData(connection, id));

			onComplete();
		}

		public void Remove(Connection connection)
		{
			for (int i = 0; i < Clients.Count; ++i)
			{
				if (Clients[i].ConnectionData == connection)
				{
					Clients.RemoveAt(i);
					return;
				}
			}
		}

		public void SetJudgeId(Connection connection, ClientIdData id)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.ConnectionData == connection)
				{
					cd.ClientId = id;
					return;
				}
			}
		}

		public void SetLastJudgeScore(ScoreUpdateData scoreUpdate)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.ClientId.CompareTo(scoreUpdate.Judge))
				{
					cd.LastJudgeValue = scoreUpdate.Score.Score;

					return;
				}
			}
		}
	}

	public class TeamData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public ObservableCollection<string> PlayerNames = new ObservableCollection<string>();
		public string PlayerNamesString
		{
			get
			{
				string ret = "";

				if (PlayerNames.Count == 0)
				{
					ret = "No players";
				}
				else
				{
					for (int i = 0; i < PlayerNames.Count; ++i)
					{
						ret += PlayerNames[i];

						if (i != PlayerNames.Count - 1)
						{
							ret += ", ";
						}
					}
				}

				return ret;
			}
		}
		public List<DialRoutineScoreData> Scores = new List<DialRoutineScoreData>();
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
			get { return Rank != 0 ? Rank.ToString() : "N/A"; }
		}
		bool isPlaying = false;
		public bool IsPlaying
		{
			get { return isPlaying; }
			set
			{
				isPlaying = value;
				NotifyPropertyChanged("IsPlaying");
				NotifyPropertyChanged("DisplayBackgroundColor");
			}
		}
		public Brush DisplayBackgroundColor
		{
			get { return IsPlaying ? Brushes.LightGreen : Brushes.White; }
		}
		public float TotalScore
		{
			get
			{
				float total = 0f;
				foreach (DialRoutineScoreData score in Scores)
				{
					total += score.GetTotalScore();
				}

				return total;
			}
		}
		public string TotalScoreString { get { return TotalScore.ToString("0.0"); } }
		public string DetailedJudgeScoresString
		{
			get
			{
				string ret = "";

				foreach (DialRoutineScoreData score in Scores)
				{
					ret += score.JudgeName + ": " + score.GetScoreString() + "  ";
				}

				return ret;
			}
		}

		void UpdateRank(ObservableCollection<TeamData> teamList)
		{
			foreach (TeamData team1 in teamList)
			{
				float score1Total = team1.TotalScore;
				int rank = 1;
				if (score1Total > 0)
				{
					foreach (TeamData team2 in teamList)
					{
						if (score1Total < team2.TotalScore)
						{
							++rank;
						}
					}

					team1.Rank = rank;
				}
				else
				{
					team1.Rank = 0;
				}
			}
		}

		void UpdateAfterScoreUpdate(ObservableCollection<TeamData> teamList)
		{
			UpdateRank(teamList);

			NotifyPropertyChanged("TotalScoreString");
			NotifyPropertyChanged("DetailedJudgeScoresString");
		}

		public void SetFinishedScore(ObservableCollection<TeamData> teamList, DialRoutineScoreData newScore)
		{
			for (int i = 0; i < Scores.Count; ++i)
			{
				DialRoutineScoreData score = Scores[i];

				if (score.IsSameInstance(newScore))
				{
					Scores[i] = newScore;

					UpdateAfterScoreUpdate(teamList);

					return;
				}
			}

			Scores.Add(newScore);

			UpdateAfterScoreUpdate(teamList);
		}
	}

	public class SaveData
	{
		public ObservableCollection<TeamData> TeamList = new ObservableCollection<TeamData>();
		public List<JudgeData> ImportedJudges = new List<JudgeData>();
		public float RoutineLengthMinutes = 3f;
	}
}
