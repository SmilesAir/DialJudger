using CommonClasses;
using NetworkCommsDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

namespace Scoreboard
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		ClientConnection ClientConn = new ClientConnection((newClientId) => { });
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		public string TournamentTitle
		{
			get { return "Frisbeer 2018"; }
		}
		string currentPlayingPlayerNames = "No Current Team";
		public string CurrentPlayingPlayerNames
		{
			get { return currentPlayingPlayerNames; }
			set
			{
				currentPlayingPlayerNames = value;

				NotifyPropertyChanged("CurrentPlayingPlayerNames");
			}
		}
		public ObservableCollection<TeamResultsData> ResultsList = new ObservableCollection<TeamResultsData>();
		public ObservableCollection<UpNextData> UpNextList = new ObservableCollection<UpNextData>();
		RoutineTimers RoutineTimer;
		public double TeamDisplayHeight
		{
			get { return ActualHeight / 14f; }
		}
		public double PointsDeltaWidth
		{
			get { return ActualWidth / 6f; }
		}
		float routineLengthMinutes = .1f;
		public float RoutineLengthMinutes
		{
			get { return routineLengthMinutes; }
			set
			{
				routineLengthMinutes = value;

				NotifyPropertyChanged("RoutineLengthMinutes");
			}
		}
		public float BetweenTeamBufferMinutes = 1f;
		float splitPoints = 0f;
		public float SplitPoints
		{
			get { return splitPoints; }
			set
			{
				splitPoints = value;

				NotifyPropertyChanged("SplitPoints");
				NotifyPropertyChanged("SplitPointsString");
				NotifyPropertyChanged("SplitDeltaPointsString");
			}
		}
		public string SplitPointsString
		{
			get { return SplitPoints > 0f ? "Current Points: " + SplitPoints.ToString("0.0") : ""; }
		}
		public float LeaderPoints
		{
			get { return ResultsList.Count > 0 ? ResultsList.First().TotalPoints : 0f; }
		}
		public string SplitDeltaPointsString
		{
			get
			{
				float leaderSplitAverage = (float)(LeaderPoints / (RoutineLengthMinutes * 60f) * RoutineTimer.ElapsedSeconds);
				float delta = SplitPoints - leaderSplitAverage;

				return SplitPoints > 0f ? "Delta Split: " + delta.ToString("0.0") : "";
			}
		}
		public string RoutineTimeString
		{
			get { return RoutineTimer.IsRoutinePlaying ? "Time: " + RoutineTimer.RemainingTimeString : ""; }
		}

		public MainWindow()
		{
			InitializeComponent();

			RoutineTimer = new RoutineTimers(() => { }, () => { NotifyPropertyChanged("RoutineTimeString"); });
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			TopLevelGrid.DataContext = this;
			ResultsControl.ItemsSource = ResultsList;
			UpNextControl.ItemsSource = UpNextList;

			AppendHandlers();

			ClientConn.StartConnection(EClientType.Scoreboard);
		}

		private void AppendHandlers()
		{
			NetworkComms.AppendGlobalIncomingPacketHandler<InitRoutineData>("ServerStartRoutine", (h, c, x) => HandleStartRoutine(x));
			NetworkComms.AppendGlobalIncomingPacketHandler<InitRoutineData>("ServerSetPlayingTeam", (h, c, x) => HandleSetPlayingTeam(x));
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ServerCancelRoutine", (h, c, x) => HandleCancelRoutine(x));
			NetworkComms.AppendGlobalIncomingPacketHandler<ScoreboardResultsData>("ServerSendResults", (h, c, x) => HandleServerResults(x));
			NetworkComms.AppendGlobalIncomingPacketHandler<ScoreboardUpNextData>("ServerSendUpNextTeams", (h, c, x) => HandleServerUpNextTeams(x));
			NetworkComms.AppendGlobalIncomingPacketHandler<float>("ServerSendSplit", (h, c, x) => HandleServerSendSplit(x));
		}

		private void HandleStartRoutine(InitRoutineData startData)
		{
			RoutineLengthMinutes = startData.RoutineLengthMinutes;
			RoutineTimer.StartRoutine(RoutineLengthMinutes);
		}

		private void HandleSetPlayingTeam(InitRoutineData startData)
		{
			CurrentPlayingPlayerNames = startData.PlayersNames;
			RoutineLengthMinutes = startData.RoutineLengthMinutes;
			SplitPoints = 0f;
		}

		private void HandleCancelRoutine(string param)
		{
			RoutineTimer.StopRoutine();
		}

		private void HandleServerResults(ScoreboardResultsData results)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ResultsList.Clear();

				float previousPoints = 0f;
				foreach (ScoreboardTeamResultData result in results.Results)
				{
					TeamResultsData scoreboardResult = new TeamResultsData(result);

					if (previousPoints != 0f)
					{
						scoreboardResult.DeltaPoints = result.TotalPoints - previousPoints;
					}

					ResultsList.Add(scoreboardResult);

					previousPoints = result.TotalPoints;
				}
			}));
		}

		private void HandleServerUpNextTeams(ScoreboardUpNextData upNextData)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				UpNextList.Clear();

				int i = 1;
				foreach (ScoreboardUpNextTeamData team in upNextData.UpNextTeams)
				{
					UpNextData upNextTeam = new UpNextData(team);
					upNextTeam.OnDeckNumber = i;
					upNextTeam.EtaMinutesToPlay = (int)(i * RoutineLengthMinutes + i * BetweenTeamBufferMinutes);

					UpNextList.Add(upNextTeam);

					++i;
				}
			}));
		}

		private void HandleServerSendSplit(float split)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				SplitPoints = split;
			}));
		}

		private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (this.WindowState == System.Windows.WindowState.Normal)
			{
				this.WindowState = System.Windows.WindowState.Maximized;
			}
			else
			{
				this.WindowState = System.Windows.WindowState.Normal;
			}
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			NotifyPropertyChanged("TeamDisplayHeight");
		}

		private void Window_StateChanged(object sender, EventArgs e)
		{
			if (this.WindowState == WindowState.Maximized)
			{
				this.WindowStyle = WindowStyle.None;
			}
			else
			{
				this.WindowStyle = WindowStyle.SingleBorderWindow;
			}
		}
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
}