using CommonClasses;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace Server
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		static string ListeningUrl = "";
		static public ClientList Clients = new ClientList();
		static SaveData SaveDataInst = new SaveData();
		static float RoutineLengthMinutes = 3f;
		TeamData CurrentPlayingTeam = null;

		public string NowPlayingString
		{
			get
			{
				if (CurrentPlayingTeam != null)
				{
					return CurrentPlayingTeam.TeamNames;
				}
				else
				{
					return "Need To Set Playing Team";
				}
			}
		}
		public string TimeRemainingString
		{
			get { return "1:53"; }
		}
		public string RoutineLengthString
		{
			get { return RoutineLengthMinutes.ToString(); }
			set
			{
				float.TryParse(value, out RoutineLengthMinutes);
			}
		}
		public string StartButtonText
		{
			get { return "Click on First Throw"; }
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
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			AppendHandlers();

			JudgesControl.ItemsSource = Clients.Clients;

			TeamsControl.ItemsSource = SaveDataInst.TeamList;

			TopLevelGrid.DataContext = this;

			TeamData td = new TeamData();
			td.PlayerNames.Add("Ryan Young");
			td.PlayerNames.Add("James Wiseman");
			SaveDataInst.TeamList.Add(td);
			td = new TeamData();
			td.PlayerNames.Add("Jake Gauthier");
			td.PlayerNames.Add("Arthur Coddington");
			SaveDataInst.TeamList.Add(td);
			SetPlayingTeam(SaveDataInst.TeamList[0]);

			Connection.StartListening(ConnectionType.UDP, new IPEndPoint(IPAddress.Any, 10000));

			//Start listening for incoming connections
			Connection.StartListening(ConnectionType.TCP, new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));

			//Print out the IPs and ports we are now listening on
			Console.WriteLine("Server listening for TCP connection on:");
			foreach (System.Net.IPEndPoint localEndPoint in Connection.ExistingLocalListenEndPoints(ConnectionType.TCP))
			{
				ListeningUrl = TeamsTextBox.Text = localEndPoint.Address + ":" + localEndPoint.Port;
			}
		}

		private void AppendHandlers()
		{
			NetworkComms.AppendGlobalConnectionEstablishHandler(OnConnectionEstablished);
			NetworkComms.AppendGlobalConnectionCloseHandler(OnConnectionClosed);

			NetworkComms.AppendGlobalIncomingPacketHandler<int>("BroadcastFindServer", HandleBroadcastFindServer);
			NetworkComms.AppendGlobalIncomingPacketHandler<ClientIdData>("ClientConnect", HandleClientConnect);
			NetworkComms.AppendGlobalIncomingPacketHandler<ScoreUpdateData>("JudgeScoreUpdate", HandleJudgeScoreUpdate);
		}

		private static void HandleBroadcastFindServer(PacketHeader header, Connection connection, int port)
		{
			UDPConnection.SendObject("BroadcastServerInfo", ListeningUrl, new IPEndPoint(IPAddress.Broadcast, port));
		}

		private static void HandleClientConnect(PacketHeader header, Connection connection, ClientIdData clientInfo)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.Add(connection, clientInfo);
			}));
		}

		private static void HandleJudgeScoreUpdate(PacketHeader header, Connection connection, ScoreUpdateData scoreUpdate)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				Clients.SetLastJudgeScore(scoreUpdate);
			}));
		}

		private static void OnConnectionEstablished(Connection connection)
		{
		}

		private static void OnConnectionClosed(Connection connection)
		{
			Clients.Remove(connection);
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

			//foreach (Connection connection in NetworkComms.GetExistingConnection())
			//{
			//	connection.SendObject("ServerSetPlayingTeam", playingTeam);
			//}

			NotifyPropertyChanged("NowPlayingString");
		}

		private void MenuItemExit_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		private void StartButton_Click(object sender, RoutedEventArgs e)
		{
			StartRoutine();
		}

		void StartRoutine()
		{
			foreach (Connection connection in NetworkComms.GetExistingConnection())
			{
				connection.SendObject("ServerStartRoutine", "");
			}
		}
	}

	public class ClientData : INotifyPropertyChanged
	{
		public Connection ConnectionData = null;
		public ClientIdData Judge = null;
		public RoutineData RoutineScores = new RoutineData();
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

		// Display
		public string JudgeName { get { return Judge == null ? "None" : Judge.DisplayName; } }
		public string JudgeValue { get { return LastJudgeValue.ToString("0.0"); } }

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
			Judge = id;
		}
	}

	public class ClientList
	{
		public ObservableCollection<ClientData> Clients = new ObservableCollection<ClientData>();

		public ClientList()
		{
		}

		public void Add(Connection connection, ClientIdData id)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.Judge.CompareTo(id))
				{
					return;
				}
			}

			Clients.Add(new ClientData(connection, id));
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
					cd.Judge = id;
					return;
				}
			}
		}

		public void SetLastJudgeScore(ScoreUpdateData scoreUpdate)
		{
			foreach (ClientData cd in Clients)
			{
				if (cd.Judge.CompareTo(scoreUpdate.Judge))
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
		public string TeamNames
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
		public RoutineScoreData Scores = new RoutineScoreData();
		public string ScoreString
		{
			get { return Scores.ScoreString; }
		}
		public int Rank = 0;
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
	}

	public class SaveData
	{
		public ObservableCollection<TeamData> TeamList = new ObservableCollection<TeamData>();
		public float RoutineLengthMinutes = 3f;
	}
}
