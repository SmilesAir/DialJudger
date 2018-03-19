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
using System.Speech.Synthesis;
using System.Windows.Media.Animation;

namespace Server
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public static MainWindow ServerWindow = null;
		System.Timers.Timer SendSplitTimer = new System.Timers.Timer();
		string listeningUrl = "";
		public string ListeningUrl
		{
			get { return listeningUrl; }
			set
			{
				listeningUrl = value;
				NotifyPropertyChanged("ListeningUrl");
				NotifyPropertyChanged("WindowTitle");
			}
		}
		static public ClientList Clients = new ClientList();
		SaveData SaveDataInst = new SaveData();
		string saveFilename = "DialJudgerServerSave.txt";
		public string SaveFilename
		{
			get { return saveFilename; }
			set
			{
				saveFilename = value;
				NotifyPropertyChanged("SaveFilename");
				NotifyPropertyChanged("WindowTitle");
			}
		}
		float routineLengthMinutes = .1f;
		public float RoutineLengthMinutes
		{
			get { return routineLengthMinutes; }
			set
			{
				routineLengthMinutes = value;
				RoutineTimer.RoutineLengthMinutes = value;
				SaveDataInst.RoutineLengthMinutes = value;

				NotifyPropertyChanged("RoutineLengthMinutes");
				NotifyPropertyChanged("TimeRemainingString");
			}
		}
		public float RoutineLengthSeconds { get { return RoutineLengthMinutes * 60f; } }
		TeamData currentPlayingTeam = null;
		public TeamData CurrentPlayingTeam
		{
			get { return currentPlayingTeam; }
			set
			{
				currentPlayingTeam = value;

				NotifyPropertyChanged("NowPlayingString");
				NotifyPropertyChanged("StartButtonText");
				NotifyPropertyChanged("StartButtonTextColor");
			}
		}
		RoutineTimers RoutineTimer = new RoutineTimers(() => ServerWindow.OnFinishRoutineTimer(), () => ServerWindow.OnUpdateRoutineTimer());
		int CancelRoutineClickCount = 0;
		float CancelRoutineTimeLimit = .4f;
		DateTime CancelRoutineClickTime;
		float StartAfterCancelTimeLimit = .75f;
		List<System.Timers.Timer> TimeCallTimers = new List<System.Timers.Timer>();
		SpeechSynthesizer Speech = new SpeechSynthesizer();
		public bool bInvalidSave = false;
		DateTime LastJudgeUpdateTime = DateTime.Now;

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
		public bool CurrentPlayingTeamHasResults
		{
			get
			{
				if (CurrentPlayingTeam != null)
				{
					return CurrentPlayingTeam.HasScores;
				}

				return false;
			}
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
				else if (CurrentPlayingTeamHasResults)
				{
					return "Click to Set Next Team";
				}
				else
				{
					return "Click on First Throw";
				}
			}
		}
		public Brush StartButtonTextColor
		{
			get
			{
				if (CurrentPlayingTeam == null)
				{
					return Brushes.Crimson;
				}
				else if (IsJudging)
				{
					return Brushes.DarkOrange;
				}
				else if (CurrentPlayingTeamHasResults)
				{
					return Brushes.DarkOrange;
				}
				else
				{
					return Brushes.DarkSeaGreen;
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
		public string WindowTitle
		{
			get { return "Server - " + ListeningUrl + " - " + SaveFilename; }
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
			if (File.Exists(Properties.Settings.Default.LastSaveFilenamePath))
			{
				SaveFilename = Properties.Settings.Default.LastSaveFilenamePath;
			}

			this.WindowState = Properties.Settings.Default.WindowMaximized ? WindowState.Maximized : this.WindowState;

			Speech.SetOutputToDefaultAudioDevice();

			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			SendSplitTimer.Interval = 500;
			SendSplitTimer.AutoReset = true;
			SendSplitTimer.Elapsed += SendSplitTimer_Elapsed;

			AppendHandlers();

			JudgesControl.ItemsSource = Clients.Clients;

			TeamsControl.ItemsSource = SaveDataInst.TeamList;

			this.DataContext = this;

			//Start listening for incoming connections
			Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0), true);

			NetworkCommsDotNet.Tools.PeerDiscovery.EnableDiscoverable(NetworkCommsDotNet.Tools.PeerDiscovery.DiscoveryMethod.UDPBroadcast);

			using (System.Net.Sockets.Socket socket =
				new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
			{
				socket.Connect("8.8.8.8", 65530);
				IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
				ListeningUrl = endPoint.ToString();
			}

			Load();

			routineLengthMinutesString = RoutineLengthMinutes.ToString();
			RoutineLengthMinutes = routineLengthMinutes;
		}

		private void SendSplitTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			SendSplitToClients();
		}

		private void SendSplitToClients()
		{
			float totalPoints = 0f;

			foreach (ClientData cd in Clients.Clients)
			{
				if (cd.ClientId.ClientType == EClientType.Judge && cd.LastSplit != null)
				{
					totalPoints += cd.LastSplit.TotalPoints;
				}
			}

			foreach (ClientData cd in Clients.Clients)
			{
				if (IsExternalDisplay(cd))
				{
					cd.ConnectionData.SendObject("ServerSendSplit", totalPoints);
				}
			}
		}

		private void AppendHandlers()
		{
			NetworkComms.AppendGlobalConnectionEstablishHandler(OnConnectionEstablished);
			NetworkComms.AppendGlobalConnectionCloseHandler(OnConnectionClosed);

			NetworkComms.AppendGlobalIncomingPacketHandler<ClientIdData>("ClientConnect", HandleClientConnect);
			NetworkComms.AppendGlobalIncomingPacketHandler<ScoreSplitData>("JudgeScoreUpdate", (header, connection, scoreUpdate) => { HandleJudgeScoreUpdate(header, connection, scoreUpdate); });
			NetworkComms.AppendGlobalIncomingPacketHandler<DialRoutineScoreData>("JudgeFinishedScore", HandleJudgeFinishedScore);
			NetworkComms.AppendGlobalIncomingPacketHandler<DialRoutineScoreData>("JudgeSendBackupScore", HandleJudgeSendBackupScore);
		}

		private static void HandleClientConnect(PacketHeader header, Connection connection, ClientIdData clientInfo)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(() =>
			{
				Clients.Add(connection, clientInfo, () => ServerWindow.UpdateJudgesForClients());

				if (ServerWindow.IsExternalDisplay(clientInfo))
				{
					// Init the new scorboard
					ServerWindow.SendUpdatesToExternalDisplay();
				}
			}));
		}

		private void HandleJudgeScoreUpdate(PacketHeader header, Connection connection, ScoreSplitData scoreUpdate)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.SetLastJudgeScore(scoreUpdate);

				CheckShouldRoutineStart();
			}));
		}

		private static void HandleJudgeFinishedScore(PacketHeader header, Connection connection, DialRoutineScoreData score)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ServerWindow.SetTeamScore(score);

				ServerWindow.NotifyPropertyChanged("StartButtonText");
				ServerWindow.NotifyPropertyChanged("StartButtonTextColor");
			}));
		}

		private static void HandleJudgeSendBackupScore(PacketHeader header, Connection connection, DialRoutineScoreData score)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ServerWindow.SetTeamScore(score);
			}));
		}

		private void SendObject<T>(string rpcName, T obj)
		{
			foreach (Connection connection in NetworkComms.GetExistingConnection())
			{
				if (connection.ConnectionInfo.ConnectionType == ConnectionType.TCP)
				{
					connection.SendObject(rpcName, obj);
				}
			}
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

				SendUpdatesToExternalDisplay();

				Save();
			}
		}

		void CheckShouldRoutineStart()
		{
			if (Clients.Clients.Count > 0 && !IsJudging)
			{
				bool bAllJudgesJudging = true;
				DateTime updateTime = new DateTime();
				foreach (ClientData cd in Clients.Clients)
				{
					if (cd.SecondSinceLastUpdate > 5 || cd.LastJudgeValue <= 0)
					{
						bAllJudgesJudging = false;
						break;
					}
					else if (cd.LastUpdateTime > updateTime)
					{
						updateTime = cd.LastUpdateTime;
					}
				}

				if (bAllJudgesJudging && updateTime > LastJudgeUpdateTime)
				{
					LastJudgeUpdateTime = updateTime;

					Storyboard flashStartButton = FindResource("FlashStartButtonButton") as Storyboard;
					flashStartButton.Begin();

					Speech.SpeakAsyncCancelAll();
					Speech.SpeakAsync("Do you need to start routine?");
				}
			}
		}

		List<GraphBarList> CalcGraphData(List<TeamData> teamList)
		{
			List<GraphBarList> retList = new List<GraphBarList>();
			foreach (TeamData td in teamList)
			{
				retList.Add(new GraphBarList());
			}
			
			float barTime = RoutineLengthSeconds / CommonValues.GraphBarCount;
			float maxScore = CommonValues.MaxScorePerSecond * barTime;
			for (int barIndex = 0; barIndex < CommonValues.GraphBarCount; ++barIndex)
			{
				float startTime = barIndex * barTime;
				float endTime = barIndex * barTime + barTime;
				List<Tuple<int, float, float>> sortedBarData = new List<Tuple<int, float, float>>();
				for (int teamIndex = 0; teamIndex < teamList.Count; ++teamIndex)
				{
					float scoreNormalized = Math.Min(1f, teamList[teamIndex].CalcScoreWindow(startTime, endTime) / maxScore);
					float scoreUpToNow = teamList[teamIndex].CalcScoreWindow(0, endTime);
					Tuple<int, float, float> newBarData = new Tuple<int, float, float>(teamIndex, scoreNormalized, scoreUpToNow);

					int insertIndex = 0;
					foreach (Tuple<int, float, float> barData in sortedBarData)
					{
						if (scoreUpToNow > barData.Item3)
						{
							sortedBarData.Insert(insertIndex, newBarData);

							break;
						}

						++insertIndex;
					}

					if (insertIndex == sortedBarData.Count)
					{
						sortedBarData.Add(newBarData);
					}
				}

				int rank = 1;
				foreach (Tuple<int, float, float> barData in sortedBarData)
				{
					retList[barData.Item1].GraphData.Add(new GraphBarData(rank, barData.Item2));

					++rank;
				}
			}

			return retList;
		}

		void SendResultsToExtenalDisplay()
		{
			List<TeamData> sortedTeams = CalcSortedTeams();
			ScoreboardResultsData results = new ScoreboardResultsData();

			List<GraphBarList> graphBarData = CalcGraphData(sortedTeams);

			int teamIndex = 0;
			foreach (TeamData team in sortedTeams)
			{
				if (!team.IsScratch)
				{
					ScoreboardTeamResultData teamResult = new ScoreboardTeamResultData();

					teamResult.PlayerNames = team.PlayerNamesString;
					teamResult.Rank = team.Rank;
					teamResult.TotalPoints = team.TotalScore;
					teamResult.GraphData = graphBarData[teamIndex];

					results.Results.Add(teamResult);
				}

				++teamIndex;
			}

			foreach (ClientData cd in Clients.Clients)
			{
				if (IsExternalDisplay(cd))
				{
					cd.ConnectionData.SendObject("ServerSendResults", results);
				}
			}
		}

		void SendUpNextTeamsToExternalDisplay()
		{
			List<TeamData> upNextTeams = new List<TeamData>();
			foreach (TeamData team in SaveDataInst.TeamList)
			{
				if (team.Rank == 0 && team != CurrentPlayingTeam && !team.IsScratch)
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
				if (IsExternalDisplay(cd))
				{
					cd.ConnectionData.SendObject("ServerSendUpNextTeams", upNextData);
				}
			}
		}

		void SendUpdatesToExternalDisplay()
		{
			SendResultsToExtenalDisplay();
			SendUpNextTeamsToExternalDisplay();
		}

		bool IsExternalDisplay(ClientData cd)
		{
			return IsExternalDisplay(cd.ClientId);
		}

		bool IsExternalDisplay(ClientIdData clientId)
		{
			return clientId.ClientType == EClientType.Scoreboard || clientId.ClientType == EClientType.Overlay;
		}

		private static void OnConnectionEstablished(Connection connection)
		{
		}

		private static void OnConnectionClosed(Connection connection)
		{
			if (Application.Current != null)
			{
				Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
				{
					Clients.Remove(connection);
				}));
			}
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			Properties.Settings.Default.WindowMaximized = this.WindowState == WindowState.Maximized;
			Properties.Settings.Default.Save();

			Save();

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
			if (RoutineTimer.IsRoutinePlaying)
			{
				return;
			}

			bool bCancel = false;
			if (playingTeam != null && playingTeam.HasScores)
			{
				if (MessageBox.Show("This team has scores! Do you want to overwrite scores for:\r\n" + playingTeam.PlayerNamesString + "?", "Attention!",
				MessageBoxButton.OKCancel) == MessageBoxResult.OK)
				{
					ClearTeamScores(playingTeam);
				}
				else
				{
					bCancel = true;
				}
			}

			if (!bCancel)
			{
				CurrentPlayingTeam = playingTeam;

				foreach (TeamData td in SaveDataInst.TeamList)
				{
					td.IsPlaying = td == playingTeam;
				}

				InitRoutineData routineData = new InitRoutineData(playingTeam == null ? "No Team" : playingTeam.PlayerNamesString, RoutineLengthMinutes);

				SendObject("ServerSetPlayingTeam", routineData);

				SendUpdatesToExternalDisplay();
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
				if (CurrentPlayingTeamHasResults)
				{
					ServerWindow.TryIncrementPlayingTeam();
				}
				else if (secondsSinceCancelClick > StartAfterCancelTimeLimit)
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
				Speech.SpeakAsyncCancelAll();

				Storyboard flashStartButton = FindResource("FlashStartButtonButton") as Storyboard;
				flashStartButton.Stop();

				RoutineTimer.StartRoutine(RoutineLengthMinutes);

				NotifyPropertyChanged("StartButtonText");
				NotifyPropertyChanged("StartButtonTextColor");

				InitRoutineData routineData = new InitRoutineData(CurrentPlayingTeam.PlayerNamesString, RoutineLengthMinutes);

				foreach (Connection connection in NetworkComms.GetExistingConnection())
				{
					connection.SendObject("ServerStartRoutine", routineData);
				}

				SendSplitTimer.Start();

				StartTimeCallTimers(CurrentPlayingTeam);
			}
		}

		void StopTimeCallTimers()
		{
			foreach (System.Timers.Timer timer in TimeCallTimers)
			{
				timer.Stop();
			}

			TimeCallTimers.Clear();
		}

		void StartTimeCallTimers(TeamData team)
		{
			StopTimeCallTimers();
			float preTimeSeconds = 5f;
			float routineLengthSeconds = RoutineLengthMinutes * 60f - preTimeSeconds;

			if (team.TimeCall10sEnabled && routineLengthSeconds > 10f)
			{
				System.Timers.Timer newTimer = new System.Timers.Timer();
				newTimer.Interval = (routineLengthSeconds - 10f) * 1000f;
				newTimer.Elapsed += (sender, e) => { SpeakTimeCall(ETimeCall.TimeCall10s); };
				TimeCallTimers.Add(newTimer);
			}
			if (team.TimeCall15sEnabled && routineLengthSeconds > 15f)
			{
				System.Timers.Timer newTimer = new System.Timers.Timer();
				newTimer.Interval = (routineLengthSeconds - 15f) * 1000f;
				newTimer.Elapsed += (sender, e) => { SpeakTimeCall(ETimeCall.TimeCall15s); };
				TimeCallTimers.Add(newTimer);
			}
			if (team.TimeCall20sEnabled && routineLengthSeconds > 20f)
			{
				System.Timers.Timer newTimer = new System.Timers.Timer();
				newTimer.Interval = (routineLengthSeconds - 20f) * 1000f;
				newTimer.Elapsed += (sender, e) => { SpeakTimeCall(ETimeCall.TimeCall20s); };
				TimeCallTimers.Add(newTimer);
			}
			if (team.TimeCall30sEnabled && routineLengthSeconds > 30f)
			{
				System.Timers.Timer newTimer = new System.Timers.Timer();
				newTimer.Interval = (routineLengthSeconds - 30f) * 1000f;
				newTimer.Elapsed += (sender, e) => { SpeakTimeCall(ETimeCall.TimeCall30s); };
				TimeCallTimers.Add(newTimer);
			}
			if (team.TimeCall1mEnabled && routineLengthSeconds > 60f)
			{
				System.Timers.Timer newTimer = new System.Timers.Timer();
				newTimer.Interval = (routineLengthSeconds - 60f) * 1000f;
				newTimer.Elapsed += (sender, e) => { SpeakTimeCall(ETimeCall.TimeCall1m); };
				TimeCallTimers.Add(newTimer);
			}

			foreach (System.Timers.Timer timer in TimeCallTimers)
			{
				timer.AutoReset = false;
				timer.Start();
			}
		}

		void SpeakTimeCall(ETimeCall timeCall)
		{
			string message = GetTimeCallMessage(timeCall);

			Speech.SpeakAsync(message);
		}

		string GetTimeCallMessage(ETimeCall timeCall)
		{
			string attensionString = "Attention, Time call for ";

			switch (timeCall)
			{
				case ETimeCall.TimeCall10s:
					return attensionString + "10 seconds in 3, 2, 1";
				case ETimeCall.TimeCall15s:
					return attensionString + "15 seconds in 3, 2, 1";
				case ETimeCall.TimeCall20s:
					return attensionString + "20 seconds in 3, 2, 1";
				case ETimeCall.TimeCall30s:
					return attensionString + "30 seconds in 3, 2, 1";
				case ETimeCall.TimeCall1m:
					return attensionString + "1 minute in 3, 2, 1";
			}

			return "";
		}

		void CancelRoutine()
		{
			RoutineTimer.StopRoutine();

			NotifyPropertyChanged("StartButtonText");
			NotifyPropertyChanged("StartButtonTextColor");

			foreach (Connection connection in NetworkComms.GetExistingConnection())
			{
				connection.SendObject("ServerCancelRoutine", "");
			}

			StopSendingSplits();

			StopTimeCallTimers();
		}

		void StopSendingSplits()
		{
			SendSplitToClients();
			SendSplitTimer.Stop();
		}

		void UpdateJudgesForClients()
		{
			for (int clientIndex = 0, judgeIndex = 0; clientIndex < Clients.Clients.Count; ++clientIndex)
			{
				JudgeData jd = judgeIndex < SaveDataInst.ImportedJudges.Count ? SaveDataInst.ImportedJudges[judgeIndex] : new JudgeData();
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
			bool bFoundLastPlayedTeam = false;
			for (int i = 0; i < teamList.Count; ++i)
			{
				TeamData team = teamList[i];
				bFoundLastPlayedTeam |= CurrentPlayingTeam == team;
				if (bFoundLastPlayedTeam)
				{
					if (i < teamList.Count - 1)
					{
						TeamData nextTeam = teamList[i + 1];
						if (!nextTeam.IsScratch && !nextTeam.HasScores)
						{
							SetPlayingTeam(nextTeam);

							return;
						}
					}
					else
					{
						SetPlayingTeam(null);

						return;
					}
				}
			}
		}

		void ImportTeamsFromTextbox()
		{
			if (SaveDataInst.TeamList.Count > 0)
			{
				if (MessageBox.Show("Overwrite Current Data?", "Attention!", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
				{
					return;
				}
			}

			ImportTeamsFromText(TeamsTextBox.Text);
		}

		private void ImportTeamsFromText(string inText)
		{
			string[] splitters = { ",", "-", "/", "|" };
			StringReader text = new StringReader(inText);

			SaveDataInst.TeamList.Clear();

			string line = null;
			while ((line = text.ReadLine()) != null)
			{
				if (line.Contains("Judges"))
				{
					ImportJudgesFromText(text.ReadToEnd());
					break;
				}
				else if (line.StartsWith("//") || line.Trim().Length == 0)
				{
					continue;
				}
				else
				{
					TeamData newTeam = new TeamData();
					newTeam.UpdateAllTeamScores = OnTeamScoreUpdate;

					string[] names = line.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
					foreach (string name in names)
					{
						newTeam.PlayerNames.Add(name.Trim());
					}

					SaveDataInst.TeamList.Add(newTeam);
				}
			}

			Save();
			SendUpdatesToExternalDisplay();
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
			if (SaveDataInst.ImportedJudges.Count > 0)
			{
				if (MessageBox.Show("Overriding old Judges! Continue?", "Attention!",
					MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
				{
					return;
				}
			}

			ImportJudgesFromText(JudgesTextBox.Text);
		}

		void ImportJudgesFromText(string inText)
		{
			SaveDataInst.ImportedJudges.Clear();

			string[] splitters = { ",", "-", "/", "|" };
			StringReader text = new StringReader(inText);

			bool bImportError = false;
			string line = null;
			while ((line = text.ReadLine()) != null)
			{
				if (line.StartsWith("//") || line.Trim().Length == 0)
				{
					continue;
				}
				else
				{
					string[] judgeParams = line.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
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

							MessageBox.Show("Failed to import line (Unknown category):\r\n" + line, "Attention!");
						}
					}
					else
					{
						// Parse Error
						bImportError = true;

						MessageBox.Show("Failed to import line (Incorrect comma usage):\r\n" + line, "Attention!");
					}
				}
			}

			if (!bImportError)
			{
				UpdateJudgesForClients();

				Save();
			}
		}

		void Save()
		{
			if (bInvalidSave)
			{
				DoSaveAsDialog();
			}
			else
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

						SaveLastFilenamePath();
					}
				}
				catch (Exception e)
				{
					MessageBox.Show("Failed to Save.\r\n" + e.Message, "Attention!");
				}
			}
		}

		void Load()
		{
			try
			{
				SaveDataInst.ClearData();

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
							td.UpdateAllTeamScores = OnTeamScoreUpdate;
						}

						UpdateTeamsRank();

						RoutineLengthMinutes = SaveDataInst.RoutineLengthMinutes;

						UpdateJudgesForClients();
						UpdateResultsText();

						SaveLastFilenamePath();

						SendUpdatesToExternalDisplay();
					}
				}
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to Load.\r\n" + e.Message, "Attention!");
			}
		}

		private void SaveLastFilenamePath()
		{
			Properties.Settings.Default.LastSaveFilenamePath = System.IO.Path.GetFullPath(SaveFilename);
			Properties.Settings.Default.Save();
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			Save();
		}

		public void OnFinishRoutineTimer()
		{
			StopSendingSplits();

			NotifyPropertyChanged("TimeRemainingString");
			NotifyPropertyChanged("StartButtonText");
			NotifyPropertyChanged("StartButtonTextColor");
		}

		public void OnUpdateRoutineTimer()
		{
			NotifyPropertyChanged("TimeRemainingString");
		}

		private void ClearScores_Click(object sender, RoutedEventArgs e)
		{
			TeamData td = (sender as Button).Tag as TeamData;

			if (MessageBox.Show("Are you sure you want to Clear Scores for team:\r\n" + td.PlayerNamesString + "?", "Attention!", 
				MessageBoxButton.OKCancel) == MessageBoxResult.OK)
			{
				ClearTeamScores(td);
			}
		}

		private void ClearTeamScores(TeamData team)
		{
			team.ClearScores(SaveDataInst.TeamList);

			SendUpdatesToExternalDisplay();

			UpdateResultsText();
		}

		public void UpdateTeamsRank()
		{
			foreach (TeamData team1 in SaveDataInst.TeamList)
			{
				float score1Total = team1.TotalScore;
				int rank = 1;
				if (team1.JudgesScores.Count > 0 && !team1.IsScratch)
				{
					foreach (TeamData team2 in SaveDataInst.TeamList)
					{
						if (score1Total < team2.TotalScore && !team2.IsScratch)
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

		public void OnTeamScoreUpdate()
		{
			UpdateTeamsRank();

			SendUpdatesToExternalDisplay();

			UpdateResultsText();

			Save();
		}

		private void Open_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.DefaultExt = ".txt";
			ofd.Filter = "Text Files (*.txt)|*.txt";
			ofd.Multiselect = false;

			if (ofd.ShowDialog() == true)
			{
				SaveFilename = ofd.FileName;

				bInvalidSave = false;

				Load();
			}
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.DefaultExt = ".txt";
			ofd.Filter = "Text Files (*.txt)|*.txt";
			ofd.Multiselect = false;

			if (ofd.ShowDialog() == true)
			{
				if (SaveDataInst.TeamList.Count == 0 ||
					MessageBox.Show("Overwrite Current Data?", "Attention!", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes)
				{
					try
					{
						using (StreamReader file = new StreamReader(ofd.FileName))
						{
							bInvalidSave = true;

							ImportTeamsFromText(file.ReadToEnd());
						}
					}
					catch
					{
					}
				}
			}
		}

		private void SaveAsItem_Click(object sender, RoutedEventArgs e)
		{
			DoSaveAsDialog();
		}

		private void DoSaveAsDialog()
		{
			Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
			dlg.FileName = "DialJudgerServerSave";
			dlg.DefaultExt = ".txt";
			dlg.Filter = "Text documents (.txt)|*.txt";

			if (dlg.ShowDialog() == true)
			{
				SaveFilename = dlg.FileName;

				bInvalidSave = false;

				Save();
			}
		}
	}

	public class ClientData : INotifyPropertyChanged
	{
		public Connection ConnectionData = null;
		public ClientIdData ClientId = null;
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
			get { return LastSplit != null ? LastSplit.DialValue : -1f; }
		}
		public string ClientTypeString
		{
			get { return ClientId.ClientType.ToString(); }
		}
		ScoreSplitData lastSplit = null;
		public ScoreSplitData LastSplit
		{
			get { return lastSplit; }
			set
			{
				if (lastSplit == null || !lastSplit.CompareTo(value))
				{
					lastUpdateTime = DateTime.Now;
				}

				lastSplit = value;

				NotifyPropertyChanged("LastSplit");
				NotifyPropertyChanged("LastJudgeValue");
				NotifyPropertyChanged("JudgeValue");
				NotifyPropertyChanged("SecondSinceLastUpdate");
			}
		}
		DateTime lastUpdateTime = new DateTime();
		public DateTime LastUpdateTime { get { return lastUpdateTime; } }
		public double SecondSinceLastUpdate
		{
			get { return (DateTime.Now - lastUpdateTime).TotalSeconds; }
		}

		// Display
		public string ClientName { get { return ClientId == null ? "None" : ClientId.DisplayName; } }
		public string JudgeValue
		{
			get
			{
				return (IsScoreboard || IsOverlay) ? "" : LastJudgeValue.ToString("0.0");
			}
		}
		public string JudgeName
		{
			get
			{
				if (IsScoreboard)
				{
					return "Scoreboard";
				}
				else if (IsOverlay)
				{
					return "Overlay";
				}
				else if (Judge == null)
				{
					return "None";
				}
				else
				{
					return Judge.JudgeName;
				}
			}
		}
		public string JudgeCategory { get { return Judge == null ? "None" : Judge.Category.ToString(); } }
		public bool IsScoreboard { get { return ClientId != null && ClientId.ClientType == EClientType.Scoreboard; } }
		public bool IsOverlay { get { return ClientId != null && ClientId.ClientType == EClientType.Overlay; } }

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

		public void RemoveNull()
		{

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

		public void SetSplit(Connection connection, ScoreSplitData split)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.ConnectionData == connection)
				{
					cd.LastSplit = split;
					return;
				}
			}
		}

		public void SetLastJudgeScore(ScoreSplitData split)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.ClientId.CompareTo(split.Judge))
				{
					cd.LastSplit = split;

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
		public List<DialRoutineScoreData> JudgesScores = new List<DialRoutineScoreData>();
		[XmlIgnoreAttribute]
		public Action UpdateAllTeamScores;
		int rank = 0;
		public int Rank
		{
			get { return rank; }
			set
			{
				rank = value;
				NotifyPropertyChanged("Rank");
				NotifyPropertyChanged("RankString");
				NotifyPropertyChanged("DetailedJudgeScoresString");
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
				foreach (DialRoutineScoreData score in JudgesScores)
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

				foreach (DialRoutineScoreData score in JudgesScores)
				{
					ret += score.JudgeName + ": " + score.GetScoreString() + "  ";
				}

				ret += IsScratch ? "SCRATCHED" : "";

				return ret;
			}
		}
		bool bIsScratch = false;
		public bool IsScratch
		{
			get { return bIsScratch; }
			set
			{
				bIsScratch = value;

				NotifyPropertyChanged("IsScratch");

				UpdateAfterScoreUpdate();
			}
		}
		public bool HasScores { get { return TotalScore > 0; } }
		bool[] timeCallEnables = { false, false, false, false, false };
		public bool TimeCall10sEnabled
		{
			get { return timeCallEnables[(int)ETimeCall.TimeCall10s]; }
			set
			{
				timeCallEnables[(int)ETimeCall.TimeCall10s] = value;

				NotifyPropertyChanged("TimeCall10sEnabled");
			}
		}
		public bool TimeCall15sEnabled
		{
			get { return timeCallEnables[(int)ETimeCall.TimeCall15s]; }
			set
			{
				timeCallEnables[(int)ETimeCall.TimeCall15s] = value;

				NotifyPropertyChanged("TimeCall15sEnabled");
			}
		}
		public bool TimeCall20sEnabled
		{
			get { return timeCallEnables[(int)ETimeCall.TimeCall20s]; }
			set
			{
				timeCallEnables[(int)ETimeCall.TimeCall20s] = value;

				NotifyPropertyChanged("TimeCall20sEnabled");
			}
		}
		public bool TimeCall30sEnabled
		{
			get { return timeCallEnables[(int)ETimeCall.TimeCall30s]; }
			set
			{
				timeCallEnables[(int)ETimeCall.TimeCall30s] = value;

				NotifyPropertyChanged("TimeCall30sEnabled");
			}
		}
		public bool TimeCall1mEnabled
		{
			get { return timeCallEnables[(int)ETimeCall.TimeCall1m]; }
			set
			{
				timeCallEnables[(int)ETimeCall.TimeCall1m] = value;

				NotifyPropertyChanged("TimeCall1mEnabled");
			}
		}

		void UpdateAfterScoreUpdate()
		{
			if (UpdateAllTeamScores != null)
			{
				UpdateAllTeamScores();
			}

			NotifyPropertyChanged("TotalScoreString");
			NotifyPropertyChanged("DetailedJudgeScoresString");
		}

		public void SetFinishedScore(ObservableCollection<TeamData> teamList, DialRoutineScoreData newScore)
		{
			for (int i = 0; i < JudgesScores.Count; ++i)
			{
				DialRoutineScoreData score = JudgesScores[i];

				if (score.IsSameInstance(newScore))
				{
					JudgesScores[i] = newScore;

					UpdateAfterScoreUpdate();

					return;
				}
			}

			JudgesScores.Add(newScore);

			UpdateAfterScoreUpdate();
		}

		public void ClearScores(ObservableCollection<TeamData> teamList)
		{
			JudgesScores.Clear();

			UpdateAfterScoreUpdate();
		}

		public float CalcScoreWindow(float startTime, float endTime)
		{
			float ret = 0;

			foreach (DialRoutineScoreData scoreData in JudgesScores)
			{
				ret += scoreData.CalcScoreWindow(startTime, endTime);
			}

			return ret;
		}
	}

	public class SaveData
	{
		public ObservableCollection<TeamData> TeamList = new ObservableCollection<TeamData>();
		public List<JudgeData> ImportedJudges = new List<JudgeData>();
		public float RoutineLengthMinutes = 3f;

		public void ClearData()
		{
			TeamList.Clear();
			ImportedJudges.Clear();
			RoutineLengthMinutes = 3f;
		}
	}

	public enum ETimeCall
	{
		TimeCall10s,
		TimeCall15s,
		TimeCall20s,
		TimeCall30s,
		TimeCall1m
	}
}
