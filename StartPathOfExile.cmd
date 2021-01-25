REM POE vmware loader
REM CONFIG START
SET limitedUser="liza"
REM Yep you need both
SET gamepath1=\"D:\games\poe\"
SET gamepath2=D:\games\poe
REM CONFIG END

REM killing related to poe apps
taskkill /f /im "Loader.exe"
taskkill /f /im "Loader.exe"

REM removing cache from documents folder
DEL "C:\Users\%limitedUser%\Documents\My Games\Path of Exile\Countdown" /s /q
DEL "C:\Users\%limitedUser%\Documents\My Games\Path of Exile\DailyDealCache" /s /q
DEL "C:\Users\%limitedUser%\Documents\My Games\Path of Exile\Minimap" /s /q
DEL "C:\Users\%limitedUser%\Documents\My Games\Path of Exile\MOTDCache" /s /q
DEL "C:\Users\%limitedUser%\Documents\My Games\Path of Exile\ShopImages" /s /q

ECHO Enter limited user password below
RUNAS /user:%limitedUser% /savecred "cmd /C pushd %gamepath1% && PathOfExile_x64.exe --nologo --nopatch --nosound"

REM waiting 10 sec or until user close poe
for /l %%x in (1, 1, 10) do (
  Timeout /T 1 /Nobreak
  tasklist | find /i "PathOfExile_x64" >nul 2>&1
  IF ERRORLEVEL 1 (
    GOTO INITIAL_DELAY_END
  )
)
:INITIAL_DELAY_END

REM moving exe so poe cant access it (ghetto anti anti cheat, makes you slightly safer)
pushd %gamepath2%
mkdir "1"
move /Y "Client.exe" "1"
move /Y "PathOfExile.exe" "1"
move /Y "PathOfExile_x64.exe" "1"

REM waiting for poe to exit
:MAIN_LOOP_START
tasklist | find /i "PathOfExile_x64" >nul 2>&1
IF ERRORLEVEL 1 (
  GOTO MAIN_LOOP_END
) ELSE (
  ECHO POE is still running
  Timeout /T 1 /Nobreak
  GOTO MAIN_LOOP_START
)
:MAIN_LOOP_END

REM restoring game exe
pushd %gamepath2%\1
move /Y "Client.exe" %gamepath2%
move /Y "PathOfExile.exe" %gamepath2%
move /Y "PathOfExile_x64.exe" %gamepath2%

REM killing related to poe apps
taskkill /f /im "Loader.exe"
REM killing all cmd windows
taskkill /f /im "cmd.exe"
taskkill /f /im "conhost.exe"
