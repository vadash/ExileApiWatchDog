SET limitedUser="liza"
SET gamepath1=\"D:\games\poe\"
RUNAS /user:%limitedUser% /savecred "cmd /C pushd %gamepath1% && PathOfExile_x64.exe --nologo --nopatch --softwareaudio"
