rmdir /s /q DialJudgeBins
mkdir DialJudgeBins
mkdir DialJudgeBins\Client
mkdir DialJudgeBins\Scoreboard
mkdir DialJudgeBins\Server

xcopy Client\bin\Release DialJudgeBins\Client
xcopy Scoreboard\bin\Release DialJudgeBins\Scoreboard
xcopy Server\bin\Release DialJudgeBins\Server

xcopy Build DialJudgeBins
