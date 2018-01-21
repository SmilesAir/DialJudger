using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace CommonClasses
{
	public class CommonDebug
	{
#if DEBUG
		public static bool bEnableDebug = true;
#else
		public static bool bEnableDebug = false;
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

	[ProtoContract]
	public class ClientIdData
	{
		[ProtoMember(1)]
		public string Name = "";

		[ProtoMember(2)]
		public int OptionalNumberId = CommonValues.InvalidNumberId;

		public string DisplayName { get { return Name + (OptionalNumberId == CommonValues.InvalidNumberId ? "" : " (" + OptionalNumberId + ")"); } }

		public ClientIdData()
		{
		}

		public ClientIdData(string name)
		{
			Name = name;
		}

		public ClientIdData(string name, int numberId)
		{
			Name = name;
			OptionalNumberId = numberId;
		}

		public bool CompareTo(ClientIdData id)
		{
			return Name == id.Name && OptionalNumberId == id.OptionalNumberId;
		}
	}

	[ProtoContract]
	public class ScoreData
	{
		[ProtoMember(1)]
		public float Score = 0f;

		public ScoreData()
		{
		}
	}

	[ProtoContract]
	public class ScoreUpdateData
	{
		[ProtoMember(1)]
		public ClientIdData Judge = null;

		[ProtoMember(2)]
		public ScoreData Score = new ScoreData();

		public ScoreUpdateData()
		{
		}

		public ScoreUpdateData(ClientIdData id, float score)
		{
			Judge = id;
			Score.Score = score;
		}
	}

	public class RoutineData
	{
		public ClientIdData Judge = null;
		public List<ScoreData> Scores = new List<ScoreData>();

		public RoutineData()
		{
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
		public string JudgeName = "";

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

		public float GetTotalScore(float additionalSeconds)
		{
			float total = 0f;

			for (int i = 0; i < DialInputs.Count - 1; ++i)
			{
				DialInputData first = DialInputs[i];
				DialInputData second = DialInputs[i + 1];

				float timeDelta = second.TimeSeconds - first.TimeSeconds;

				total += timeDelta * first.DialScore;
			}

			if (additionalSeconds > 0f && DialInputs.Count > 0)
			{
				DialInputData last = DialInputs.Last();

				total += additionalSeconds * last.DialScore;
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
				return string.Format("{0:0}:{1:00}", remainingSeconds / 60, remainingSeconds % 60);
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
}
