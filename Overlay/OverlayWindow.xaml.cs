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
		System.Timers.Timer PostStartFadeOutTimer = new System.Timers.Timer();
		System.Timers.Timer PostFinishFadeOutTimer = new System.Timers.Timer();
		ClientConnection ClientConn = new ClientConnection((newClientId) => { });
		RoutineTimers RoutineTimer;
		ObservableCollection<OutputRow> DisplayRows = new ObservableCollection<OutputRow>();
		TransitionInstance LowerDisplayTransition;
		TransitionInstance HudTransition;
		TransitionInstance ScoreboardTransition;

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
		double lowerDisplayTextOpacity = 0f;
		public double LowerDisplayTextOpacity
		{
			get { return lowerDisplayTextOpacity; }
			set
			{
				lowerDisplayTextOpacity = value;
				NotifyPropertyChanged("LowerDisplayTextOpacity");
			}
		}
		double LowerDisplayInHeight
		{
			get { return TopLevelGrid.ActualHeight / 6f; }
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
		double hudTextOpacity = 0f;
		public double HudTextOpacity
		{
			get { return hudTextOpacity; }
			set
			{
				hudTextOpacity = value;
				NotifyPropertyChanged("HudTextOpacity");
			}
		}
		double HudInHeight
		{
			get { return 88; }
		}

		double scoreboardTextOpacity = 0f;
		public double ScoreboardTextOpacity
		{
			get { return scoreboardTextOpacity; }
			set
			{
				scoreboardTextOpacity = value;
				NotifyPropertyChanged("ScoreboardTextOpacity");
			}
		}
		double scoreboardHeight = 0f;
		public double ScoreboardHeight
		{
			get { return scoreboardHeight; }
			set
			{
				scoreboardHeight = value;
				NotifyPropertyChanged("ScoreboardHeight");
			}
		}
		public double ScoreboardInHeight
		{
			get { return ActualHeight - 130; }
		}

		public double TeamDisplayHeight
		{
			get { return ActualHeight / 15f; }
		}
		public double PointsDeltaWidth
		{
			get { return ActualWidth / 6f; }
		}

		public Brush BackgroundColor
		{
			get { return Brushes.AntiqueWhite; }
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
		public ObservableCollection<UpNextData> UpNextList = new ObservableCollection<UpNextData>();
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

		public MainWindow()
		{
			InitializeComponent();

			RoutineTimer = new RoutineTimers(() =>
			{
				LowerDisplayTransition.TransitionIn();
				PostFinishFadeOutTimer.Start();
			}, () =>
			{
				NotifyPropertyChanged("RoutineTimeString");
			});
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			TopLevelGrid.DataContext = this;
			OutputRows.ItemsSource = DisplayRows;

			PostStartFadeOutTimer.Interval = 5000;
			PostStartFadeOutTimer.Elapsed += PostStartFadeOutTimer_Elapsed;
			PostStartFadeOutTimer.AutoReset = false;

			PostFinishFadeOutTimer.Interval = 10000;
			PostFinishFadeOutTimer.Elapsed += PostFinishFadeOutTimer_Elapsed;
			PostFinishFadeOutTimer.AutoReset = false;

			AppendHandlers();

			ClientConn.StartConnection(EClientType.Overlay);

			LowerDisplayTransition = new TransitionInstance(
				(x) => { LowerDisplayTextOpacity = x; },
				(x) => { LowerDisplayHeight = x; },
				LowerDisplayInHeight);
			HudTransition = new TransitionInstance(
				(x) => { HudTextOpacity = x; },
				(x) => { HudHeight = x; },
				HudInHeight);
			ScoreboardTransition = new TransitionInstance(
				(x) => { ScoreboardTextOpacity = x; },
				(x) => { ScoreboardHeight = x; },
				ScoreboardInHeight);
		}

		private void PostFinishFadeOutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			TransitionNonScoreboardElementsOut();
		}

		private void PostStartFadeOutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			LowerDisplayTransition.TransitionOut();
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

			HudTransition.TransitionIn();

			ScoreboardTransition.TransitionOut();
		}

		private void HandleSetPlayingTeam(InitRoutineData startData)
		{
			CurrentPlayingPlayerNames = startData.PlayersNames;
			RoutineLengthMinutes = startData.RoutineLengthMinutes;
			SplitPoints = 0f;

			LowerDisplayTransition.TransitionIn();
		}

		private void HandleCancelRoutine(string param)
		{
			RoutineTimer.StopRoutine();

			LowerDisplayTransition.TransitionIn();
			HudTransition.TransitionOut();
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
					scoreboardResult.RenderGraph(1000, (int)TeamDisplayHeight);

					if (previousPoints != 0f)
					{
						scoreboardResult.DeltaPoints = result.TotalPoints - previousPoints;
					}

					ResultsList.Add(scoreboardResult);

					previousPoints = result.TotalPoints;
				}

				UpdateDisplayRows();

				NotifyPropertyChanged("LowerDisplayText");
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
					upNextTeam.EtaMinutesToPlay = (int)(i * RoutineLengthMinutes + i * CommonValues.BetweenTeamBufferMinutes);

					UpNextList.Add(upNextTeam);

					++i;
				}

				UpdateDisplayRows();
			}));
		}

		private void HandleServerSendSplit(float split)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				SplitPoints = split;
			}));
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			NotifyPropertyChanged("TeamDisplayHeight");
			NotifyPropertyChanged("ScoreboardHeight");
		}

		void UpdateDisplayRows()
		{
			DisplayRows.Clear();

			if (ResultsList.Count > 0)
			{
				OutputRow results = new OutputRow();
				results.String1 = "Results";
				results.String1ColumnSpan = 5;
				results.BgColor = Brushes.LightGray;
				DisplayRows.Add(results);

				foreach (TeamResultsData result in ResultsList)
				{
					OutputRow row = new OutputRow(result);
					DisplayRows.Add(row);
				}
			}

			if (UpNextList.Count > 0)
			{
				OutputRow upNextRow = new OutputRow();
				upNextRow.String1 = "Up Next";
				upNextRow.String1ColumnSpan = 5;
				upNextRow.BgColor = Brushes.LightGray;
				DisplayRows.Add(upNextRow);

				foreach (UpNextData upNext in UpNextList)
				{
					OutputRow row = new OutputRow(upNext);
					DisplayRows.Add(row);
				}
			}

			NotifyPropertyChanged("DisplayRows");
		}

		private void Window_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				if (ScoreboardTransition.State != DisplayState.Out)
				{
					ScoreboardTransition.TransitionOut();
				}
				else
				{
					ScoreboardTransition.TransitionIn();

					TransitionNonScoreboardElementsOut();
				}
			}
		}

		private void TransitionNonScoreboardElementsOut()
		{
			LowerDisplayTransition.TransitionOut();
			HudTransition.TransitionOut();
		}
	}

	public class TransitionInstance
	{
		System.Timers.Timer UpdateTimer = new System.Timers.Timer();
		Action<double> UpdateOpacity;
		Action<double> UpdateHeight;
		double FullHeight = 0;
		double TargetHeight = 0;
		double TargetOpacity = 0;
		double UpdateIntervalMS = 30;
		double TransitionTime = 0;
		double TransitionLength = 1f;
		public DisplayState State = DisplayState.Out;

		public TransitionInstance(Action<double> updateOpacity, Action<double> updateHeight, double targetHeight)
		{
			FullHeight = targetHeight;
			UpdateOpacity = updateOpacity;
			UpdateHeight = updateHeight;

			UpdateTimer.AutoReset = true;
			UpdateTimer.Elapsed += UpdateTimer_Elapsed;
			UpdateTimer.Interval = UpdateIntervalMS;
		}

		private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			TransitionTime += UpdateIntervalMS / 1000f;

			double t = TransitionTime / TransitionLength;
			if (t >= 1)
			{
				UpdateHeight(TargetHeight);
				UpdateOpacity(TargetOpacity);

				UpdateTimer.Stop();

				State = State == DisplayState.TransitionIn ? DisplayState.In : DisplayState.Out;
			}
			else if (t >= .5)
			{
				if (State == DisplayState.TransitionIn)
				{
					UpdateHeight(TargetHeight);
					UpdateOpacity(((t - .5) / .5));
				}
				else
				{
					UpdateOpacity(TargetOpacity);
					UpdateHeight((1 - ((t - .5) / .5)) * FullHeight);
				}
			}
			else
			{
				if (State == DisplayState.TransitionIn)
				{
					UpdateHeight((t / .5) * FullHeight);
				}
				else
				{
					UpdateOpacity((1 - (t / .5)));
				}
			}
		}

		public void TransitionIn()
		{
			if (State != DisplayState.In && State != DisplayState.TransitionIn)
			{
				UpdateOpacity(0);
				TargetHeight = FullHeight;
				TargetOpacity = 1f;
				TransitionTime = 0f;
				State = DisplayState.TransitionIn;

				UpdateTimer.Start();
			}
		}

		public void TransitionOut()
		{
			if (State != DisplayState.Out && State != DisplayState.TransitionOut)
			{
				TargetHeight = 0;
				TargetOpacity = 0;
				TransitionTime = 0f;
				State = DisplayState.TransitionOut;

				UpdateTimer.Start();
			}
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

	public class OutputRow : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		string[] strings = new string[4];
		public string String1
		{
			get { return strings[0]; }
			set
			{
				strings[0] = value;
				NotifyPropertyChanged("String1");
			}
		}
		public string String2
		{
			get { return strings[1]; }
			set
			{
				strings[1] = value;
				NotifyPropertyChanged("String2");
			}
		}
		public string String3
		{
			get { return strings[2]; }
			set
			{
				strings[2] = value;
				NotifyPropertyChanged("String3");
			}
		}
		public string String4
		{
			get { return strings[3]; }
			set
			{
				strings[3] = value;
				NotifyPropertyChanged("String4");
			}
		}
		int string1ColumnSpan = 1;
		public int String1ColumnSpan
		{
			get { return string1ColumnSpan; }
			set
			{
				string1ColumnSpan = value;
				NotifyPropertyChanged("String1ColumnSpan");
			}
		}
		Brush bgColor = null;
		public Brush BgColor
		{
			get { return bgColor; }
			set
			{
				bgColor = value;
				NotifyPropertyChanged("BgColor");
			}
		}
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

		public OutputRow()
		{
		}

		public OutputRow(TeamResultsData result)
		{
			String1 = result.RankString;
			String2 = result.PlayerNames;
			String3 = result.TotalPointsString;
			String4 = result.DeltaPointsString;
			graphBmp = result.GraphBmp;
		}

		public OutputRow(UpNextData upNext)
		{
			String1 = upNext.OnDeckNumberString;
			String2 = upNext.PlayerNames;
			String4 = upNext.EstimatedTimeToPlayString;
		}
	}
}
