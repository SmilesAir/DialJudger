using CommonClasses;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

namespace Client
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		ClientConnection ClientCon = null;
		System.Timers.Timer SendScoreTimer = new System.Timers.Timer();
		RoutineTimers RoutineTimer = new RoutineTimers(() => ClientWindow.FinishRoutine(), () => ClientWindow.OnUpdateRoutine());
		public bool IsConnected
		{
			get { return ClientCon.IsConnected; }
		}
		private float dialValue = 0f;
		public float DialValue
		{
			get { return dialValue; }
			set
			{
				dialValue = value;
				NotifyPropertyChanged("DialValue");
				NotifyPropertyChanged("DisplayDialValue");
				NotifyPropertyChanged("SliderValue");
			}
		}
		public float SliderValue
		{
			get { return dialValue; }
			set
			{
				SetDialValue(value);

				NotifyPropertyChanged("SliderValue");
			}
		}
		private float DialIncrementValue = .1f;
		private EDialSpeed DialSpeed = EDialSpeed.Medium;
		public ClientIdData ClientId
		{
			get { return ClientCon.ClientId; }
			set
			{
				ClientCon.ClientId = value;

				NotifyPropertyChanged("ClientId");
				NotifyPropertyChanged("DisplayWindowTitle");
			}
		}
		bool bIsHooked = false;
		float routineLengthMinutes = 0f;
		public float RoutineLengthMinutes
		{
			get { return routineLengthMinutes; }
			set
			{
				routineLengthMinutes = value;
				RoutineTimer.RoutineLengthMinutes = value;

				NotifyPropertyChanged("RoutineLengthMinutes");
				NotifyPropertyChanged("DisplayTimeString");
			}
		}
		public bool IsJudging { get { return RoutineTimer.IsRoutinePlaying; } }
		public string DisplayTimeString
		{
			get
			{
				return "Time Remaining: " + RoutineTimer.RemainingTimeString;
			}
		}
		public DialRoutineScoreData RoutineScore = new DialRoutineScoreData();
		public string TotalScoreString
		{
			get
			{
				return "Total Score: " + RoutineScore.GetTotalScore((float)RoutineTimer.ElapsedSeconds).ToString("0.0");
			}
		}
		SpeechSynthesizer Speech = new SpeechSynthesizer();
		int volumeValue = 50;
		public int VolumeValue
		{
			get { return volumeValue; }
			set
			{
				volumeValue = value;
				Speech.Volume = value;

				NotifyPropertyChanged("VolumeValue");
			}
		}
		bool isSoundMuted = false;
		public bool IsSoundMuted
		{
			get { return isSoundMuted; }
			set
			{
				isSoundMuted = value;

				if (value)
				{
					Speech.Volume = 0;
				}
				else
				{
					Speech.Volume = VolumeValue;
				}

				NotifyPropertyChanged("IsSoundMuted");
			}
		}

		static MainWindow ClientWindow;

		#region Display
		public string DisplayWindowTitle { get { return "Dial Judge Client - " + (ClientId == null ? "None" : ClientId.DisplayName); } }
		private string judgeName = "No Judge Name";
		public string JudgeName
		{
			get { return judgeName; }
			set
			{
				judgeName = value;
				NotifyPropertyChanged("JudgeName");
				NotifyPropertyChanged("DisplayJudgeName");
			}
		}
		public string DisplayJudgeName
		{
			get { return "Judge: " + JudgeName; }
		}
		private string teamName = "No Team";
		public string TeamName
		{
			get { return teamName; }
			set
			{
				teamName = value;
				NotifyPropertyChanged("TeamName");
				NotifyPropertyChanged("DisplayTeamName");
			}
		}
		public string DisplayTeamName
		{
			get { return "Team: " + TeamName; }
		}

		private ECategory judgeCategory = ECategory.General;
		public ECategory JudgeCategory
		{
			get { return judgeCategory; }
			set
			{
				judgeCategory = value;
				NotifyPropertyChanged("JudgeCategory");
				NotifyPropertyChanged("DisplayCategoryName");
			}
		}
		public string DisplayCategoryName
		{
			get { return "Category: " + JudgeCategory.ToString(); }
		}
		public string DisplayDialValue { get { return DialValue.ToString("0.0"); } }
		public bool DisplayDialSpeedSlowChecked
		{
			get { return DialSpeed == EDialSpeed.Slow; }
			set
			{
				SetDialSpeed(EDialSpeed.Slow);

				NotifyPropertyChanged("DisplayDialSpeedSlowChecked");
				NotifyPropertyChanged("DisplayDialSpeedMediumChecked");
				NotifyPropertyChanged("DisplayDialSpeedFastChecked");
			}
		}
		public bool DisplayDialSpeedMediumChecked
		{
			get { return DialSpeed == EDialSpeed.Medium; }
			set
			{
				SetDialSpeed(EDialSpeed.Medium);

				NotifyPropertyChanged("DisplayDialSpeedSlowChecked");
				NotifyPropertyChanged("DisplayDialSpeedMediumChecked");
				NotifyPropertyChanged("DisplayDialSpeedFastChecked");
			}
		}
		public bool DisplayDialSpeedFastChecked
		{
			get { return DialSpeed == EDialSpeed.Fast; }
			set
			{
				SetDialSpeed(EDialSpeed.Fast);

				NotifyPropertyChanged("DisplayDialSpeedSlowChecked");
				NotifyPropertyChanged("DisplayDialSpeedMediumChecked");
				NotifyPropertyChanged("DisplayDialSpeedFastChecked");
			}
		}
		private void SetDialSpeed(EDialSpeed speed)
		{
			DialSpeed = speed;

			switch (DialSpeed)
			{
				case EDialSpeed.Slow:
					DialIncrementValue = .1f;
					break;
				case EDialSpeed.Medium:
					DialIncrementValue = .5f;
					break;
				case EDialSpeed.Fast:
					DialIncrementValue = 1f;
					break;
			}

			NotifyPropertyChanged("DisplayDialSpeedSlowChecked");
			NotifyPropertyChanged("DisplayDialSpeedMediumChecked");
			NotifyPropertyChanged("DisplayDialSpeedFastChecked");
		}
		#endregion

		#region KeyboardHooks
		private const int WH_KEYBOARD_LL = 13;
		private const int WM_KEYDOWN = 0x0100;
		private static LowLevelKeyboardProc proc = HookCallback;
		private static IntPtr hookID = IntPtr.Zero;
		#endregion

		#region NotifyChanged
		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
		#endregion

		public MainWindow()
		{
			InitializeComponent();

			this.WindowState = Properties.Settings.Default.WindowMaximized ? WindowState.Maximized : this.WindowState;

			ClientCon = new ClientConnection(newClientId => OnClientIdChanged(newClientId));

			ClientWindow = this;

			this.DataContext = this;

			SetDialSpeed(DialSpeed);

			Speech.SetOutputToDefaultAudioDevice();
			Speech.Volume = VolumeValue;
		}

		private void InitTimers()
		{
			SendScoreTimer.Interval = 500;
			SendScoreTimer.AutoReset = true;
			SendScoreTimer.Elapsed += SendScoreTimer_Elapsed;
			SendScoreTimer.Start();
		}

		void OnUpdateRoutine()
		{
			NotifyPropertyChanged("DisplayTimeString");
			NotifyPropertyChanged("TotalScoreString");
		}

		public void PlayNumberSound(float number)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				if (!IsSoundMuted)
				{
					Speech.SpeakAsyncCancelAll();
					Speech.SpeakAsync(decimal.Parse(number.ToString("0.0")).ToString("G29"));
				}
			}));
		}

		private void SendScoreTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (IsConnected)
			{
				NetworkComms.SendObject("JudgeScoreUpdate", ClientCon.ServerIp, ClientCon.ServerPort,
					new ScoreSplitData(ClientId, DialValue, RoutineScore.GetTotalScore((float)RoutineTimer.ElapsedSeconds)));
			}
		}

		#region Window Events
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Track track = (Track)ValueSlider.Template.FindName("PART_Track", ValueSlider);
			track.Thumb.Width = 80f;
			track.Thumb.Height = 80f;

			track = (Track)VolumeSlider.Template.FindName("PART_Track", VolumeSlider);
			track.Thumb.Width = 80f;
			track.Thumb.Height = 80f;

			AppendHandlers();

			InitTimers();

			SetHook();

			ClientCon.StartConnection(EClientType.Judge);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			Properties.Settings.Default.WindowMaximized = this.WindowState == WindowState.Maximized;
			Properties.Settings.Default.Save();

			NetworkComms.Shutdown();

			UnHook();
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			if (CommonDebug.bOnlyDialInputWhenFocused)
			{
				SetHook();
			}
		}

		private void Window_Deactivated(object sender, EventArgs e)
		{
			if (CommonDebug.bOnlyDialInputWhenFocused)
			{
				UnHook();
			}
		}
		#endregion

		private void AppendHandlers()
		{
			NetworkComms.AppendGlobalIncomingPacketHandler<InitRoutineData>("ServerStartRoutine", HandleStartRoutine);
			NetworkComms.AppendGlobalIncomingPacketHandler<InitRoutineData>("ServerSetPlayingTeam", HandleSetPlayingTeam);
			NetworkComms.AppendGlobalIncomingPacketHandler<JudgeData>("ServerSetJudgeInfo", HandleSetJudgeInfo);
			NetworkComms.AppendGlobalIncomingPacketHandler<string>("ServerCancelRoutine", HandleCancelRoutine);
		}

		private static void HandleStartRoutine(PacketHeader header, Connection connection, InitRoutineData startData)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ClientWindow.TeamName = startData.PlayersNames;
				ClientWindow.RoutineLengthMinutes = startData.RoutineLengthMinutes;

				ClientWindow.StartRoutine();
			}));
		}

		private static void HandleSetPlayingTeam(PacketHeader header, Connection connection, InitRoutineData startData)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ClientWindow.TeamName = startData.PlayersNames;
				ClientWindow.RoutineLengthMinutes = startData.RoutineLengthMinutes;
			}));
		}

		private static void HandleSetJudgeInfo(PacketHeader header, Connection connection, JudgeData judgeData)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ClientWindow.JudgeName = judgeData.JudgeName;
				ClientWindow.JudgeCategory = judgeData.Category;
			}));
		}

		private static void HandleCancelRoutine(PacketHeader header, Connection connection, string param)
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
			{
				ClientWindow.CancelRoutine();
			}));
		}

		private void OnDialInput(EDialInput input)
		{
			float newValue = DialValue;
			switch (input)
			{
				case EDialInput.Up:
					newValue += DialIncrementValue;
					break;
				case EDialInput.Down:
					newValue -= DialIncrementValue;
					break;
			}

			SetDialValue(newValue);
		}

		private void SetDialValue(float newValue)
		{
			DialValue = Math.Max(0f, Math.Min(10f, newValue));

			PlayNumberSound(DialValue);

			if (IsJudging)
			{
				RoutineScore.DialInputs.Add(new DialInputData(DialValue, (float)RoutineTimer.ElapsedSeconds));
			}
		}

		private void StartRoutine()
		{
			DialValue = 0f;
			RoutineScore.DialInputs.Clear();
			RoutineScore.JudgeName = JudgeName;
			RoutineScore.PlayerNames = TeamName;

			RoutineTimer.StartRoutine(RoutineLengthMinutes);
		}

		private void FinishRoutine()
		{
			RoutineScore.DialInputs.Add(new DialInputData(DialValue, (float)RoutineTimer.ElapsedSeconds));

			CreateBackup();

			NotifyPropertyChanged("TotalScoreString");
			NotifyPropertyChanged("DisplayTimeString");

			DialValue = 0f;

			// send results
			NetworkComms.SendObject("JudgeFinishedScore", ClientCon.ServerIp, ClientCon.ServerPort, RoutineScore);
		}

		private void CancelRoutine()
		{
			RoutineTimer.StopRoutine();

			DialValue = 0f;
			RoutineScore.DialInputs.Clear();
		}

		public void CreateBackup()
		{
			try
			{
				if (!Directory.Exists("DialJudgerClientBackups"))
				{
					Directory.CreateDirectory("DialJudgerClientBackups");
				}

				string filename = "DialJudgerClientBackups\\" + RoutineScore.JudgeName + "_" + RoutineScore.PlayerNames + "_" +
					RoutineScore.GetScoreString() + "_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".txt";

				XmlSerializer serializer = new XmlSerializer(typeof(DialRoutineScoreData));
				using (StringWriter retString = new StringWriter())
				{
					serializer.Serialize(retString, RoutineScore);
					using (StreamWriter saveFile = new StreamWriter(filename))
					{
						saveFile.Write(retString.ToString());
					}
				}
			}
			catch
			{
			}
		}

		public void SendBackupToServer()
		{
			try
			{
				Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
				ofd.DefaultExt = ".txt";
				ofd.Filter = "Text Files (*.txt)|*.txt";
				ofd.Multiselect = true;

				if (ofd.ShowDialog() == true)
				{
					foreach (string filename in ofd.FileNames)
					{
						using (StreamReader saveFile = new StreamReader(filename))
						{
							XmlSerializer serializer = new XmlSerializer(typeof(DialRoutineScoreData));
							DialRoutineScoreData backupScore = (DialRoutineScoreData)serializer.Deserialize(saveFile);

							NetworkComms.SendObject("JudgeSendBackupScore", ClientCon.ServerIp, ClientCon.ServerPort, backupScore);
						}
					}
				}
			}
			catch
			{
				// Popup Error
			}
		}

		private void SendBackupButton_Click(object sender, RoutedEventArgs e)
		{
			SendBackupToServer();
		}

		void OnClientIdChanged(ClientIdData newClientId)
		{
			NotifyPropertyChanged("ClientId");
			NotifyPropertyChanged("DisplayWindowTitle");
		}

		#region KeyboardHooks
		private void SetHook()
		{
			if (!bIsHooked)
			{
				hookID = SetHook(proc);

				bIsHooked = true;
			}
		}

		private void UnHook()
		{
			UnhookWindowsHookEx(hookID);

			bIsHooked = false;
		}

		private static IntPtr SetHook(LowLevelKeyboardProc proc)
		{
			using (Process curProcess = Process.GetCurrentProcess())

			using (ProcessModule curModule = curProcess.MainModule)
			{
				return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
					GetModuleHandle(curModule.ModuleName), 0);
			}
		}

		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

		private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
		{
			if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
			{
				int vkCode = Marshal.ReadInt32(lParam);

				if (vkCode == 175)
				{
					Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
					{
						ClientWindow.OnDialInput(EDialInput.Up);
					}));

					return (IntPtr)1;
				}
				else if (vkCode == 174)
				{
					Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
					{
						ClientWindow.OnDialInput(EDialInput.Down);
					}));

					return (IntPtr)1;
				}
				else if (vkCode == 173)
				{
					Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ThreadStart(delegate
					{
						ClientWindow.OnDialInput(EDialInput.Click);
					}));

					return (IntPtr)1;
				}
			}

			return CallNextHookEx(hookID, nCode, wParam, lParam);
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWindowsHookEx(IntPtr hhk);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string lpModuleName);
		#endregion
	}

	public enum EDialSpeed
	{
		Slow,
		Medium,
		Fast
	}
}
