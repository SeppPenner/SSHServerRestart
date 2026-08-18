cd ..\src

@ECHO off
cls

FOR /d /r . %%d in (bin,obj) DO (
	IF EXIST "%%d" (		 	 
		ECHO %%d | FIND /I "\node_modules\" > Nul && ( 
			ECHO.Skipping: %%d
		) || (
			ECHO.Deleting: %%d
			rd /s/q "%%d"
		)
	)
)

@ECHO on
@ECHO.Publishing self contained for win-x64...
@cd SSHServerRestart
@git diff --quiet HEAD -- Config.xml
@IF ERRORLEVEL 1 ECHO.NOTE: The local Config.xml differs from the committed one, it will not be shipped.
@dotnet publish -c Release -r win-x64 --self-contained true -o bin/publish
@ECHO.Deleting *.pdb files...
@cd bin/publish
@del *.pdb
@ECHO.Overwriting the published Config.xml with the committed one so that no credentials are shipped...
@git show HEAD:src/SSHServerRestart/Config.xml > Config.template
@IF ERRORLEVEL 1 GOTO NoTemplate
@type Config.template | find /v "" > Config.xml
@del Config.template
@ECHO.Build successful. Press any key to exit.
pause
@EXIT /B 0

:NoTemplate
@IF EXIST Config.template del Config.template
@IF EXIST Config.xml del Config.xml
@ECHO.
@ECHO.ERROR: Could not read Config.xml from HEAD, so the published one was deleted instead of
@ECHO.shipping the working copy, which may hold real credentials. Do not build the installer.
@pause
@EXIT /B 1