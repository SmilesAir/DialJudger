
cd Judger
del Builds /Q /F /S
git pull origin master
git checkout -- Builds
call Build ..\
cd ..
