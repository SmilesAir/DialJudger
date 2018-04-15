using CommonClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Server
{
	class ResultsAnalyzer
	{
		List<string> filenames = new List<string>();
		EAnalysisType analysisType = EAnalysisType.Default;

		public ResultsAnalyzer()
		{
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Pro Open\Final.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Pro Open\Semi A.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Pro Open\Semi B.txt");

			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Challenger Open\Final.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Challenger Open\Semi A.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Challenger Open\Semi B.txt");

			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Coop\Final.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Coop\Semi A.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Coop\Semi B.txt");

			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Mixed\Final.txt");
			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Mixed\Semi.txt");

			filenames.Add(@"C:\Users\Ryan\Desktop\Frisbeer 2018\Pools\Women\Final.txt");
		}

		public string DoAnalysis(EAnalysisType type)
		{
			analysisType = type;

			string retString = "";

			foreach (string filename in filenames)
			{
				if (type == EAnalysisType.Default)
				{
					retString += filename.Substring(filename.IndexOf("Pools") + 6).Replace(".txt", "").Replace("\\", " - ") + "\r\n";
				}
				else
				{
					retString += "\r\n";
				}
				retString += AnalyzeSaveFile(filename) + "\r\n\r\n";
			}

			return retString;
		}

		string AnalyzeSaveFile(string filename)
		{
			if (File.Exists(filename))
			{
				using (StreamReader saveFile = new StreamReader(filename))
				{
					XmlSerializer serializer = new XmlSerializer(typeof(SaveData));
					SaveData saveData = (SaveData)serializer.Deserialize(saveFile);
					
					switch (analysisType)
					{
						case EAnalysisType.Default:
							return CalcDefault(saveData);
						case EAnalysisType.DropHighLowContinuous:
							return CalcHighLowContinuous(saveData);
					}
				}
			}

			return "";
		}

		string CalcDefault(SaveData saveData)
		{
			string resultsText = "";

			List<TeamData> sortedTeams = saveData.CalcSortedTeams();
			foreach (TeamData team in sortedTeams)
			{
				resultsText += team.PlayerNamesString + "\tRank: " + team.RankString + "\tPoints: " + team.TotalScoreString +
					"\r\n";
			}

			return resultsText;
		}

		bool TryFindNextInputEventTime(TeamData team, float startTime, out float nextTime)
		{
			nextTime = 0f;
			bool bFoundFirst = false;

			foreach (DialRoutineScoreData data in team.JudgesScores)
			{
				foreach (DialInputData input in data.DialInputs)
				{
					if (input.TimeSeconds > startTime && (!bFoundFirst || input.TimeSeconds < nextTime))
					{
						bFoundFirst = true;
						nextTime = input.TimeSeconds;

						break;
					}
				}
			}

			return bFoundFirst;
		}

		string CalcHighLowContinuous(SaveData saveData)
		{
			if (saveData.TeamList.Count == 0)
			{
				return "";
			}

			if (saveData.TeamList[0].JudgesScores.Count <= 2)
			{
				return CalcDefault(saveData);
			}

			string resultsText = "";
			List<Tuple<float, string>> unsortedStrings = new List<Tuple<float, string>>();

			foreach (TeamData team in saveData.TeamList)
			{
				List<float> judgeScores = new List<float>();
				List<float> highTime = new List<float>();
				List<float> validTime = new List<float>();
				List<float> lowTime = new List<float>();
				int judgeCount = team.JudgesScores.Count;
				for (int i = 0; i < judgeCount; ++i)
				{
					judgeScores.Add(0f);

					highTime.Add(0f);
					validTime.Add(0f);
					lowTime.Add(0f);
				}

				float startTime = 0f;
				float nextTime;

				while (TryFindNextInputEventTime(team, startTime, out nextTime))
				{
					float highScore = float.NegativeInfinity;
					int highIndex = -1;
					float lowScore = float.PositiveInfinity;
					int lowIndex = -1;
					int judgeIndex = 0;
					List<float> judgeWindowScore = new List<float>();
					foreach (DialRoutineScoreData data in team.JudgesScores)
					{
						float score = data.CalcScoreWindow(startTime, nextTime);
						judgeWindowScore.Add(score);

						if (score > highScore)
						{
							highScore = score;
							highIndex = judgeIndex;
						}

						if (score < lowScore)
						{
							lowScore = score;
							lowIndex = judgeIndex;
						}

						++judgeIndex;
					}

					for (int i = 0; i < judgeCount; ++i)
					{
						if (i == highIndex)
						{
							highTime[i] += nextTime - startTime;
						}
						else if (i == lowIndex)
						{
							lowTime[i] += nextTime - startTime;
						}
						else
						{
							validTime[i] += nextTime - startTime;
							judgeScores[i] += judgeWindowScore[i];
						}
					}

					startTime = nextTime;
				}

				float totalScore = 0f;
				foreach (float score in judgeScores)
				{
					totalScore += score;
				}

				string validTimeString = "";
				foreach (float validTimeSeconds in validTime)
				{
					validTimeString += validTimeSeconds.ToString("0.0") + "  ";
				}

				string highTimeString = "";
				foreach (float highTimeSeconds in highTime)
				{
					highTimeString += highTimeSeconds.ToString("0.0") + "  ";
				}

				string lowTimeString = "";
				foreach (float lowTimeSeconds in lowTime)
				{
					lowTimeString += lowTimeSeconds.ToString("0.0") + "  ";
				}

				float minScore = float.PositiveInfinity;
				float maxScore = float.NegativeInfinity;
				foreach (DialRoutineScoreData data in team.JudgesScores)
				{
					minScore = Math.Min(data.GetTotalScore(), minScore);
					maxScore = Math.Max(data.GetTotalScore(), maxScore);
				}

				unsortedStrings.Add(new Tuple<float, string>(totalScore, team.PlayerNamesString + "\tRank: #\tPoints: " + totalScore.ToString("0.0") +
					"\t\tValid Time: " + validTimeString + "\tHigh Time: " + highTimeString + "\tLow Time: " + lowTimeString +
					"\tSeparation: " + (maxScore - minScore).ToString("0.0")));
			}

			unsortedStrings.Sort((Tuple<float, string> a, Tuple<float, string> b) =>
			{
				if (a.Item1 == b.Item1)
				{
					return 0;
				}
				else if (a.Item1 > b.Item1)
				{
					return -1;
				}
				else
				{
					return 1;
				}
			});

			for (int i = 0; i < unsortedStrings.Count; ++i)
			{
				resultsText += unsortedStrings[i].Item2.Replace("#", (i + 1).ToString()) + "\r\n";
			}

			return resultsText;
		}
	}

	public enum EAnalysisType
	{
		Default,
		DropHighLowContinuous,
		DropHighLowTotal,
		Max
	}
}
