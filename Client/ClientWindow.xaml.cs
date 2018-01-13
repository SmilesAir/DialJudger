using CommonClasses;
using NetworkCommsDotNet;
using NetworkCommsDotNet.Connections;
using NetworkCommsDotNet.Connections.UDP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

namespace Client
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		System.Timers.Timer BroadcastTimer = new System.Timers.Timer();
		System.Timers.Timer SendScoreTimer = new System.Timers.Timer();
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
		private float dialValue = 0f;
		public float DialValue
		{
			get { return dialValue; }
			set
			{
				dialValue = value;
				NotifyPropertyChanged("DialValue");
				NotifyPropertyChanged("DisplayDialValue");
			}
		}
		private float DialIncrementValue = .1f;
		private EDialSpeed DialSpeed = EDialSpeed.Slow;
		private ClientIdData clientId = null;
		public ClientIdData ClientId
		{
			get { return clientId; }
			set
			{
				clientId = value;
				NotifyPropertyChanged("ClientId");
				NotifyPropertyChanged("DisplayWindowTitle");
			}
		}
		public string ServerIp = "";
		public int ServerPort = 0;
		bool bIsHooked = false;

		static MainWindow ClientWindow;

		#region Display
		public string DisplayWindowTitle { get { return "Dial Judge Client - " + (ClientId == null ? "None" : ClientId.DisplayName); } }
		private string displayJudgeName = "No Judge Name";
		public string DisplayJudgeName
		{
			get { return "Judge: " + displayJudgeName; }
			set
			{
				displayJudgeName = value;
				NotifyPropertyChanged("DisplayJudgeName");
			}
		}
		private string displayTeamName = "No Team";
		public string DisplayTeamName
		{
			get { return "Team: " + displayTeamName; }
			set
			{
				displayTeamName = value;
				NotifyPropertyChanged("DisplayTeamName");
			}
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

			ClientWindow = this;

			this.DataContext = this;

			ClientWindow.ClientId = new ClientIdData(Environment.MachineName, CommonDebug.GetOptionalNumberId());
		}

		private void InitTimers()
		{
			BroadcastTimer.Interval = 1000;
			BroadcastTimer.AutoReset = true;
			BroadcastTimer.Elapsed += BroadcastTimer_Elapsed;

			BroadcastTimer.Start();

			SendScoreTimer.Interval = 500;
			SendScoreTimer.AutoReset = true;
			SendScoreTimer.Elapsed += SendScoreTimer_Elapsed;

			SendScoreTimer.Start();
		}

		private void SendScoreTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (IsConnected)
			{
				NetworkComms.SendObject("JudgeScoreUpdate", ClientWindow.ServerIp, ClientWindow.ServerPort,
					new ScoreUpdateData(ClientId, DialValue));
			}
		}

		private void BroadcastTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			BroadcastFindServer();
		}

		#region Window Events
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Track track = (Track)ValueSlider.Template.FindName("PART_Track", ValueSlider);
			track.Thumb.Width = 80f;
			track.Thumb.Height = 80f;

			AppendHandlers();

			InitTimers();

			SetHook();

			Connection.StartListening(ConnectionType.UDP, new IPEndPoint(IPAddress.Any, 0), true);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
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
			NetworkComms.AppendGlobalConnectionEstablishHandler(OnConnectionEstablished);
			NetworkComms.AppendGlobalConnectionCloseHandler(OnConnectionClosed);

			NetworkComms.AppendGlobalIncomingPacketHandler<string>("BroadcastServerInfo", HandleBroadcastServerInfo);
		}

		private static void HandleBroadcastServerInfo(PacketHeader header, Connection connection, string serverInfo)
		{
			ClientWindow.ServerIp = serverInfo.Split(':').First();
			ClientWindow.ServerPort = int.Parse(serverInfo.Split(':').Last());

			NetworkComms.SendObject("ClientConnect", ClientWindow.ServerIp, ClientWindow.ServerPort, ClientWindow.ClientId);
		}

		private void BroadcastFindServer()
		{
			IPEndPoint ipEndPoint = Connection.ExistingLocalListenEndPoints(ConnectionType.UDP)[0] as IPEndPoint;
			UDPConnection.SendObject("BroadcastFindServer", ipEndPoint.Port, new IPEndPoint(IPAddress.Broadcast, 10000));
		}

		private static void OnConnectionEstablished(Connection connection)
		{
			ClientWindow.IsConnected = true;
		}

		private static void OnConnectionClosed(Connection connection)
		{
			ClientWindow.IsConnected = false;
		}

		private void OnDialInput(EDialInput input)
		{
			switch (input)
			{
				case EDialInput.Up:
					DialValue += DialIncrementValue;
					break;
				case EDialInput.Down:
					DialValue -= DialIncrementValue;
					break;
			}

			DialValue = Math.Max(0f, Math.Min(10f, DialValue));
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
