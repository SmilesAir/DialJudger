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
	public class RoutineScoreData
	{
		public float TotalScore
		{
			get { return 0f; }
		}

		public string ScoreString
		{
			get { return TotalScore.ToString("0.0"); }
		}
	}
}
