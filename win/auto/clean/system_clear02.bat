@echo off
rem Nyomtatási várólista karbantartása.
net stop spooler
del %windir%\system32\spool\printers\*.* /s /q /f
net start spooler

rem Törli a letöltött telepítőit a frissítéseknek és alkalmazásoknak.
net stop wuauserv
del "%windir%\softwaredistribution\*.*" /s /q /f
rmdir %windir%\softwaredistribution /s
mkdir %windir%\softwaredistribution
net start wuauserv

rem A TEMP mappákban található fájlok és könyvtárak törlésére szolgál.
del %temp%\*.* /s /q /f
rmdir %temp%\*.* /s /q

rem Ezt a két parancsot elég egy alkalommal futtatni!
rem Nyisd meg a parancssort rendszergazdaként és úgy futtasd a parancsokat.
rem powercfg /hibernate off

rem Egyszer kell futtatni ezt a parancsot és alkalmazni a kívánt opciókat.
rem Érdemes az összes karbantartási lehetőséget bejelölni.
rem Ezután már csak a "/sagerun:1" kapcsolóval kell futtatni a parancsot
rem és az elvégzi az összes korábban beállított karbantartási opciót.
rem cleanmgr /sageset:1
cleanmgr /sagerun:1

rem Elég egy alkalommal futtatni a szervizcsomag telepítése után!
rem Nyisd meg a parancssort rendszergazdaként és úgy futtasd a parancsokat.
rem dism /online /cleanup-image /spsuperseded

pause

rem A karbantartás befejezése után, újraindítja a számítógépet
shutdown -r -t 01
