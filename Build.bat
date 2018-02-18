rmdir /s /q DialJudgeBins
mkdir DialJudgeBins
mkdir DialJudgeBins\Client
mkdir DialJudgeBins\Scoreboard
mkdir DialJudgeBins\Server
mkdir DialJudgeBins\Overlay

xcopy Client\bin\Release DialJudgeBins\Client
xcopy Scoreboard\bin\Release DialJudgeBins\Scoreboard
xcopy Server\bin\Release DialJudgeBins\Server
xcopy Overlay\bin\Release DialJudgeBins\Overlay

xcopy Build DialJudgeBins
