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

namespace Overlay
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		System.Timers.Timer LowerDisplayTransitionTimer = new System.Timers.Timer();
		System.Timers.Timer HudTransitionTimer = new System.Timers.Timer();
		System.Timers.Timer PostStartFadeOutTimer = new System.Timers.Timer();
		System.Timers.Timer PostFinishFadeOutTimer = new System.Timers.Timer();
		ClientConnection ClientConn = new ClientConnection((newClientId) => { });
		RoutineTimers RoutineTimer;

		public string LowerDisplayText
		{
			get
			{
				if (CurrentTeamHasResults)
				{
					return CurrentPlayingPlayerNames + "   Rank: " + CurrentTeamResult.Rank + " " + CurrentTeamResult.TotalPointsString;
				}

				return CurrentPlayingPlayerNames;
			}
		}
		double lowerDisplayHeight = 0f;
		public double LowerDisplayHeight
		{
			get
			{
				return lowerDisplayHeight;
			}
			set
			{
				lowerDisplayHeight = value;
				NotifyPropertyChanged("LowerDisplayHeight");
			}
		}
		double lowerDisplayTextAlpha = 0f;
		public double LowerDisplayTextAlpha
		{
			get { return lowerDisplayTextAlpha; }
			set
			{
				lowerDisplayTextAlpha = value;
				NotifyPropertyChanged("LowerDisplayTextAlpha");
			}
		}
		double LowerDisplayInHeight
		{
			get { return TopLevelGrid.ActualHeight / 4f; }
		}

		double hudHeight = 0f;
		public double HudHeight
		{
			get
			{
				return hudHeight;
			}
			set
			{
				hudHeight = value;
				NotifyPropertyChanged("HudHeight");
			}
		}
		double hudTextAlpha = 0f;
		public double HudTextAlpha
		{
			get { return hudTextAlpha; }
			set
			{
				hudTextAlpha = value;
				NotifyPropertyChanged("HudTextAlpha");
			}
		}
		double HudInHeight
		{
			get { return 88; }
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
		string currentPlayingPlayerNames = "No Current Team";
		public string CurrentPlayingPlayerNames
		{
			get { return currentPlayingPlayerNames; }
			set
			{
				currentPlayingPlayerNames = value;

				NotifyPropertyChanged("CurrentPlayingPlayerNames");
				NotifyPropertyChanged("LowerDisplayText");
			}
		}
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
				NotifyPropertyChanged("SplitDeltaStringColor");
			}
		}
		public string SplitPointsString
		{
			get { return SplitPoints.ToString("0.0"); }
		}
		public float SplitDeltaPoints
		{
			get
			{
				float leaderSplitAverage = (float)(LeaderPoints / (RoutineLengthMinutes * 60f) * RoutineTimer.ElapsedSeconds);
				return SplitPoints - leaderSplitAverage;
			}
		}
		public string SplitDeltaPointsString
		{
			get
			{
				return SplitPoints > 0f ? (SplitDeltaPoints > 0 ? "+" : "") + SplitDeltaPoints.ToString("0.0") : "---";
			}
		}
		public float LeaderPoints
		{
			get { return ResultsList.Count > 0 ? ResultsList.First().TotalPoints : 0f; }
		}
		public ObservableCollection<TeamResultsData> ResultsList = new ObservableCollection<TeamResultsData>();
		public bool CurrentTeamHasResults
		{
			get
			{
				return CurrentTeamResult != null;
			}
		}
		public string RoutineTimeString
		{
			get { return RoutineTimer.IsRoutinePlaying ? RoutineTimer.RemainingTimeString : "0:00"; }
		}
		public Brush SplitDeltaStringColor
		{
			get
			{
				if (Math.Abs(SplitDeltaPoints) < .001f || SplitPoints == 0f)
				{
					return Brushes.Black;
				}
				else if (SplitDeltaPoints > 0f)
				{
					return Brushes.Green;
				}
				else
				{
					return Brushes.Red;
				}
			}
		}

		public TeamResultsData CurrentTeamResult
		{
			get
			{
				foreach (TeamResultsData result in ResultsList)
				{
					if (result.PlayerNames == CurrentPlayingPlayerNames && result.TotalPoints > 0)
					{
						return result;
					}
				}

				return null;
			}
		}

		double TransitionLength = 1f;
		double LowerDisplayTransitionTime = 0f;
		double LowerDisplayTargetHeight = 0f;
		double LowerDisplayTargetAlpha = 0f;
		double HudTransitionTime = 0f;
		double HudTargetHeight = 0f;
		double HudTargetAlpha = 0f;
		double UpdateIntervalMS = 30;
		DisplayState LowerDisplayState = DisplayState.Out;
		DisplayState HudState = DisplayState.Out;

		public MainWindow()
		{
			InitializeComponent();

			RoutineTimer = new RoutineTimers(() =>
			{
				LowerDisplayTransitionIn();
				PostFinishFadeOutTimer.Start();
			}, () =>
			{
				NotifyPropertyChanged("RoutineTimeString");
			});
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			TopLevelGrid.DataContext = this;

			LowerDisplayTransitionTimer.Interval = UpdateIntervalMS;
			LowerDisplayTransitionTimer.Elapsed += LowerDisplayTransitionTimer_Elapsed;
			LowerDisplayTransitionTimer.AutoReset = true;

			HudTransitionTimer.Interval = UpdateIntervalMS;
			HudTransitionTimer.Elapsed += HudTransitionTimer_Elapsed; ;
			HudTransitionTimer.AutoReset = true;

			PostStartFadeOutTimer.Interval = 5000;
			PostStartFadeOutTimer.Elapsed += PostStartFadeOutTimer_Elapsed;
			PostStartFadeOutTimer.AutoReset = false;

			PostFinishFadeOutTimer.Interval = 10000;
			PostFinishFadeOutTimer.Elapsed += PostFinishFadeOutTimer_Elapsed;
			PostFinishFadeOutTimer.AutoReset = false;

			AppendHandlers();

			ClientConn.StartConnection(EClientType.Overlay);
		}

		private void PostFinishFadeOutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			LowerDisplayTransitionOut();
			HudTransitionOut();
		}

		private void PostStartFadeOutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			LowerDisplayTransitionOut();
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

			PostStartFadeOutTimer.Start();

			HudTransitionIn();
		}

		private void HandleSetPlayingTeam(InitRoutineData startData)
		{
			CurrentPlayingPlayerNames = startData.PlayersNames;
			RoutineLengthMinutes = startData.RoutineLengthMinutes;
			SplitPoints = 0f;

			LowerDisplayTransitionIn();
		}

		private void HandleCancelRoutine(string param)
		{
			RoutineTimer.StopRoutine();

			LowerDisplayTransitionIn();
			HudTransitionOut();
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

				NotifyPropertyChanged("LowerDisplayText");
			}));
		}

		private void HandleServerUpNextTeams(ScoreboardUpNextData upNextData)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
			}));
		}

		private void HandleServerSendSplit(float split)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				SplitPoints = split;
			}));
		}

		private void LowerDisplayTransitionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			LowerDisplayTransitionTime += UpdateIntervalMS / 1000f;

			double t = LowerDisplayTransitionTime / TransitionLength;
			if (t >= 1)
			{
				LowerDisplayHeight = LowerDisplayTargetHeight;
				LowerDisplayTextAlpha = LowerDisplayTargetAlpha;

				LowerDisplayTransitionTimer.Stop();

				LowerDisplayState = LowerDisplayState == DisplayState.TransitionIn ? DisplayState.In : DisplayState.Out;
			}
			else if (t >= .5)
			{
				if (LowerDisplayState == DisplayState.TransitionIn)
				{
					LowerDisplayHeight = LowerDisplayTargetHeight;
					LowerDisplayTextAlpha = ((t - .5) / .5);
				}
				else
				{
					LowerDisplayTextAlpha = LowerDisplayTargetAlpha;
					LowerDisplayHeight = (1 - ((t - .5) / .5)) * LowerDisplayInHeight;
				}
			}
			else
			{
				if (LowerDisplayState == DisplayState.TransitionIn)
				{
					LowerDisplayHeight = (t / .5) * LowerDisplayInHeight;
				}
				else
				{
					LowerDisplayTextAlpha = (1 - (t / .5));
				}
			}
		}

		private void HudTransitionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			HudTransitionTime += UpdateIntervalMS / 1000f;

			double t = HudTransitionTime / TransitionLength;
			if (t >= 1)
			{
				HudHeight = HudTargetHeight;
				HudTextAlpha = HudTargetAlpha;

				HudTransitionTimer.Stop();

				HudState = HudState == DisplayState.TransitionIn ? DisplayState.In : DisplayState.Out;
			}
			else if (t >= .5)
			{
				if (HudState == DisplayState.TransitionIn)
				{
					HudHeight = HudTargetHeight;
					HudTextAlpha = ((t - .5) / .5);
				}
				else
				{
					HudTextAlpha = HudTargetAlpha;
					HudHeight = (1 - ((t - .5) / .5)) * HudInHeight;
				}
			}
			else
			{
				if (HudState == DisplayState.TransitionIn)
				{
					HudHeight = (t / .5) * HudInHeight;
				}
				else
				{
					HudTextAlpha = (1 - (t / .5));
				}
			}
		}

		public void LowerDisplayTransitionIn()
		{
			LowerDisplayTextAlpha = 0f;
			LowerDisplayTargetHeight = LowerDisplayInHeight;
			LowerDisplayTargetAlpha = 1f;
			LowerDisplayTransitionTime = 0f;
			LowerDisplayState = DisplayState.TransitionIn;

			LowerDisplayTransitionTimer.Start();

			NotifyPropertyChanged("LowerDisplayText");
		}

		public void LowerDisplayTransitionOut()
		{
			LowerDisplayTargetHeight = 0;
			LowerDisplayTargetAlpha = 0;
			LowerDisplayTransitionTime = 0f;
			LowerDisplayState = DisplayState.TransitionOut;

			LowerDisplayTransitionTimer.Start();

			NotifyPropertyChanged("LowerDisplayText");
		}

		public void HudTransitionIn()
		{
			HudTextAlpha = 0f;
			HudTargetHeight = HudInHeight;
			HudTargetAlpha = 1f;
			HudTransitionTime = 0f;
			HudState = DisplayState.TransitionIn;

			HudTransitionTimer.Start();
		}

		public void HudTransitionOut()
		{
			HudTargetHeight = 0;
			HudTargetAlpha = 0;
			HudTransitionTime = 0f;
			HudState = DisplayState.TransitionOut;

			HudTransitionTimer.Start();
		}
	}

	public enum DisplayState
	{
		In,
		Out,
		TransitionIn,
		TransitionOut,
		Max
	}
}
